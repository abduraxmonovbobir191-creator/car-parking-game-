const WebSocket = require('ws');
const { v4: uuidv4 } = require('uuid');

const PORT = process.env.PORT || 8080;
const wss = new WebSocket.Server({ port: PORT });

// Server-Authoritative Memory State (Replaceable with PostgreSQL + Redis)
const players = new Map();
const cars = new Map();
const activeAuctions = new Map();

const MARKET_TAX_RATE = 0.05; // 5% commission to prevent inflation
const ANTI_SNIPE_WINDOW_MS = 10000; // Last 10 seconds
const ANTI_SNIPE_EXTEND_MS = 15000; // Extend by 15 seconds

function broadcast(payload) {
    const message = JSON.stringify(payload);
    wss.clients.forEach(client => {
        if (client.readyState === WebSocket.OPEN) {
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
        customParts: ["Stage 2 Turbo", "Forged Carbon Hood"]
    };
    cars.set(vin, car);
    return car;
}

wss.on('connection', (ws) => {
    let currentPlayerId = null;

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
                            escrowLocked: 0
                        });
                        // Grant starter car
                        createSampleCar(currentPlayerId, "BMW M5 CS (F90)", 635);
                    }
                    ws.send(JSON.stringify({
                        event: 'AUTH_SUCCESS',
                        player: players.get(currentPlayerId),
                        activeAuctions: Array.from(activeAuctions.values())
                    }));
                    break;
                }

                case 'CREATE_AUCTION': {
                    const { vin, startingPrice, buyoutPrice, durationSeconds } = data;
                    const car = cars.get(vin);

                    if (!car || car.ownerId !== currentPlayerId) {
                        return ws.send(JSON.stringify({ event: 'ERROR', message: 'Siz bu mashina egasi emassiz!' }));
                    }
                    if (activeAuctions.has(vin)) {
                        return ws.send(JSON.stringify({ event: 'ERROR', message: 'Mashina allaqachon auksionda!' }));
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
                        return ws.send(JSON.stringify({ event: 'ERROR', message: 'Auksion topilmadi yoki tugagan.' }));
                    }
                    if (auction.sellerId === currentPlayerId) {
                        return ws.send(JSON.stringify({ event: 'ERROR', message: 'O\'z mashinangizga stavka qo\'ya olmaysiz.' }));
                    }
                    if (bidAmount <= auction.currentBid) {
                        return ws.send(JSON.stringify({ event: 'ERROR', message: 'Stavka joriy narxdan baland bo\'lishi shart!' }));
                    }
                    if (!bidder || (bidder.balance - bidder.escrowLocked) < bidAmount) {
                        return ws.send(JSON.stringify({ event: 'ERROR', message: 'Balansingizda yetarli mablag\' yo\'q!' }));
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
            }
        } catch (err) {
            ws.send(JSON.stringify({ event: 'ERROR', message: 'Noto\'g\'ri so\'rov formati' }));
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
