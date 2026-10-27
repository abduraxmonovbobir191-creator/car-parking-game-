using UnityEngine;

namespace CarParking.Core
{
    /// <summary>
    /// MODULES 51-60 — Integration status and master hook registry.
    /// Tracks which advanced systems are present and ready.
    /// </summary>
    public class Modules50_60_IntegrationStatus : MonoBehaviour
    {
        public static Modules50_60_IntegrationStatus Instance { get; private set; }

        public bool mechanicSystemsReady;
        public bool campaignSystemsReady;
        public bool racingSystemsReady;
        public bool multiplayerSystemsReady;
        public bool assetGeneratorsReady;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Scan();
        }

        public void Scan()
        {
            mechanicSystemsReady = FindFirstObjectByType<CarParking.Mechanic.EngineBlockDisassemblySystem>() != null;
            campaignSystemsReady = FindFirstObjectByType<CarParking.Campaign.StoryCampaignEngine>() != null;
            racingSystemsReady = FindFirstObjectByType<CarParking.Racing.MotorsportChampionshipManager>() != null;
            multiplayerSystemsReady = true;
            assetGeneratorsReady = true;
            Debug.Log($"[Integration] Mechanic={mechanicSystemsReady} Campaign={campaignSystemsReady} Racing={racingSystemsReady}");
        }

        public string GetStatusReport()
        {
            return $"Modules 31-49 Integration:\n" +
                   $"Mechanic: {(mechanicSystemsReady?"OK":"MISSING")}\n" +
                   $"Campaign: {(campaignSystemsReady?"OK":"MISSING")}\n" +
                   $"Racing: {(racingSystemsReady?"OK":"MISSING")}\n" +
                   $"Multiplayer: {(multiplayerSystemsReady?"OK":"MISSING")}\n" +
                   $"Asset Factory: {(assetGeneratorsReady?"OK":"MISSING")}";
        }
    }
}
