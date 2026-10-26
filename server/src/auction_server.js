const WebSocket = require('ws');
const { v4: uuidv4 } = require('uuid');
const db = require('./db');
const { LobbyManager } = require('./lobby_sync');
const { DealershipAndMonetizationManager } = require('./monetization_dealership');
const { StreetRaceWagerManager } = require('./race_wager_server');

const PORT = process.env.PORT || 8080;
const wss = new WebSocket.Server({ port: PORT, maxPayload: 16 * 1024 });

// In-memory working copy. PostgreSQL is the source of truth: after every command the changed
// rows are written in one transaction, and if that fails the copy is reloaded from the database.
const players = new Map();
const cars = new Map();
const activeAuctions = new Map();
const lobby = new LobbyManager();
const dealership = new DealershipAndMonetizationManager();
const raceManager = new StreetRaceWagerManager();

const MARKET_TAX_RATE = 0.05; // 5% commission to prevent inflation
const ANTI_SNIPE_WINDOW_MS = 10000; // Last 10 seconds
const ANTI_SNIPE_EXTEND_MS = 15000; // Extend by 15 seconds
const ROOM_ID_RE = /^[A-Za-z0-9_-]{1,32}$/;
const PINK_SLIP_ENABLED = process.env.PINK_SLIP === '1'; // Stays off until race times are server-measured
const RACE_EXPIRY_MS = 10 * 60 * 1000;
const VOICE_RANGE_M = 22; // Matches ProximityVoiceChat.maxHearingDistance on the client

const TABLE_KEYS = {
    players: ['id'],
    cars: ['vin'],
    car_owners: ['vin', 'seq'],
    vip_plates: ['plate'],
    auctions: ['vin'],
    races: ['race_id']
};
const WRITE_ORDER = Object.keys(TABLE_KEYS); // parents before children
const LEDGER_FIELDS = ['balance', 'escrow_locked', 'vip_coins'];

// ---------------------------------------------------------------- command queue and outbox

let outbox = null; // messages of the running command, delivered only after its transaction commits

function deliver(fn) {
    if (outbox) outbox.push(fn);
    else fn();
}

function send(ws, payload) {
    deliver(() => {
        if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(payload));
    });
}

// Recipients are chosen when the command runs, not when its outbox is flushed: a player who is
// removed from a room during the command (anti-cheat, disconnect) must still receive that notice.
function sendTo(targets, payload) {
    const message = JSON.stringify(payload);
    deliver(() => {
        targets.forEach(client => {
            if (client.readyState === WebSocket.OPEN) client.send(message);
        });
    });
}

function broadcast(payload) {
    sendTo([...wss.clients], payload);
}

function broadcastToRoom(roomId, payload) {
    sendTo([...wss.clients].filter(c => c.roomId === roomId), payload);
}

function broadcastToPlayers(playerIds, payload) {
    sendTo([...wss.clients].filter(c => playerIds.includes(c.playerId)), payload);
}

// ---------------------------------------------------------------- persistence

let persisted = new Map(); // "table|key" -> { table, pk, row, json }

// The rows the database should hold for the current in-memory state
function collect() {
    const out = new Map();
    const put = (table, row) => {
        const key = `${table}|${TABLE_KEYS[table].map(c => row[c]).join('|')}`;
        out.set(key, { table, row, json: JSON.stringify(row) });
    };

    for (const p of players.values()) {
        put('players', {
            id: p.id,
            username: p.username,
            balance: p.balance,
            escrow_locked: p.escrowLocked,
            vip_coins: p.vipCoins ?? 0
        });
    }

    for (const c of cars.values()) {
        put('cars', {
            vin: c.vin,
            owner_id: c.ownerId,
            model_name: c.modelName,
            horsepower: c.horsepower,
            odometer_km: c.odometerKm ?? 0,
            accidents_count: c.accidentsCount ?? 0,
            condition_percent: c.conditionPercent ?? 100,
            last_accident_at: c.lastAccidentAt ?? 0,
            last_accident_speed_kmh: c.lastAccidentSpeedKmh ?? null,
            tuning: c.tuning ? JSON.stringify(c.tuning) : null,
            custom_parts: JSON.stringify(c.customParts ?? [])
        });
        (c.ownersHistory ?? []).forEach((ownerId, seq) => put('car_owners', { vin: c.vin, seq, owner_id: ownerId }));
    }

    for (const pl of dealership.vipPlatesMarket.values()) {
        put('vip_plates', { plate: pl.plate, tier: pl.tier, price_coins: pl.priceCoins, owner_id: pl.ownerId ?? null });
    }

    for (const a of activeAuctions.values()) {
        put('auctions', {
            vin: a.vin,
            auction_id: a.auctionId,
            seller_id: a.sellerId,
            current_bid: a.currentBid,
            buyout_price: a.buyoutPrice,
            highest_bidder_id: a.highestBidderId ?? null,
            end_time: a.endTime,
            status: a.status
        });
    }

    for (const r of raceManager.activeChallenges.values()) {
        put('races', {
            race_id: r.raceId,
            challenger_id: r.challengerId,
            opponent_id: r.opponentId,
            challenger_vin: r.challengerVin,
            opponent_vin: r.opponentVin ?? null,
            bet_amount: r.betAmount,
            is_pink_slip: r.isPinkSlip,
            status: r.status,
            start_time: r.startTime ?? null,
            created_at: r.createdAt ?? 0,
            results: JSON.stringify(r.results ?? {})
        });
    }

    return out;
}

async function upsertRow(tx, entry) {
    const cols = Object.keys(entry.row);
    const pk = TABLE_KEYS[entry.table];
    const values = cols.map(c => entry.row[c]);
    const placeholders = cols.map((_, i) => `$${i + 1}`).join(', ');
    const updates = cols.filter(c => !pk.includes(c)).map(c => `${c} = EXCLUDED.${c}`);
    // car_owners is append-only; every other table is updated in place
    const onConflict = entry.table === 'car_owners' || updates.length === 0
        ? 'DO NOTHING'
        : `DO UPDATE SET ${updates.join(', ')}`;
    await tx.query(
        `INSERT INTO ${entry.table} (${cols.join(', ')}) VALUES (${placeholders}) ON CONFLICT (${pk.join(', ')}) ${onConflict}`,
        values
    );
}

async function deleteRow(tx, entry) {
    const pk = TABLE_KEYS[entry.table];
    const where = pk.map((c, i) => `${c} = $${i + 1}`).join(' AND ');
    await tx.query(`DELETE FROM ${entry.table} WHERE ${where}`, pk.map(c => entry.row[c]));
}

// Writes the difference between the database copy and memory, plus ledger rows for money changes
async function flush(reason, withLedger) {
    const now = collect();
    const writes = [];
    const deletes = [];
    const ledger = [];

    for (const [key, entry] of now) {
        const old = persisted.get(key);
        if (!old || old.json !== entry.json) writes.push(entry);
        if (withLedger && entry.table === 'players') {
            for (const field of LEDGER_FIELDS) {
                const delta = entry.row[field] - (old ? old.row[field] : 0);
                if (delta !== 0) ledger.push({ player_id: entry.row.id, field, delta, reason });
            }
        }
    }
    for (const [key, entry] of persisted) {
        if (!now.has(key)) deletes.push(entry);
    }

    if (writes.length === 0 && deletes.length === 0) {
        persisted = now;
        return;
    }

    await db.transaction(async (tx) => {
        for (const entry of deletes.sort((a, b) => WRITE_ORDER.indexOf(b.table) - WRITE_ORDER.indexOf(a.table))) {
            await deleteRow(tx, entry);
        }
        for (const entry of writes.sort((a, b) => WRITE_ORDER.indexOf(a.table) - WRITE_ORDER.indexOf(b.table))) {
            await upsertRow(tx, entry);
        }
        for (const l of ledger) {
            await tx.query(
                'INSERT INTO ledger (player_id, field, delta, reason) VALUES ($1, $2, $3, $4)',
                [l.player_id, l.field, l.delta, l.reason]
            );
        }
    });
    persisted = now;
}

// Loads the whole state from PostgreSQL and makes the database match the in-memory defaults
async function bootstrap() {
    players.clear();
    cars.clear();
    activeAuctions.clear();
    raceManager.activeChallenges.clear();
    for (const item of dealership.vipPlatesMarket.values()) item.ownerId = null;

    const { rows: playerRows } = await db.pool.query('SELECT * FROM players');
    for (const r of playerRows) {
        players.set(r.id, { id: r.id, username: r.username, balance: r.balance, escrowLocked: r.escrow_locked, vipCoins: r.vip_coins });
    }

    const { rows: ownerRows } = await db.pool.query('SELECT vin, seq, owner_id FROM car_owners ORDER BY vin, seq');
    const history = new Map();
    for (const o of ownerRows) {
        if (!history.has(o.vin)) history.set(o.vin, []);
        history.get(o.vin).push(o.owner_id);
    }

    const { rows: carRows } = await db.pool.query('SELECT * FROM cars');
    for (const r of carRows) {
        cars.set(r.vin, {
            vin: r.vin,
            ownerId: r.owner_id,
            modelName: r.model_name,
            horsepower: r.horsepower,
            odometerKm: Number(r.odometer_km),
            accidentsCount: r.accidents_count,
            lastAccidentSpeedKmh: r.last_accident_speed_kmh ?? undefined,
            conditionPercent: r.condition_percent,
            lastAccidentAt: r.last_accident_at,
            tuning: r.tuning ?? null,
            customParts: r.custom_parts ?? [],
            ownersHistory: history.get(r.vin) ?? [r.owner_id]
        });
    }

    const { rows: plateRows } = await db.pool.query('SELECT plate, owner_id FROM vip_plates');
    for (const r of plateRows) {
        const item = dealership.vipPlatesMarket.get(r.plate);
        if (item) item.ownerId = r.owner_id;
    }

    const { rows: auctionRows } = await db.pool.query('SELECT * FROM auctions');
    for (const r of auctionRows) {
        activeAuctions.set(r.vin, {
            auctionId: r.auction_id,
            vin: r.vin,
            carDetails: cars.get(r.vin),
            sellerId: r.seller_id,
            currentBid: r.current_bid,
            buyoutPrice: r.buyout_price,
            highestBidderId: r.highest_bidder_id ?? null,
            endTime: r.end_time,
            status: r.status
        });
    }

    const { rows: raceRows } = await db.pool.query('SELECT * FROM races');
    for (const r of raceRows) {
        raceManager.activeChallenges.set(r.race_id, {
            raceId: r.race_id,
            challengerId: r.challenger_id,
            challengerVin: r.challenger_vin,
            opponentId: r.opponent_id,
            opponentVin: r.opponent_vin ?? null,
            betAmount: r.bet_amount,
            isPinkSlip: r.is_pink_slip,
            status: r.status,
            startTime: r.start_time ?? undefined,
            createdAt: r.created_at,
            results: r.results ?? {}
        });
    }

    // Empty memory copy: the first flush writes everything, which seeds new VIP plates too.
    // No ledger rows here, because these balances are already in the database.
    persisted = new Map();
    await flush('bootstrap', false);
}

const ready = db.migrate().then(bootstrap);
ready.catch(err => {
    console.error('[db] startup failed:', err.message);
    process.exit(1);
});

let queue = ready;

// Every state change goes through here, one at a time, so checks and writes never interleave
function runCommand(label, ws, fn) {
    const job = queue.then(async () => {
        outbox = [];
        try {
            fn();
            await flush(label, true);
            const toSend = outbox;
            outbox = null;
            toSend.forEach(deliverNow => deliverNow());
        } catch (err) {
            outbox = null;
            console.error(`[db] ${label} failed, reloading state from PostgreSQL:`, err.message);
            try {
                await bootstrap();
            } catch (reloadErr) {
                console.error('[db] reload failed:', reloadErr.message);
            }
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ event: 'ERROR', message: 'Server xatosi. Qayta urinib ko\'ring.' }));
            }
        }
    });
    queue = job.catch(() => {});
    return job;
}

// ---------------------------------------------------------------- game helpers

function createSampleCar(ownerId, modelName, horsepower) {
    const vin = `NGD-${uuidv4().slice(0, 8).toUpperCase()}`;
    const car = {
        vin,
        ownerId,
        modelName,
        horsepower,
        odometerKm: 0,
        accidentsCount: 0,
        ownersHistory: [ownerId],
        customParts: ["Stage 2 Turbo", "Forged Carbon Hood"],
        conditionPercent: 100,
        lastAccidentAt: 0,
        tuning: null
    };
    cars.set(vin, car);
    return car;
}

// A car cannot be in two races or in an auction at the same time
function carIsBusy(vin) {
    if (activeAuctions.has(vin)) return true;
    for (const race of raceManager.activeChallenges.values()) {
        if (race.status === 'FINISHED') continue;
        if (race.challengerVin === vin || race.opponentVin === vin) return true;
    }
    return false;
}

function finalizeAuction(vin) {
    const auction = activeAuctions.get(vin);
    if (!auction || auction.status !== 'ACTIVE') return;

    auction.status = 'COMPLETED';
    const car = cars.get(vin);

    if (auction.highestBidderId) {
        const buyer = players.get(auction.highestBidderId);
        const seller = players.get(auction.sellerId);

        if (buyer && seller && car) {
            buyer.escrowLocked -= auction.currentBid;
            buyer.balance -= auction.currentBid;

            const tax = Math.floor(auction.currentBid * MARKET_TAX_RATE);
            seller.balance += auction.currentBid - tax;

            // Transfer ownership and append to the Carfax history
            car.ownerId = buyer.id;
            car.ownersHistory.push(buyer.id);
        }
    }

    activeAuctions.delete(vin);
    broadcast({
        event: 'AUCTION_FINISHED',
        vin,
        winnerId: auction.highestBidderId,
        finalPrice: auction.currentBid,
        car
    });
}

// Drops stale challenges; refunds the cash bets of races that never finished
function expireRaces() {
    const now = Date.now();
    for (const [raceId, race] of raceManager.activeChallenges.entries()) {
        if (race.status === 'PENDING' && now - race.createdAt > RACE_EXPIRY_MS) {
            raceManager.activeChallenges.delete(raceId);
        } else if (race.status === 'IN_PROGRESS' && now - race.startTime > RACE_EXPIRY_MS) {
            if (!race.isPinkSlip) {
                const challenger = players.get(race.challengerId);
                const opponent = players.get(race.opponentId);
                if (challenger) challenger.balance += race.betAmount;
                if (opponent) opponent.balance += race.betAmount;
            }
            raceManager.activeChallenges.delete(raceId);
            broadcastToPlayers([race.challengerId, race.opponentId], { event: 'RACE_CANCELLED', raceId, reason: 'timeout' });
        }
    }
}

// ---------------------------------------------------------------- connections

wss.on('connection', (ws) => {
    let currentPlayerId = null;
    ws.roomId = null;
    ws.playerId = null;
    ws.voiceWindowStart = 0;
    ws.voiceCount = 0;

    function dispatch(data) {
        switch (data.action) {
            case 'AUTH': {
                currentPlayerId = data.playerId || `USER-${uuidv4().slice(0, 5)}`;
                ws.playerId = currentPlayerId;
                if (!players.has(currentPlayerId)) {
                    players.set(currentPlayerId, {
                        id: currentPlayerId,
                        username: data.username || 'Racer',
                        balance: 150000, // Starting cash
                        escrowLocked: 0,
                        vipCoins: 1200 // Starting VIP coins
                    });
                    createSampleCar(currentPlayerId, "BMW M5 CS (F90)", 635);
                }
                send(ws, {
                    event: 'AUTH_SUCCESS',
                    player: players.get(currentPlayerId),
                    cars: Array.from(cars.values()).filter(c => c.ownerId === currentPlayerId),
                    activeAuctions: Array.from(activeAuctions.values())
                });
                break;
            }

            case 'CREATE_AUCTION': {
                const { vin, startingPrice, buyoutPrice, durationSeconds } = data;
                const car = cars.get(vin);

                if (!car || car.ownerId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                }
                if (carIsBusy(vin)) {
                    return send(ws, { event: 'ERROR', message: 'Mashina auksionda yoki poygada.' });
                }
                if (!Number.isSafeInteger(startingPrice) || startingPrice < 1) {
                    return send(ws, { event: 'ERROR', message: 'Boshlang\'ich narx noto\'g\'ri.' });
                }
                const buyout = buyoutPrice === undefined || buyoutPrice === null ? startingPrice * 3 : buyoutPrice;
                if (!Number.isSafeInteger(buyout) || buyout < startingPrice) {
                    return send(ws, { event: 'ERROR', message: 'Buyout narxi noto\'g\'ri.' });
                }

                const durationMs = Math.min(Math.max(durationSeconds || 60, 30), 3600) * 1000;
                const auction = {
                    auctionId: uuidv4(),
                    vin,
                    carDetails: car,
                    sellerId: currentPlayerId,
                    currentBid: startingPrice,
                    buyoutPrice: buyout,
                    highestBidderId: null,
                    endTime: Date.now() + durationMs,
                    status: 'ACTIVE'
                };

                activeAuctions.set(vin, auction);
                broadcast({ event: 'AUCTION_CREATED', auction });
                break;
            }

            case 'PLACE_BID': {
                const { vin, bidAmount } = data;
                const auction = activeAuctions.get(vin);
                const bidder = players.get(currentPlayerId);

                if (!auction || auction.status !== 'ACTIVE') {
                    return send(ws, { event: 'ERROR', message: 'Auksion topilmadi yoki tugagan.' });
                }
                if (!Number.isSafeInteger(bidAmount)) {
                    return send(ws, { event: 'ERROR', message: 'Stavka noto\'g\'ri.' });
                }
                if (auction.sellerId === currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'O\'z mashinangizga stavka qo\'ya olmaysiz.' });
                }
                if (bidAmount <= auction.currentBid) {
                    return send(ws, { event: 'ERROR', message: 'Stavka joriy narxdan baland bo\'lishi shart!' });
                }
                if (!bidder || (bidder.balance - bidder.escrowLocked) < bidAmount) {
                    return send(ws, { event: 'ERROR', message: 'Balansingizda yetarli mablag\' yo\'q!' });
                }

                // Unlock the previous highest bidder's money
                if (auction.highestBidderId) {
                    const prevBidder = players.get(auction.highestBidderId);
                    if (prevBidder) prevBidder.escrowLocked -= auction.currentBid;
                }

                // Lock the new bidder's funds (escrow)
                bidder.escrowLocked += bidAmount;
                auction.currentBid = bidAmount;
                auction.highestBidderId = currentPlayerId;

                // Anti-sniping: extend the timer if the bid comes in the last 10 seconds
                if (auction.endTime - Date.now() < ANTI_SNIPE_WINDOW_MS) {
                    auction.endTime += ANTI_SNIPE_EXTEND_MS;
                }

                broadcast({
                    event: 'BID_UPDATED',
                    vin,
                    currentBid: auction.currentBid,
                    highestBidderId: currentPlayerId,
                    endTime: auction.endTime
                });

                if (bidAmount >= auction.buyoutPrice) {
                    finalizeAuction(vin);
                }
                break;
            }

            case 'CHALLENGE_RACE': {
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const { vin, opponentId, betAmount, isPinkSlip } = data;
                const challenger = players.get(currentPlayerId);
                const car = cars.get(vin);
                if (!car || car.ownerId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                }
                if (typeof opponentId !== 'string' || opponentId === currentPlayerId || !players.has(opponentId)) {
                    return send(ws, { event: 'ERROR', message: 'Raqib topilmadi yoki o\'zingiz.' });
                }
                if (isPinkSlip && !PINK_SLIP_ENABLED) {
                    return send(ws, { event: 'ERROR', message: 'Kalitga-kalit poygasi hozircha o\'chirilgan.' });
                }
                const bet = isPinkSlip ? 0 : Number(betAmount);
                if (!isPinkSlip && (!Number.isSafeInteger(bet) || bet < 0 || bet > 1000000)) {
                    return send(ws, { event: 'ERROR', message: 'Garov miqdori noto\'g\'ri.' });
                }
                if (carIsBusy(vin)) {
                    return send(ws, { event: 'ERROR', message: 'Mashina auksionda yoki boshqa poygada.' });
                }
                const result = raceManager.createRaceChallenge(challenger, car, opponentId, bet, Boolean(isPinkSlip));
                if (!result.success) {
                    return send(ws, { event: 'ERROR', message: result.message });
                }
                result.challenge.createdAt = Date.now();
                broadcastToPlayers([currentPlayerId, opponentId], { event: 'RACE_CREATED', race: result.challenge });
                break;
            }

            case 'ACCEPT_RACE': {
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const { raceId, vin } = data;
                const race = raceManager.activeChallenges.get(raceId);
                if (!race) {
                    return send(ws, { event: 'ERROR', message: 'Poyga topilmadi.' });
                }
                const opponent = players.get(currentPlayerId);
                const challenger = players.get(race.challengerId);
                const car = cars.get(vin);
                if (!car || car.ownerId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                }
                if (carIsBusy(vin)) {
                    return send(ws, { event: 'ERROR', message: 'Mashina auksionda yoki boshqa poygada.' });
                }
                // The challenger's bet was not escrowed at creation, so check it now
                if (!race.isPinkSlip && (!challenger || challenger.balance < race.betAmount)) {
                    return send(ws, { event: 'ERROR', message: 'Chaqiruvchining garov uchun mablag\'i yetarli emas.' });
                }
                const result = raceManager.acceptRaceChallenge(raceId, opponent, car, challenger);
                if (!result.success) {
                    return send(ws, { event: 'ERROR', message: result.message });
                }
                broadcastToPlayers([race.challengerId, currentPlayerId], {
                    event: 'RACE_STARTED',
                    raceId,
                    startTime: result.race.startTime,
                    challengerVin: result.race.challengerVin,
                    opponentVin: result.race.opponentVin
                });
                break;
            }

            case 'SUBMIT_RACE_TIME':
            case 'SUBMIT_DRAG_TIME': { // SUBMIT_RACE_TIME is the public name; the old one still works
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const { raceId, finishSeconds } = data;
                const race = raceManager.activeChallenges.get(raceId);
                if (!race || race.status !== 'IN_PROGRESS') {
                    return send(ws, { event: 'ERROR', message: 'Faol poyga topilmadi.' });
                }
                if (race.challengerId !== currentPlayerId && race.opponentId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu poyga ishtirokchisi emassiz.' });
                }
                if (Date.now() < race.startTime) {
                    return send(ws, { event: 'ERROR', message: 'Poyga hali boshlanmagan.' });
                }
                if (race.results[currentPlayerId] !== undefined) {
                    return send(ws, { event: 'ERROR', message: 'Vaqt allaqachon yuborilgan.' });
                }
                if (!Number.isFinite(finishSeconds)) {
                    return send(ws, { event: 'ERROR', message: 'Vaqt noto\'g\'ri.' });
                }
                const result = raceManager.submitDragTime(raceId, currentPlayerId, finishSeconds, players, cars);
                if (result.error) {
                    return send(ws, { event: 'ERROR', message: result.error });
                }
                if (result.completed) {
                    broadcastToPlayers([race.challengerId, race.opponentId], { event: 'RACE_FINISHED', ...result });
                    return;
                }
                send(ws, { event: 'RACE_TIME_RECORDED', raceId, waitingForOpponent: true });
                break;
            }

            case 'BUY_VIP_PLATE':
            case 'PURCHASE_VIP_PLATE': { // legacy name kept for existing clients
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const player = players.get(currentPlayerId);
                const result = dealership.purchaseVipPlate(player, data.plateCode);
                if (!result.success) {
                    return send(ws, { event: 'ERROR', message: result.message });
                }
                send(ws, { event: 'VIP_PLATE_PURCHASED', plate: result.plate, vipCoins: player.vipCoins });
                break;
            }

            case 'REPORT_ACCIDENT': {
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const { vin, impactSpeedKmh } = data;
                const car = cars.get(vin);
                if (!car || car.ownerId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                }
                if (!Number.isFinite(impactSpeedKmh) || impactSpeedKmh < 18 || impactSpeedKmh > 400) {
                    return send(ws, { event: 'ERROR', message: 'Avariya tezligi noto\'g\'ri.' });
                }
                // One crash can produce several contact events; accept one report per 2 seconds per car
                if (Date.now() - car.lastAccidentAt < 2000) {
                    return send(ws, { event: 'ERROR', message: 'Bu avariya allaqachon qayd etilgan.' });
                }
                car.lastAccidentAt = Date.now();
                dealership.recordVehicleAccident(car, impactSpeedKmh);
                car.lastAccidentSpeedKmh = Math.round(impactSpeedKmh);
                send(ws, {
                    event: 'ACCIDENT_RECORDED',
                    vin,
                    impactSpeedKmh: Math.round(impactSpeedKmh),
                    accidentsCount: car.accidentsCount,
                    conditionPercent: car.conditionPercent
                });
                break;
            }

            case 'REPAIR_CAR':
            case 'REPAIR_VEHICLE': { // legacy name kept for existing clients
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const car = cars.get(data.vin);
                if (!car || car.ownerId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                }
                if (carIsBusy(car.vin)) {
                    return send(ws, { event: 'ERROR', message: 'Auksiondagi yoki poygadagi mashinani ta\'mirlab bo\'lmaydi.' });
                }
                const result = dealership.repairVehicle(players.get(currentPlayerId), car);
                if (!result.success) {
                    return send(ws, { event: 'ERROR', message: result.message });
                }
                send(ws, { event: 'REPAIR_DONE', vin: car.vin, ...result });
                break;
            }

            case 'JOIN_ROOM': {
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const { roomId, vin } = data;
                if (typeof roomId !== 'string' || !ROOM_ID_RE.test(roomId)) {
                    return send(ws, { event: 'ERROR', message: 'Noto\'g\'ri xona ID.' });
                }
                const car = cars.get(vin);
                if (!car || car.ownerId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                }

                if (ws.roomId && ws.roomId !== roomId) {
                    lobby.removePlayer(ws.roomId, currentPlayerId);
                    broadcastToRoom(ws.roomId, { event: 'PLAYER_LEFT', roomId: ws.roomId, playerId: currentPlayerId });
                }

                // The VIN is taken from the server record, never trusted from the client
                const roomPlayers = lobby.joinRoom(roomId, currentPlayerId, { vin: car.vin });
                ws.roomId = roomId;
                send(ws, { event: 'ROOM_STATE', roomId, players: roomPlayers });
                broadcastToRoom(roomId, { event: 'PLAYER_JOINED', roomId, playerId: currentPlayerId, vin: car.vin });
                break;
            }

            case 'VERIFY_TUNING': {
                if (!currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                }
                const { vin, tuning } = data;
                const car = cars.get(vin);
                if (!car || car.ownerId !== currentPlayerId) {
                    return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                }
                const fields = ['horsepower', 'camberFront', 'camberRear', 'rideHeight'];
                if (!tuning || !fields.every(f => Number.isFinite(tuning[f]))) {
                    return send(ws, { event: 'ERROR', message: 'Tuning ma\'lumoti noto\'g\'ri.' });
                }
                const result = lobby.validateTuningAntiCheat(tuning);
                if (!result.valid) {
                    return send(ws, { event: 'ERROR', message: result.reason });
                }
                car.tuning = {
                    horsepower: tuning.horsepower,
                    camberFront: tuning.camberFront,
                    camberRear: tuning.camberRear,
                    rideHeight: tuning.rideHeight
                };
                send(ws, { event: 'TUNING_VERIFIED', vin, tuning: car.tuning });
                break;
            }

            case 'SYNC_TRANSFORM': {
                if (!currentPlayerId || !ws.roomId) {
                    return send(ws, { event: 'ERROR', message: 'Avval xonaga qo\'shiling (JOIN_ROOM).' });
                }
                const { vin, px, py, pz, ry, rpm, hood } = data;
                if (![px, py, pz, ry, rpm].every(Number.isFinite)) {
                    return send(ws, { event: 'ERROR', message: 'Koordinata noto\'g\'ri.' });
                }
                const current = lobby.rooms.get(ws.roomId)?.get(currentPlayerId);
                if (!current || current.vin !== vin) {
                    return send(ws, { event: 'ERROR', message: 'Bu mashina sizniki emas.' });
                }

                const result = lobby.updatePlayerTransform(ws.roomId, currentPlayerId, { px, py, pz, ry, rpm, hood });
                if (!result) {
                    return send(ws, { event: 'ERROR', message: 'Xonada emassiz.' });
                }
                if (result.cheated) {
                    const roomId = ws.roomId;
                    lobby.removePlayer(roomId, currentPlayerId);
                    // Tell the cheater too: they are still a socket in the room until roomId is cleared
                    broadcastToRoom(roomId, { event: 'PLAYER_LEFT', roomId, playerId: currentPlayerId, reason: 'anti-cheat' });
                    ws.roomId = null;
                    send(ws, { event: 'ERROR', message: result.reason });
                    break;
                }
                broadcastToRoom(ws.roomId, { event: 'TRANSFORM_UPDATED', playerId: currentPlayerId, state: result.state });
                break;
            }

            case 'VOICE_STREAM': {
                if (!currentPlayerId || !ws.roomId) {
                    return send(ws, { event: 'ERROR', message: 'Ovoz uchun xonaga qo\'shiling (JOIN_ROOM).' });
                }
                const { pcm } = data;
                if (typeof pcm !== 'string' || pcm.length === 0 || pcm.length > 8000) {
                    return send(ws, { event: 'ERROR', message: 'Ovoz paketi noto\'g\'ri.' });
                }
                // At most 15 packets per second per connection
                const now = Date.now();
                if (now - ws.voiceWindowStart >= 1000) {
                    ws.voiceWindowStart = now;
                    ws.voiceCount = 0;
                }
                if (++ws.voiceCount > 15) {
                    return send(ws, { event: 'ERROR', message: 'Ovoz juda tez yuborilmoqda.' });
                }
                const room = lobby.rooms.get(ws.roomId);
                const me = room?.get(currentPlayerId);
                if (!me) {
                    return send(ws, { event: 'ERROR', message: 'Xonada emassiz.' });
                }
                // Hearing range uses the server's positions, never the sender's claim
                const listeners = [...room.values()]
                    .filter(p => p.playerId !== currentPlayerId && Math.hypot(p.px - me.px, p.pz - me.pz) <= VOICE_RANGE_M)
                    .map(p => p.playerId);
                broadcastToPlayers(listeners, { event: 'VOICE_PACKET', from: currentPlayerId, pcm });
                break;
            }
        }
    }

    ws.on('message', (rawMessage) => {
        let data;
        try {
            data = JSON.parse(rawMessage);
        } catch {
            return send(ws, { event: 'ERROR', message: 'Noto\'g\'ri so\'rov formati' });
        }
        runCommand(String(data.action || 'UNKNOWN'), ws, () => {
            try {
                dispatch(data);
            } catch {
                send(ws, { event: 'ERROR', message: 'Noto\'g\'ri so\'rov formati' });
            }
        });
    });

    ws.on('close', () => {
        if (!ws.roomId || !currentPlayerId) return;
        runCommand('DISCONNECT', null, () => {
            const roomId = ws.roomId;
            lobby.removePlayer(roomId, currentPlayerId);
            broadcastToRoom(roomId, { event: 'PLAYER_LEFT', roomId, playerId: currentPlayerId });
            ws.roomId = null;
        });
    });
});

ready.then(() => {
    // Race housekeeping and auction timers run through the same queue as player commands
    setInterval(() => runCommand('RACE_EXPIRY', null, expireRaces), 5000);
    setInterval(() => runCommand('AUCTION_TICK', null, () => {
        const now = Date.now();
        for (const [vin, auction] of activeAuctions.entries()) {
            if (now >= auction.endTime) finalizeAuction(vin);
        }
    }), 1000);
    console.log(`[NextGenDrive] Live Auction & Economy Server running on ws://localhost:${PORT} (PostgreSQL)`);
});
