const { v4: uuidv4 } = require('uuid');

const MIN_LEGIT_QUARTER_MILE_SEC = 6.2; // World-record threshold for Anti-Cheat

class StreetRaceWagerManager {
    constructor() {
        this.activeChallenges = new Map(); // raceId -> Challenge details
    }

    createRaceChallenge(challenger, challengerCar, opponentId, betAmount, isPinkSlip) {
        if (!challengerCar || challengerCar.ownerId !== challenger.id) {
            return { success: false, message: "Mashina sizga tegishli emas!" };
        }
        if (!isPinkSlip && challenger.balance < betAmount) {
            return { success: false, message: "Garov uchun balansingizda yetarli pul yo'q!" };
        }

        const raceId = `RACE-${uuidv4().slice(0, 6).toUpperCase()}`;
        const challenge = {
            raceId,
            challengerId: challenger.id,
            challengerVin: challengerCar.vin,
            opponentId,
            opponentVin: null,
            betAmount: isPinkSlip ? 0 : Math.max(0, betAmount),
            isPinkSlip: Boolean(isPinkSlip), // True = Winner takes loser's car!
            status: 'PENDING',
            results: {}
        };

        this.activeChallenges.set(raceId, challenge);
        return { success: true, challenge };
    }

    acceptRaceChallenge(raceId, opponent, opponentCar, challenger) {
        const race = this.activeChallenges.get(raceId);
        if (!race || race.status !== 'PENDING') {
            return { success: false, message: "Poyga chaqiruvi topilmadi yoki eskirgan." };
        }
        if (race.opponentId !== opponent.id) {
            return { success: false, message: "Bu chaqiruv sizga yuborilmagan." };
        }
        if (!opponentCar || opponentCar.ownerId !== opponent.id) {
            return { success: false, message: "Poyga uchun mashinangiz tasdiqlanmadi." };
        }
        if (!race.isPinkSlip && opponent.balance < race.betAmount) {
            return { success: false, message: "Garovni qabul qilish uchun pulingiz yetarli emas!" };
        }

        // Lock cash bets in escrow if not Pink Slip
        if (!race.isPinkSlip) {
            challenger.balance -= race.betAmount;
            opponent.balance -= race.betAmount;
        }

        race.opponentVin = opponentCar.vin;
        race.status = 'IN_PROGRESS';
        race.startTime = Date.now() + 5000; // 5-second countdown
        return { success: true, race };
    }

    submitDragTime(raceId, playerId, finishSeconds, playersMap, carsMap) {
        const race = this.activeChallenges.get(raceId);
        if (!race || race.status !== 'IN_PROGRESS') {
            return { completed: false, error: "Faol poyga topilmadi." };
        }

        // Anti-Cheat check for impossible 402m times
        if (finishSeconds < MIN_LEGIT_QUARTER_MILE_SEC) {
            return { completed: false, error: "Anti-Cheat: Soxta poyga vaqti aniqlandi!" };
        }

        race.results[playerId] = finishSeconds;

        const cTime = race.results[race.challengerId];
        const oTime = race.results[race.opponentId];

        if (cTime !== undefined && oTime !== undefined) {
            race.status = 'FINISHED';
            const challengerWon = cTime <= oTime;
            const winnerId = challengerWon ? race.challengerId : race.opponentId;
            const loserId = challengerWon ? race.opponentId : race.challengerId;

            const winner = playersMap.get(winnerId);
            const loserCarVin = challengerWon ? race.opponentVin : race.challengerVin;
            const loserCar = carsMap.get(loserCarVin);

            if (race.isPinkSlip && loserCar && winner) {
                // Transfer car ownership ("Kalitga-kalit")
                loserCar.ownerId = winner.id;
                loserCar.ownersHistory.push(winner.id);
            } else if (winner) {
                // Award cash pot (minus 5% street race commission)
                const totalPot = race.betAmount * 2;
                winner.balance += Math.floor(totalPot * 0.95);
            }

            this.activeChallenges.delete(raceId);
            return {
                completed: true,
                raceId,
                winnerId,
                loserId,
                isPinkSlip: race.isPinkSlip,
                transferredCarVin: race.isPinkSlip ? loserCarVin : null,
                times: race.results
            };
        }

        return { completed: false, waitingForOpponent: true };
    }
}

module.exports = { StreetRaceWagerManager };
