#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CarParking.World;
using CarParking.Vehicle;
using CarParking.Character;
using CarParking.Multiplayer;
using CarParking.AI;
using CarParking.Audio;
using CarParking.Core;
using CarParking.UI;
using CarParking.TrackEditor;
using CarParking.VR;
using CarParking.CameraSystem;

namespace CarParking.EditorTools
{
    public static class OneClickWorldSceneBuilder
    {
        [MenuItem("Tools/Ultra Car Simulator/1. Build Complete Playable World & Vehicle Scene")]
        public static void BuildCompleteScene()
        {
            if (!EditorUtility.DisplayDialog("Ultra Car Simulator",
                "Build ALL systems (Modules 1-12) and playable vehicle?", "Build", "Cancel"))
                return;

            var worldRoot = CreateRoot("--- WORLD SYSTEMS ---");
            var vehicleRoot = CreateRoot("--- PLAYABLE VEHICLE ---");
            var aiRoot = CreateRoot("--- AI & MULTIPLAYER ---");
            var uiRoot = CreateRoot("--- UI & SERVICES ---");

            var floatingOrigin = CreateSystem<FloatingOriginSystem>(worldRoot, "FloatingOriginSystem");
            var terrainStreamer = CreateSystem<ProceduralTerrainChunkStreamer>(worldRoot, "ProceduralTerrainChunkStreamer");
            CreateSystem<TerrainDeformationManager>(worldRoot, "TerrainDeformationManager");
            CreateSystem<UrbanAndBridgeGenerator>(worldRoot, "UrbanAndBridgeGenerator");
            CreateSystem<ProceduralMegacityGenerator>(worldRoot, "ProceduralMegacityGenerator");
            CreateSystem<WaterAndWeatherSystem>(worldRoot, "WaterAndWeatherSystem");
            CreateSystem<WeatherDisasterSystem>(worldRoot, "WeatherDisasterSystem");
            CreateSystem<RealWorldWeatherAndSatelliteSync>(worldRoot, "RealWorldWeatherAndSatelliteSync");
            CreateSystem<AdvancedHydrodynamicsSystem>(worldRoot, "AdvancedHydrodynamicsSystem");
            CreateSystem<VolumetricLightingAndNightAtmosphere>(worldRoot, "VolumetricLightingAndNightAtmosphere");
            CreateSystem<WorldExplorationEasterEggs>(worldRoot, "WorldExplorationEasterEggs");

            CreateSystem<SmartTrafficSystem>(aiRoot, "SmartTrafficSystem");
            CreateSystem<SmartCityPedestriansAndTraffic>(aiRoot, "SmartCityPedestriansAndTraffic");
            CreateSystem<DiverseNPCPopulationSystem>(aiRoot, "DiverseNPCPopulationSystem");
            CreateSystem<RagdollAndImpactSystem>(aiRoot, "RagdollAndImpactSystem");
            CreateSystem<DynamicCrowdReactionSystem>(aiRoot, "DynamicCrowdReactionSystem");
            CreateSystem<NetworkWorldSyncManager>(aiRoot, "NetworkWorldSyncManager");
            CreateSystem<NetworkBackendClient>(aiRoot, "NetworkBackendClient");
            CreateSystem<CarMarketAndTradeSystem>(aiRoot, "CarMarketAndTradeSystem");
            CreateSystem<CarTokenLedgerClient>(aiRoot, "CarTokenLedgerClient");
            CreateSystem<StreetRaceAndTelemetrySystem>(aiRoot, "StreetRaceAndTelemetrySystem");
            CreateSystem<PolicePursuitAndRadarSystem>(aiRoot, "PolicePursuitAndRadarSystem");
            CreateSystem<VoiceChatManager>(aiRoot, "VoiceChatManager");
            CreateSystem<AIAutopilotSystem>(aiRoot, "AIAutopilotSystem");

            CreateSystem<WorldServicesAndEconomySystem>(uiRoot, "WorldServicesAndEconomySystem");
            CreateSystem<OBDScannerAndEconomySystem>(uiRoot, "OBDScannerAndEconomySystem");
            CreateSystem<InsuranceAndAccidentDeepLearningSystem>(uiRoot, "InsuranceAndAccidentDeepLearningSystem");
            CreateSystem<Graphics8KAndHUDController>(uiRoot, "Graphics8KAndHUDController");
            CreateSystem<GarageAndWorldSaveManager>(uiRoot, "GarageAndWorldSaveManager");
            CreateSystem<MobileTouchAndGyroController>(uiRoot, "MobileTouchAndGyroController");
            CreateSystem<DealershipLiveryAndTrailerSystem>(uiRoot, "DealershipLiveryAndTrailerSystem");
            CreateSystem<TruckingLogisticsBusinessSystem>(uiRoot, "TruckingLogisticsBusinessSystem");
            CreateSystem<CinematicDroneAndReplaySystem>(uiRoot, "CinematicDroneAndReplaySystem");
            CreateSystem<InGameTrackEditor>(uiRoot, "InGameTrackEditor");
            CreateSystem<InGameRadioSystem>(uiRoot, "InGameRadioSystem");
            CreateSystem<DynamicAudioOcclusionSystem>(uiRoot, "DynamicAudioOcclusionSystem");
            CreateSystem<PhotoAndCinematicDirectorSystem>(uiRoot, "PhotoAndCinematicDirectorSystem");
            CreateSystem<VRHeadsetAndHandController>(uiRoot, "VRHeadsetAndHandController");
            CreateSystem<CinematicCameraInertia>(uiRoot, "CinematicCameraInertia");
            CreateSystem<AdaptivePerformanceProfiler>(uiRoot, "AdaptivePerformanceProfiler");
            CreateSystem<CompanionAppBridge>(uiRoot, "CompanionAppBridge");

            GameObject vehicle = new GameObject("PlayableVehicle");
            vehicle.transform.SetParent(vehicleRoot.transform);
            vehicle.transform.position = new Vector3(0f, 1.2f, 0f);
            vehicle.tag = "Vehicle";

            var rb = vehicle.AddComponent<Rigidbody>();
            rb.mass = 1450f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var bodyCol = vehicle.AddComponent<BoxCollider>();
            bodyCol.size = new Vector3(1.9f, 1.3f, 4.5f);
            bodyCol.center = new Vector3(0f, 0.3f, 0f);

            var controller = vehicle.AddComponent<RealisticVehicleController>();
            var drivetrain = vehicle.AddComponent<DrivetrainSystem>();
            vehicle.AddComponent<PrecisionParkingSystem>();
            vehicle.AddComponent<VehicleEnvironmentBridge>();
            vehicle.AddComponent<VehicleTuningSystem>();
            vehicle.AddComponent<CockpitAndDashboardSystem>();
            vehicle.AddComponent<VehicleAudioSynthesizer>();
            vehicle.AddComponent<VehicleDamageAndDeformation>();
            vehicle.AddComponent<AdvancedMechanicalFailures>();
            vehicle.AddComponent<ShatterableGlassAndDebrisSystem>();
            vehicle.AddComponent<AdvancedDirtMaskController>();
            vehicle.AddComponent<OffRoadWinchAndRecoverySystem>();
            vehicle.AddComponent<VehicleImpactForwarder>();
            vehicle.AddComponent<AccidentReportForwarder>();
            vehicle.AddComponent<FlyingCarAndRocketBooster>();
            vehicle.AddComponent<AIAutopilotSystem>();

            controller.wheels = new RealisticVehicleController.Wheel[4];
            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            Vector3[] positions = {
                new Vector3(-0.85f, 0.35f, 1.45f),
                new Vector3(0.85f, 0.35f, 1.45f),
                new Vector3(-0.85f, 0.35f, -1.35f),
                new Vector3(0.85f, 0.35f, -1.35f)
            };
            for (int i = 0; i < 4; i++)
            {
                var wgo = new GameObject(names[i]);
                wgo.transform.SetParent(vehicle.transform);
                wgo.transform.localPosition = positions[i];
                var w = new RealisticVehicleController.Wheel();
                w.wheelTransform = wgo.transform;
                w.radius = 0.35f;
                w.isDriveWheel = true;
                w.isSteerWheel = i < 2;
                controller.wheels[i] = w;
            }
            controller.drivetrain = drivetrain;

            GameObject character = new GameObject("DriverCharacter");
            character.transform.SetParent(vehicleRoot.transform);
            character.transform.position = new Vector3(3f, 1f, 0f);
            character.AddComponent<CharacterController>();
            character.AddComponent<DriverCharacterController>();

            if (floatingOrigin != null) floatingOrigin.SetFocus(vehicle.transform);
            if (terrainStreamer != null)
            {
                var so = new SerializedObject(terrainStreamer);
                var vp = so.FindProperty("viewer");
                if (vp != null) { vp.objectReferenceValue = vehicle.transform; so.ApplyModifiedProperties(); }
            }

            Selection.activeGameObject = vehicle;
            EditorUtility.DisplayDialog("Ultra Car Simulator", "Modules 1-12 complete.", "OK");
            Debug.Log("[OneClickBuilder] Modules 1-12 complete.");
        }

        private static GameObject CreateRoot(string name) => new GameObject(name);
        private static T CreateSystem<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go.AddComponent<T>();
        }
    }
}
#endif
