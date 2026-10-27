using UnityEngine;
using CarParking.Vehicle;
using CarParking.Core;

namespace CarParking.Core
{
    /// <summary>
    /// OBD Scanner, ECU flashing interface, bank loans, insurance policies,
    /// and garage/property ownership economy.
    /// </summary>
    public class OBDScannerAndEconomySystem : MonoBehaviour
    {
        public static OBDScannerAndEconomySystem Instance { get; private set; }

        [Header("Bank")]
        public float bankBalance;
        public float loanPrincipal;
        public float loanInterestRate = 0.12f;
        public float maxLoanAmount = 50000f;

        [Header("Insurance")]
        public bool hasInsurance;
        public float insurancePremiumPerDay = 80f;
        public float insuranceCoverage = 0.7f; // 70% of repair cost covered
        public float insuranceExpiryTime;

        [Header("Property")]
        public bool ownsGarage;
        public bool ownsDealershipLot;
        public float garageValue = 25000f;
        public float dealershipLotValue = 120000f;

        [Header("ECU")]
        public int ecuMapSlot = 1;
        public float customBoostLimit = 14f;
        public float customRevLimit = 7200f;
        public float customFuelMap = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (WorldServicesAndEconomySystem.Instance != null)
                bankBalance = WorldServicesAndEconomySystem.Instance.playerCash * 0.5f;
        }

        private void Update()
        {
            // Hotkeys for demo
            if (Input.GetKeyDown(KeyCode.F11))
                OpenOBDInterface();

            // Daily insurance premium (simplified — every 5 minutes real time as "day")
            if (hasInsurance && Time.time > insuranceExpiryTime)
            {
                hasInsurance = false;
                Debug.Log("[Insurance] Policy expired");
            }
        }

        // ---------- OBD / ECU ----------

        public void OpenOBDInterface()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle == null) return;

            var dt = vehicle.GetComponent<DrivetrainSystem>();
            var damage = vehicle.GetComponent<VehicleDamageAndDeformation>();
            var mech = vehicle.GetComponent<AdvancedMechanicalFailures>();

            Debug.Log("=== OBD SCANNER ===");
            if (dt != null)
            {
                Debug.Log($"RPM: {dt.currentRpm:F0} | Gear: {dt.currentGear} | Torque: {dt.maxTorque:F0} Nm");
                Debug.Log($"Turbo Boost Limit: {dt.turboMaxBoost:F2}");
            }
            if (damage != null)
            {
                Debug.Log($"Radiator: {damage.radiatorHealth * 100f:F0}% | Engine: {damage.engineHealth * 100f:F0}%");
            }
            if (mech != null)
            {
                Debug.Log($"Engine Block: {mech.engineBlockHealth * 100f:F0}% | Axle: {mech.axleHealth * 100f:F0}% | Fuel Tank: {mech.fuelTankHealth * 100f:F0}%");
                Debug.Log($"Tires Pressure: [{mech.tirePressure[0]:F2}, {mech.tirePressure[1]:F2}, {mech.tirePressure[2]:F2}, {mech.tirePressure[3]:F2}]");
                Debug.Log($"Leaking: {mech.isLeaking} | On Fire: {mech.isOnFire}");
            }
            Debug.Log($"ECU Map Slot: {ecuMapSlot} | Boost Limit: {customBoostLimit} PSI | Rev Limit: {customRevLimit}");
        }

        public void FlashECU(int mapSlot, float boostPsi, float revLimit, float fuelMap)
        {
            ecuMapSlot = mapSlot;
            customBoostLimit = boostPsi;
            customRevLimit = revLimit;
            customFuelMap = fuelMap;

            var vehicles = FindObjectsByType<DrivetrainSystem>(FindObjectsSortMode.None);
            foreach (var dt in vehicles)
            {
                dt.turboMaxBoost = 1f + (boostPsi / 14.7f) * 0.65f;
                dt.maxRpm = revLimit;
                dt.maxTorque *= fuelMap;
            }

            Debug.Log($"[ECU] Flashed map {mapSlot}: {boostPsi} PSI, {revLimit} RPM, fuel x{fuelMap}");
        }

        // ---------- Banking ----------

        public bool TakeLoan(float amount)
        {
            if (amount <= 0f || amount > maxLoanAmount) return false;
            if (loanPrincipal > 0f) return false; // one loan at a time

            loanPrincipal = amount;
            if (WorldServicesAndEconomySystem.Instance != null)
                WorldServicesAndEconomySystem.Instance.playerCash += amount;

            bankBalance += amount;
            Debug.Log($"[Bank] Loan approved: ${amount:N0} at {loanInterestRate * 100f:F0}% APR");
            return true;
        }

        public bool RepayLoan(float amount)
        {
            if (loanPrincipal <= 0f) return false;
            float repay = Mathf.Min(amount, loanPrincipal);

            if (WorldServicesAndEconomySystem.Instance != null)
            {
                if (WorldServicesAndEconomySystem.Instance.playerCash < repay) return false;
                WorldServicesAndEconomySystem.Instance.playerCash -= repay;
            }

            loanPrincipal -= repay;
            if (loanPrincipal < 1f) loanPrincipal = 0f;
            Debug.Log($"[Bank] Repaid ${repay:N0}. Remaining principal: ${loanPrincipal:N0}");
            return true;
        }

        // ---------- Insurance ----------

        public bool BuyInsurance(float durationDays)
        {
            float cost = insurancePremiumPerDay * durationDays;
            if (WorldServicesAndEconomySystem.Instance == null) return false;
            if (WorldServicesAndEconomySystem.Instance.playerCash < cost) return false;

            WorldServicesAndEconomySystem.Instance.playerCash -= cost;
            hasInsurance = true;
            insuranceExpiryTime = Time.time + durationDays * 300f; // 5 min = 1 day simplified
            Debug.Log($"[Insurance] Policy active for {durationDays} days. Cost ${cost:N0}");
            return true;
        }

        public float ClaimInsurance(float repairCost)
        {
            if (!hasInsurance) return 0f;
            float covered = repairCost * insuranceCoverage;
            if (WorldServicesAndEconomySystem.Instance != null)
                WorldServicesAndEconomySystem.Instance.playerCash += covered;
            Debug.Log($"[Insurance] Claim paid: ${covered:N0}");
            return covered;
        }

        // ---------- Property ----------

        public bool BuyGarage()
        {
            if (ownsGarage) return false;
            if (WorldServicesAndEconomySystem.Instance == null) return false;
            if (WorldServicesAndEconomySystem.Instance.playerCash < garageValue) return false;

            WorldServicesAndEconomySystem.Instance.playerCash -= garageValue;
            ownsGarage = true;
            Debug.Log("[Property] Garage purchased");
            return true;
        }

        public bool BuyDealershipLot()
        {
            if (ownsDealershipLot) return false;
            if (WorldServicesAndEconomySystem.Instance == null) return false;
            if (WorldServicesAndEconomySystem.Instance.playerCash < dealershipLotValue) return false;

            WorldServicesAndEconomySystem.Instance.playerCash -= dealershipLotValue;
            ownsDealershipLot = true;
            Debug.Log("[Property] Dealership lot purchased");
            return true;
        }

        public void ApplyRepairWithInsurance(float fullCost)
        {
            float playerPays = fullCost;
            if (hasInsurance)
            {
                float covered = ClaimInsurance(fullCost);
                playerPays = fullCost - covered;
            }

            if (WorldServicesAndEconomySystem.Instance != null &&
                WorldServicesAndEconomySystem.Instance.playerCash >= playerPays)
            {
                WorldServicesAndEconomySystem.Instance.playerCash -= playerPays;

                var mech = FindFirstObjectByType<AdvancedMechanicalFailures>();
                if (mech != null) mech.RepairAllMechanical();

                var damage = FindFirstObjectByType<VehicleDamageAndDeformation>();
                if (damage != null) damage.FullRepair();

                Debug.Log($"[Repair] Done. Player paid ${playerPays:N0}");
            }
        }
    }
}
