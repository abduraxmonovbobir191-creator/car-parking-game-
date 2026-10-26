class DealershipAndMonetizationManager {
    constructor() {
        // Rare VIP License Plates available in the state auction
        this.vipPlatesMarket = new Map([
            ["01 A 777 AA", { plate: "01 A 777 AA", tier: "PLATINUM", priceCoins: 500, ownerId: null }],
            ["01 001 VIP",  { plate: "01 001 VIP",  tier: "DIAMOND",  priceCoins: 1000, ownerId: null }],
            ["01 Z 888 ZZ", { plate: "01 Z 888 ZZ", tier: "GOLD",     priceCoins: 350, ownerId: null }],
            ["10 X 707 XX", { plate: "10 X 707 XX", tier: "GOLD",     priceCoins: 300, ownerId: null }]
        ]);

        // Player-owned VIP Showroom Podiums
        this.showroomStalls = new Map();
    }

    purchaseVipPlate(player, plateCode) {
        const item = this.vipPlatesMarket.get(plateCode);
        if (!item) {
            return { success: false, message: "Bunday davlat raqami topilmadi." };
        }
        if (item.ownerId) {
            return { success: false, message: "Bu raqam allaqachon boshqa o'yinchiga tegishli!" };
        }
        const playerCoins = player.vipCoins ?? 0;
        if (playerCoins < item.priceCoins) {
            return { success: false, message: "VIP Coin yetarli emas!" };
        }

        player.vipCoins = playerCoins - item.priceCoins;
        item.ownerId = player.id;
        return { success: true, plate: item };
    }

    recordVehicleAccident(car, impactSpeedKmh) {
        if (!car) return null;
        car.accidentsCount = (car.accidentsCount ?? 0) + 1;
        car.lastAccidentSpeedKmh = Math.round(impactSpeedKmh);
        car.conditionPercent = Math.max(10, (car.conditionPercent ?? 100) - Math.round(impactSpeedKmh * 0.25));
        return car;
    }

    repairVehicle(player, car) {
        if (!car) return { success: false, message: "Mashina topilmadi." };
        if (car.ownerId !== player.id) {
            return { success: false, message: "Siz bu mashina egasi emassiz!" };
        }
        if (!Number.isFinite(car.horsepower) || car.horsepower <= 0) {
            return { success: false, message: "Mashina ma'lumotlari noto'g'ri." };
        }

        const missingCondition = 100 - (car.conditionPercent ?? 100);
        if (missingCondition <= 0) {
            return { success: false, message: "Mashina allaqachon 100% soz holatda." };
        }

        // Repair cost scales with horsepower & damage, but Carfax accident history remains permanent!
        const repairCost = Math.floor(missingCondition * (car.horsepower * 0.8));
        if (player.balance < repairCost) {
            return { success: false, message: `Ta'mirlash uchun ${repairCost}$ kerak. Mablag' yetarli emas!` };
        }

        player.balance -= repairCost;
        car.conditionPercent = 100;
        return {
            success: true,
            repairCost,
            newBalance: player.balance,
            note: "Kuzov va motor 100% tiklandi, ammo VIN-koddagi avariya tarixi saqlanib qoldi."
        };
    }
}

module.exports = { DealershipAndMonetizationManager };
