const WebSocket = require('ws');
const { v4: uuidv4 } = require('uuid');
const { LobbyManager } = require('./lobby_sync');
const { DealershipAndMonetizationManager } = require('./monetization_dealership');

const PORT = process.env.PORT || 8080;
const wss = new WebSocket.Server({ port: PORT, maxPayload: 16 * 1024 });

// Server-Authoritative Memory State (Replaceable with PostgreSQL + Redis)
const players = new Map();
const cars = new Map();
const activeAuctions = new Map();
const lobby = new LobbyManager();
const dealership = new DealershipAndMonetizationManager();

const MARKET_TAX_RATE = 0.05; // 5% commission to prevent inflation
const ANTI_SNIPE_WINDOW_MS = 10000; // Last 10 seconds
const ANTI_SNIPE_EXTEND_MS = 15000; // Extend by 15 seconds
const ROOM_ID_RE = /^[A-Za-z0-9_-]{1,32}$/;

function send(ws, payload) {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(payload));
    }
}

function broadcast(payload) {
    const message = JSON.stringify(payload);
    wss.clients.forEach(client => {
        if (client.readyState === WebSocket.OPEN) {
            client.send(message);
        }
    });
}

function broadcastToRoom(roomId, payload) {
    const message = JSON.stringify(payload);
    wss.clients.forEach(client => {
        if (client.readyState === WebSocket.OPEN && client.roomId === roomId) {
            client.send(message);
        }
    });
}

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

wss.on('connection', (ws) => {
    let currentPlayerId = null;
    ws.roomId = null;

    ws.on('message', (rawMessage) => {
        try {
            const data = JSON.parse(rawMessage);

            switch (data.action) {
                case 'AUTH': {
                    currentPlayerId = data.playerId || `USER-${uuidv4().slice(0, 5)}`;
                    if (!players.has(currentPlayerId)) {
                        players.set(currentPlayerId, {
                            id: currentPlayerId,
                            username: data.username || 'Racer',
                            balance: 150000, // Starting cash
                            escrowLocked: 0,
                            vipCoins: 1200 // Starting VIP coins for every new player
                        });
                        // Grant starter car
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
                    if (activeAuctions.has(vin)) {
                        return send(ws, { event: 'ERROR', message: 'Mashina allaqachon auksionda!' });
                    }

                    const durationMs = Math.min(Math.max(durationSeconds || 60, 30), 3600) * 1000;
                    const auction = {
                        auctionId: uuidv4(),
                        vin,
                        carDetails: car,
                        sellerId: currentPlayerId,
                        currentBid: startingPrice,
                        buyoutPrice: buyoutPrice || startingPrice * 3,
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
                    if (auction.sellerId === currentPlayerId) {
                        return send(ws, { event: 'ERROR', message: 'O\'z mashinangizga stavka qo\'ya olmaysiz.' });
                    }
                    if (bidAmount <= auction.currentBid) {
                        return send(ws, { event: 'ERROR', message: 'Stavka joriy narxdan baland bo\'lishi shart!' });
                    }
                    if (!bidder || (bidder.balance - bidder.escrowLocked) < bidAmount) {
                        return send(ws, { event: 'ERROR', message: 'Balansingizda yetarli mablag\' yo\'q!' });
                    }

                    // Unlock previous highest bidder's money
                    if (auction.highestBidderId) {
                        const prevBidder = players.get(auction.highestBidderId);
                        if (prevBidder) prevBidder.escrowLocked -= auction.currentBid;
                    }

                    // Lock new bidder's funds (Anti-Cheat Escrow)
                    bidder.escrowLocked += bidAmount;
                    auction.currentBid = bidAmount;
                    auction.highestBidderId = currentPlayerId;

                    // Anti-sniping: extend timer if bid placed in last 10 seconds
                    const timeRemaining = auction.endTime - Date.now();
                    if (timeRemaining < ANTI_SNIPE_WINDOW_MS) {
                        auction.endTime += ANTI_SNIPE_EXTEND_MS;
                    }

                    broadcast({
                        event: 'BID_UPDATED',
                        vin,
                        currentBid: auction.currentBid,
                        highestBidderId: currentPlayerId,
                        endTime: auction.endTime
                    });

                    // Instant Buyout Check
                    if (bidAmount >= auction.buyoutPrice) {
                        finalizeAuction(vin);
                    }
                    break;
                }

                case 'BUY_VIP_PLATE':
                case 'PURCHASE_VIP_PLATE': {  // legacy name kept for existing clients
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
                case 'REPAIR_VEHICLE': {  // legacy name kept for existing clients
                    if (!currentPlayerId) {
                        return send(ws, { event: 'ERROR', message: 'Avval tizimga kiring (AUTH).' });
                    }
                    const car = cars.get(data.vin);
                    if (!car || car.ownerId !== currentPlayerId) {
                        return send(ws, { event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' });
                    }
                    if (activeAuctions.has(car.vin)) {
                        return send(ws, { event: 'ERROR', message: 'Auksiondagi mashinani ta\'mirlab bo\'lmaydi.' });
                    }
                    const player = players.get(currentPlayerId);
                    const result = dealership.repairVehicle(player, car);
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
                    const players_ = lobby.joinRoom(roomId, currentPlayerId, { vin: car.vin });
                    ws.roomId = roomId;
                    send(ws, { event: 'ROOM_STATE', roomId, players: players_ });
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
                        // Notify the cheater too: they are still a socket in the room until roomId is cleared
                        broadcastToRoom(roomId, { event: 'PLAYER_LEFT', roomId, playerId: currentPlayerId, reason: 'anti-cheat' });
                        ws.roomId = null;
                        send(ws, { event: 'ERROR', message: result.reason });
                        break;
                    }
                    broadcastToRoom(ws.roomId, { event: 'TRANSFORM_UPDATED', playerId: currentPlayerId, state: result.state });
                    break;
                }
            }
        } catch (err) {
            send(ws, { event: 'ERROR', message: 'Noto\'g\'ri so\'rov formati' });
        }
    });

    ws.on('close', () => {
        if (ws.roomId && currentPlayerId) {
            const roomId = ws.roomId;
            lobby.removePlayer(roomId, currentPlayerId);
            broadcastToRoom(roomId, { event: 'PLAYER_LEFT', roomId, playerId: currentPlayerId });
            ws.roomId = null;
        }
    });
});

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
            const sellerPayout = auction.currentBid - tax;
            seller.balance += sellerPayout;

            // Transfer Ownership & Update Carfax History
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

// Tick loop to check expired auctions every second
setInterval(() => {
    const now = Date.now();
    for (const [vin, auction] of activeAuctions.entries()) {
        if (now >= auction.endTime) {
            finalizeAuction(vin);
        }
    }
}, 1000);

console.log(`[NextGenDrive] Live Auction & Economy Server running on ws://localhost:${PORT}`);
