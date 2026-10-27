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
    /// One-click scene builder that wires up every system from Modules 1-5.
    /// Menu: Tools/Ultra Car Simulator/1. Build Complete Playable World & Vehicle Scene
    /// </summary>
    public static class OneClickWorldSceneBuilder
    {
        [MenuItem("Tools/Ultra Car Simulator/1. Build Complete Playable World & Vehicle Scene")]
        public static void BuildCompleteScene()
        {
            if (!EditorUtility.DisplayDialog("Ultra Car Simulator",
                "This will create and wire up all core systems and a playable vehicle in the current scene.\nContinue?",
                "Build", "Cancel"))
                return;

            // Root systems
            var worldRoot = CreateRoot("--- WORLD SYSTEMS ---");
            var vehicleRoot = CreateRoot("--- PLAYABLE VEHICLE ---");
            var aiRoot = CreateRoot("--- AI & MULTIPLAYER ---");
            var uiRoot = CreateRoot("--- UI & SERVICES ---");

            // MODULE 1
            var floatingOrigin = CreateSystem<FloatingOriginSystem>(worldRoot, "FloatingOriginSystem");
            var terrainStreamer = CreateSystem<ProceduralTerrainChunkStreamer>(worldRoot, "ProceduralTerrainChunkStreamer");
            var deformation = CreateSystem<TerrainDeformationManager>(worldRoot, "TerrainDeformationManager");
            var urban = CreateSystem<UrbanAndBridgeGenerator>(worldRoot, "UrbanAndBridgeGenerator");
            var waterWeather = CreateSystem<WaterAndWeatherSystem>(worldRoot, "WaterAndWeatherSystem");

            // MODULE 4 AI / Multiplayer
            var traffic = CreateSystem<SmartTrafficSystem>(aiRoot, "SmartTrafficSystem");
            var networkSync = CreateSystem<NetworkWorldSyncManager>(aiRoot, "NetworkWorldSyncManager");
            var carMarket = CreateSystem<CarMarketAndTradeSystem>(aiRoot, "CarMarketAndTradeSystem");
            var raceSystem = CreateSystem<StreetRaceAndTelemetrySystem>(aiRoot, "StreetRaceAndTelemetrySystem");

            // MODULE 5 Services & UI
            var economy = CreateSystem<WorldServicesAndEconomySystem>(uiRoot, "WorldServicesAndEconomySystem");
            var graphicsHUD = CreateSystem<Graphics8KAndHUDController>(uiRoot, "Graphics8KAndHUDController");
            var saveManager = CreateSystem<GarageAndWorldSaveManager>(uiRoot, "GarageAndWorldSaveManager");

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

            // Body collider
            var bodyCol = vehicle.AddComponent<BoxCollider>();
            bodyCol.size = new Vector3(1.9f, 1.3f, 4.5f);
            bodyCol.center = new Vector3(0f, 0.3f, 0f);

            // Core vehicle systems
            var controller = vehicle.AddComponent<RealisticVehicleController>();
            var drivetrain = vehicle.AddComponent<DrivetrainSystem>();
            var parking = vehicle.AddComponent<PrecisionParkingSystem>();
            var bridge = vehicle.AddComponent<VehicleEnvironmentBridge>();
            var tuning = vehicle.AddComponent<VehicleTuningSystem>();
            var cockpit = vehicle.AddComponent<CockpitAndDashboardSystem>();
            var audio = vehicle.AddComponent<VehicleAudioSynthesizer>();
            var damage = vehicle.AddComponent<VehicleDamageAndDeformation>();
            var winch = vehicle.AddComponent<OffRoadWinchAndRecoverySystem>();

            // Create 4 wheels
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

            // Driver character
            GameObject character = new GameObject("DriverCharacter");
            character.transform.SetParent(vehicleRoot.transform);
            character.transform.position = new Vector3(3f, 1f, 0f);
            character.AddComponent<CharacterController>();
            var driver = character.AddComponent<DriverCharacterController>();

            // Wire FloatingOrigin focus
            if (floatingOrigin != null)
                floatingOrigin.SetFocus(vehicle.transform);

            // Wire terrain streamer viewer
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
                "All systems from Modules 1-5 are present in the scene.",
                "OK");

            Debug.Log("[OneClickBuilder] Scene build complete.");
        }

        private static GameObject CreateRoot(string name)
        {
            var go = new GameObject(name);
            return go;
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
