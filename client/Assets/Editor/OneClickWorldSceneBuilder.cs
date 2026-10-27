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

namespace CarParking.EditorTools
{
    /// <summary>
    /// One-click scene builder that wires up every system from Modules 1-6.
    /// Menu: Tools/Ultra Car Simulator/1. Build Complete Playable World & Vehicle Scene
    /// </summary>
    public static class OneClickWorldSceneBuilder
    {
        [MenuItem("Tools/Ultra Car Simulator/1. Build Complete Playable World & Vehicle Scene")]
        public static void BuildCompleteScene()
        {
            if (!EditorUtility.DisplayDialog("Ultra Car Simulator",
                "This will create and wire up all core systems (Modules 1-6) and a playable vehicle in the current scene.\nContinue?",
                "Build", "Cancel"))
                return;

            var worldRoot = CreateRoot("--- WORLD SYSTEMS ---");
            var vehicleRoot = CreateRoot("--- PLAYABLE VEHICLE ---");
            var aiRoot = CreateRoot("--- AI & MULTIPLAYER ---");
            var uiRoot = CreateRoot("--- UI & SERVICES ---");

            // MODULE 1
            var floatingOrigin = CreateSystem<FloatingOriginSystem>(worldRoot, "FloatingOriginSystem");
            var terrainStreamer = CreateSystem<ProceduralTerrainChunkStreamer>(worldRoot, "ProceduralTerrainChunkStreamer");
            CreateSystem<TerrainDeformationManager>(worldRoot, "TerrainDeformationManager");
            CreateSystem<UrbanAndBridgeGenerator>(worldRoot, "UrbanAndBridgeGenerator");
            CreateSystem<WaterAndWeatherSystem>(worldRoot, "WaterAndWeatherSystem");

            // MODULE 4 + 6 AI / Multiplayer
            CreateSystem<SmartTrafficSystem>(aiRoot, "SmartTrafficSystem");
            CreateSystem<NetworkWorldSyncManager>(aiRoot, "NetworkWorldSyncManager");
            CreateSystem<NetworkBackendClient>(aiRoot, "NetworkBackendClient");
            CreateSystem<CarMarketAndTradeSystem>(aiRoot, "CarMarketAndTradeSystem");
            CreateSystem<StreetRaceAndTelemetrySystem>(aiRoot, "StreetRaceAndTelemetrySystem");
            CreateSystem<PolicePursuitAndRadarSystem>(aiRoot, "PolicePursuitAndRadarSystem");

            // MODULE 5 + 6 Services & UI
            CreateSystem<WorldServicesAndEconomySystem>(uiRoot, "WorldServicesAndEconomySystem");
            CreateSystem<Graphics8KAndHUDController>(uiRoot, "Graphics8KAndHUDController");
            CreateSystem<GarageAndWorldSaveManager>(uiRoot, "GarageAndWorldSaveManager");
            CreateSystem<MobileTouchAndGyroController>(uiRoot, "MobileTouchAndGyroController");
            CreateSystem<DealershipLiveryAndTrailerSystem>(uiRoot, "DealershipLiveryAndTrailerSystem");

            // Playable Vehicle Hierarchy
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
            vehicle.AddComponent<OffRoadWinchAndRecoverySystem>();
            vehicle.AddComponent<DealershipLiveryAndTrailerSystem>();

            controller.wheels = new RealisticVehicleController.Wheel[4];
            string[] wheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            Vector3[] wheelPositions = {
                new Vector3(-0.85f, 0.35f, 1.45f),
                new Vector3(0.85f, 0.35f, 1.45f),
                new Vector3(-0.85f, 0.35f, -1.35f),
                new Vector3(0.85f, 0.35f, -1.35f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject wheel = new GameObject(wheelNames[i]);
                wheel.transform.SetParent(vehicle.transform);
                wheel.transform.localPosition = wheelPositions[i];

                var w = new RealisticVehicleController.Wheel();
                w.wheelTransform = wheel.transform;
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

            if (floatingOrigin != null)
                floatingOrigin.SetFocus(vehicle.transform);

            if (terrainStreamer != null)
            {
                var so = new SerializedObject(terrainStreamer);
                var viewerProp = so.FindProperty("viewer");
                if (viewerProp != null)
                {
                    viewerProp.objectReferenceValue = vehicle.transform;
                    so.ApplyModifiedProperties();
                }
            }

            Selection.activeGameObject = vehicle;
            EditorUtility.DisplayDialog("Ultra Car Simulator",
                "Complete playable world and vehicle hierarchy has been built successfully!\n\n" +
                "All systems from Modules 1-6 are present in the scene.",
                "OK");

            Debug.Log("[OneClickBuilder] Scene build complete (Modules 1-6).");
        }

        private static GameObject CreateRoot(string name)
        {
            return new GameObject(name);
        }

        private static T CreateSystem<T>(GameObject parent, string name) where T : Component
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go.AddComponent<T>();
        }
    }
}
#endif
