const MAX_LEGIT_HORSEPOWER = 1850; // Anything above this is flagged as a cheat/hack
const MAX_SPEED_KMH = 460;

class LobbyManager {
    constructor() {
        this.rooms = new Map(); // roomId -> Map of player states
    }

    joinRoom(roomId, playerId, initialData) {
        if (!this.rooms.has(roomId)) {
            this.rooms.set(roomId, new Map());
        }
        const room = this.rooms.get(roomId);
        room.set(playerId, {
            playerId,
            vin: initialData.vin,
            px: 0, py: 0, pz: 0,
            ry: 0,
            rpm: 900,
            hoodOpen: false,
            hasReported: false,
            lastUpdated: Date.now()
        });
        return Array.from(room.values());
    }

    validateTuningAntiCheat(carTuningData) {
        const { horsepower, camberFront, camberRear, rideHeight } = carTuningData;

        if (horsepower > MAX_LEGIT_HORSEPOWER || horsepower < 50) {
            return { valid: false, reason: `Anti-Cheat: Noqonuniy ot kuchi (${horsepower} HP) aniqlandi!` };
        }
        if (Math.abs(camberFront) > 15 || Math.abs(camberRear) > 18) {
            return { valid: false, reason: 'Anti-Cheat: G\'ildirak razvali chegaradan chiqib ketgan!' };
        }
        if (rideHeight < 0.05 || rideHeight > 0.50) {
            return { valid: false, reason: 'Anti-Cheat: Podveska balandligi buzilgan!' };
        }
        return { valid: true };
    }

    updatePlayerTransform(roomId, playerId, transformData) {
        const room = this.rooms.get(roomId);
        if (!room || !room.has(playerId)) return null;

        const state = room.get(playerId);
        const now = Date.now();
        const dtSeconds = Math.max((now - state.lastUpdated) / 1000, 0.016);

        // Speedhack / Teleport Anti-Cheat Check (the first report only sets the spawn point)
        if (state.hasReported) {
            const dx = transformData.px - state.px;
            const dz = transformData.pz - state.pz;
            const distMeters = Math.sqrt(dx * dx + dz * dz);
            const speedKmh = (distMeters / dtSeconds) * 3.6;

            if (speedKmh > MAX_SPEED_KMH) {
                return { cheated: true, reason: 'Anti-Cheat: Speedhack yoki Teleport aniqlandi!' };
            }
        }
        state.hasReported = true;

        state.px = transformData.px;
        state.py = transformData.py;
        state.pz = transformData.pz;
        state.ry = transformData.ry;
        state.rpm = transformData.rpm;
        state.hoodOpen = Boolean(transformData.hood);
        state.lastUpdated = now;

        return { cheated: false, state };
    }

    removePlayer(roomId, playerId) {
        const room = this.rooms.get(roomId);
        if (!room) return;
        room.delete(playerId);
        if (room.size === 0) {
            this.rooms.delete(roomId);
        }
    }
}

module.exports = { LobbyManager };
