#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CarParking.Mechanic;
using CarParking.Campaign;
using CarParking.World;
using CarParking.Racing;

namespace CarParking.EditorTools
{
    /// <summary>
    /// MODULE 50 — OneClick integration for Modules 31–49 systems.
    /// Extends the main builder with mechanic, campaign, racing and world event hooks.
    /// </summary>
    public static class OneClickModules50Extension
    {
        [MenuItem("Tools/Ultra Car Simulator/2. Wire Modules 31-49 Systems")]
        public static void WireAdvancedSystems()
        {
            var root = GameObject.Find("--- ADVANCED SYSTEMS ---");
            if (root == null)
                root = new GameObject("--- ADVANCED SYSTEMS ---");

            Ensure<EngineBlockDisassemblySystem>(root, "EngineBlockDisassembly");
            Ensure<ForcedInductionAndExhaustMechanic>(root, "ForcedInduction");
            Ensure<TransmissionAndDrivelineMechanic>(root, "TransmissionDriveline");
            Ensure<SuspensionAndBrakeHydraulicsMechanic>(root, "SuspensionBrakes");
            Ensure<InteractiveGarageLiftAndDynoRoom>(root, "GarageLiftDyno");
            Ensure<BarnFindRestorationSystem>(root, "BarnFindRestoration");

            Ensure<StoryCampaignEngine>(root, "StoryCampaign");
            Ensure<RivalCrewsAndTerritorySystem>(root, "RivalCrews");

            Ensure<DynamicRoadsideEventsSystem>(root, "RoadsideEvents");

            Ensure<MotorsportChampionshipManager>(root, "MotorsportChampionship");
            Ensure<RallyCoDriverPacenoteSystem>(root, "RallyPacenotes");

            Debug.Log("[OneClick] Modules 31-49 systems wired.");
            EditorUtility.DisplayDialog("Ultra Car Simulator", "Modules 31-49 systems added to scene.", "OK");
        }

        private static void Ensure<T>(GameObject parent, string name) where T : Component
        {
            if (Object.FindFirstObjectByType<T>() != null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.AddComponent<T>();
        }
    }
}
#endif
