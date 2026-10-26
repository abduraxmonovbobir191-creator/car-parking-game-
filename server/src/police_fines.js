// Speed traps, pursuit heat and bribes. Every decision is made here, on the server.
// Clients only report what they observed; the zone limit, the fine table and the
// position check all live here, so a modified client cannot pick its own fine.

const SPEED_ZONES = {
    'ZONE-1': { x: 100, z: 50, radius: 15, limitKmh: 60 },
    'ZONE-2': { x: 400, z: -120, radius: 20, limitKmh: 80 }
};

// Fine by how far over the limit the car was (km/h), in the game currency
const FINE_TIERS = [
    { over: 20, amount: 300 },
    { over: 40, amount: 900 },
    { over: 60, amount: 2000 },
    { over: Infinity, amount: 5000 }
];

const HEAT_MAX = 5;
const HEAT_DECAY_MS = 10 * 60 * 1000;   // one heat point is forgotten every 10 minutes
const HEAT_MULTIPLIER_PER_POINT = 0.25; // each heat point adds 25% to the fine
const SPEED_TOLERANCE_KMH = 25;         // claimed speed may exceed the server's measurement by this much
const MAX_PLAUSIBLE_KMH = 460;

function currentHeat(player, now) {
    const decayed = Math.floor(Math.max(0, now - (player.heatAt ?? 0)) / HEAT_DECAY_MS);
    return Math.max(0, (player.heat ?? 0) - decayed);
}

function fineFor(overKmh) {
    return FINE_TIERS.find(t => overKmh <= t.over).amount;
}

// Usable cash: balance that is not locked in an auction or race escrow
function availableCash(player) {
    return Math.max(0, player.balance - (player.escrowLocked ?? 0));
}

/**
 * A speed-trap observation. The fine is applied to the player object; the caller persists it.
 * @param {object} args
 * @param {object} args.player      player record (balance, escrowLocked, heat, heatAt)
 * @param {object} args.position    lobby state: px, pz, speedKmh (server-measured)
 * @param {string} args.zoneId
 * @param {number} args.claimedKmh  speed the client says it had
 * @param {number} args.now         timestamp in ms
 */
function reportSpeedTrap({ player, position, zoneId, claimedKmh, now }) {
    const zone = SPEED_ZONES[zoneId];
    if (!zone) return { ok: false, message: 'Noma\'lum tezlik nazorati zonasi.' };
    if (!Number.isFinite(claimedKmh) || claimedKmh < 0 || claimedKmh > MAX_PLAUSIBLE_KMH) {
        return { ok: false, message: 'Tezlik noto\'g\'ri.' };
    }
    if (!position) return { ok: false, message: 'Xonada emassiz.' };
    if (Math.hypot(position.px - zone.x, position.pz - zone.z) > zone.radius) {
        return { ok: false, message: 'Siz tezlik nazorati zonasida emassiz.' };
    }
    if (position.speedKmh == null) {
        return { ok: false, message: 'Tezlikni tekshirish uchun avval harakatlaning.' };
    }
    if (claimedKmh > position.speedKmh + SPEED_TOLERANCE_KMH) {
        return { ok: false, message: 'Tezlik serverdagi o\'lchovga mos kelmadi.' };
    }

    const over = claimedKmh - zone.limitKmh;
    if (over <= 0) {
        return { ok: true, fined: false, amount: 0, heat: currentHeat(player, now) };
    }

    const heat = currentHeat(player, now);
    const base = fineFor(over);
    const amount = Math.min(Math.round(base * (1 + heat * HEAT_MULTIPLIER_PER_POINT)), availableCash(player));

    player.balance -= amount;
    player.heat = Math.min(HEAT_MAX, heat + 1);
    player.heatAt = now;
    return { ok: true, fined: true, amount, over: Math.round(over), heat: player.heat };
}

/**
 * Bribing an officer. Cost grows with heat; the outcome is random, so it is a risk,
 * not a way out. The cost is charged whether or not the bribe works.
 * @param {function} rng  returns a number in [0, 1); injected for tests
 */
function attemptBribe({ player, now, rng = Math.random }) {
    const heat = currentHeat(player, now);
    if (heat === 0) return { ok: false, message: 'Politsiya sizni ta\'qib qilmayapti, pora kerak emas.' };

    const cost = 500 * heat;
    if (availableCash(player) < cost) {
        return { ok: false, message: `Pora uchun ${cost}$ kerak. Mablag' yetarli emas.` };
    }

    const chance = heat <= 2 ? 0.6 : 0.3;
    const success = rng() < chance;
    player.balance -= cost;
    player.heat = success ? Math.max(0, heat - 2) : Math.min(HEAT_MAX, heat + 1);
    player.heatAt = now;
    return { ok: true, success, cost, heat: player.heat };
}

module.exports = { SPEED_ZONES, FINE_TIERS, HEAT_MAX, currentHeat, reportSpeedTrap, attemptBribe };
