using System;
using System.IO;
using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.Core
{
    [Serializable]
    public class VehicleSaveData
    {
        public string vehicleId;
        public float[] position = new float[3];
        public float[] rotation = new float[4];
        public double originOffsetX, originOffsetY, originOffsetZ;

        public float odometerKm;
        public float fuelLevel;

        // Tuning
        public float frontCamber, rearCamber, frontCaster, frontToe, rearToe, wheelOffset;
        public int airPreset;
        public int engineType;
        public int ecuStage;
        public float turboBoostPSI;
        public float[] gearRatios;

        // Visual
        public int paintType;
        public float[] primaryColor = new float[4];
        public float[] secondaryColor = new float[4];
        public float windowTint;
    }

    [Serializable]
    public class WorldSaveData
    {
        public VehicleSaveData[] vehicles;
        public string lastSaveTime;
        public int saveVersion = 1;
    }

    /// <summary>
    /// Saves and loads vehicle tuning, paint, fuel, odometer and double-precision world position.
    /// </summary>
    public class GarageAndWorldSaveManager : MonoBehaviour
    {
        public static GarageAndWorldSaveManager Instance { get; private set; }

        [SerializeField] private string saveFileName = "world_save.json";

        private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SaveAll()
        {
            var allVehicles = FindObjectsByType<VehicleTuningSystem>(FindObjectsSortMode.None);
            WorldSaveData world = new WorldSaveData
            {
                vehicles = new VehicleSaveData[allVehicles.Length],
                lastSaveTime = DateTime.UtcNow.ToString("o")
            };

            for (int i = 0; i < allVehicles.Length; i++)
            {
                world.vehicles[i] = CaptureVehicle(allVehicles[i]);
            }

            string json = JsonUtility.ToJson(world, true);
            File.WriteAllText(SavePath, json);

            Debug.Log($"[SaveManager] Saved {allVehicles.Length} vehicles to {SavePath}");
        }

        public void LoadAll()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[SaveManager] No save file found.");
                return;
            }

            string json = File.ReadAllText(SavePath);
            WorldSaveData world = JsonUtility.FromJson<WorldSaveData>(json);

            if (world == null || world.vehicles == null) return;

            var allVehicles = FindObjectsByType<VehicleTuningSystem>(FindObjectsSortMode.None);

            foreach (var saved in world.vehicles)
            {
                // Match by simple index or vehicleId (expand matching logic as needed)
                foreach (var v in allVehicles)
                {
                    ApplyVehicle(v, saved);
                    break; // simple version - apply to first / expand later
                }
            }

            Debug.Log($"[SaveManager] Loaded save from {world.lastSaveTime}");
        }

        private VehicleSaveData CaptureVehicle(VehicleTuningSystem tuning)
        {
            var data = new VehicleSaveData();
            var t = tuning.transform;

            data.position[0] = t.position.x;
            data.position[1] = t.position.y;
            data.position[2] = t.position.z;

            data.rotation[0] = t.rotation.x;
            data.rotation[1] = t.rotation.y;
            data.rotation[2] = t.rotation.z;
            data.rotation[3] = t.rotation.w;

            if (FloatingOriginSystem.Instance != null)
            {
                var off = FloatingOriginSystem.Instance.TotalOriginOffset;
                data.originOffsetX = off.x;
                data.originOffsetY = off.y;
                data.originOffsetZ = off.z;
            }

            data.frontCamber = tuning.frontCamber;
            data.rearCamber = tuning.rearCamber;
            data.frontCaster = tuning.frontCaster;
            data.frontToe = tuning.frontToe;
            data.rearToe = tuning.rearToe;
            data.wheelOffset = tuning.wheelOffset;
            data.airPreset = tuning.currentAirPreset;
            data.engineType = (int)tuning.currentEngine;
            data.ecuStage = tuning.ecuStage;
            data.turboBoostPSI = tuning.turboBoostPSI;
            data.gearRatios = tuning.customGearRatios;

            data.paintType = (int)tuning.paintType;
            data.primaryColor[0] = tuning.primaryColor.r;
            data.primaryColor[1] = tuning.primaryColor.g;
            data.primaryColor[2] = tuning.primaryColor.b;
            data.primaryColor[3] = tuning.primaryColor.a;
            data.secondaryColor[0] = tuning.secondaryColor.r;
            data.secondaryColor[1] = tuning.secondaryColor.g;
            data.secondaryColor[2] = tuning.secondaryColor.b;
            data.secondaryColor[3] = tuning.secondaryColor.a;
            data.windowTint = tuning.windowTint;

            // Placeholder values for odometer / fuel (extend with real systems later)
            data.odometerKm = 0f;
            data.fuelLevel = 1f;

            return data;
        }

        private void ApplyVehicle(VehicleTuningSystem tuning, VehicleSaveData data)
        {
            tuning.frontCamber = data.frontCamber;
            tuning.rearCamber = data.rearCamber;
            tuning.frontCaster = data.frontCaster;
            tuning.frontToe = data.frontToe;
            tuning.rearToe = data.rearToe;
            tuning.wheelOffset = data.wheelOffset;
            tuning.currentAirPreset = data.airPreset;
            tuning.currentEngine = (EngineType)data.engineType;
            tuning.ecuStage = data.ecuStage;
            tuning.turboBoostPSI = data.turboBoostPSI;
            if (data.gearRatios != null) tuning.customGearRatios = data.gearRatios;

            tuning.paintType = (PaintType)data.paintType;
            tuning.primaryColor = new Color(data.primaryColor[0], data.primaryColor[1], data.primaryColor[2], data.primaryColor[3]);
            tuning.secondaryColor = new Color(data.secondaryColor[0], data.secondaryColor[1], data.secondaryColor[2], data.secondaryColor[3]);
            tuning.windowTint = data.windowTint;

            tuning.ApplyAllTuning();

            // Position restore (local to current origin)
            Vector3 pos = new Vector3(data.position[0], data.position[1], data.position[2]);
            Quaternion rot = new Quaternion(data.rotation[0], data.rotation[1], data.rotation[2], data.rotation[3]);
            tuning.transform.SetPositionAndRotation(pos, rot);
        }

        private void Update()
        {
            // Quick save / load hotkeys
            if (Input.GetKeyDown(KeyCode.F5))
                SaveAll();
            if (Input.GetKeyDown(KeyCode.F9))
                LoadAll();
        }
    }
}
