using System.Collections.Generic;
using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.Core
{
    /// <summary>
    /// Dynamic dirt accumulation, car wash, fuel stations, parking missions and economy.
    /// </summary>
    public class WorldServicesAndEconomySystem : MonoBehaviour
    {
        public static WorldServicesAndEconomySystem Instance { get; private set; }

        [Header("Economy")]
        public float playerCash = 15000f;

        [Header("Dirt")]
        public float dirtAccumulationRate = 0.015f;
        public float maxDirt = 1f;

        [Header("Fuel")]
        public float currentFuel = 0.75f;
        public float fuelCapacity = 1f;
        public float fuelConsumptionPerKm = 0.008f;

        [Header("Missions")]
        public int totalParkingMissions = 50;

        private float _currentDirt;
        private float _distanceTracker;
        private Vector3 _lastPos;
        private List<ParkingMission> _missions = new();
        private int _completedMissions;

        [System.Serializable]
        public class ParkingMission
        {
            public string name;
            public Vector3 targetPosition;
            public Quaternion targetRotation;
            public float tolerancePosition = 0.35f;
            public float toleranceAngle = 8f;
            public float reward = 500f;
            public bool isTimeTrial;
            public float timeLimit = 60f;
            public bool completed;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            GenerateParkingMissions();
        }

        private void Start()
        {
            _lastPos = transform.position;
        }

        private void Update()
        {
            UpdateDirtAndFuel();
            CheckParkingMissions();
        }

        private void UpdateDirtAndFuel()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle == null) return;

            Vector3 pos = vehicle.transform.position;
            float dist = Vector3.Distance(pos, _lastPos);
            _lastPos = pos;

            if (dist > 0.01f)
            {
                _distanceTracker += dist;

                // Fuel consumption
                currentFuel = Mathf.Max(0f, currentFuel - dist * 0.001f * fuelConsumptionPerKm * 100f);

                // Dirt accumulation based on biome
                BiomeType biome = BiomeType.Grassland;
                if (vehicle.wheels != null && vehicle.wheels.Length > 0 && vehicle.wheels[0].isGrounded)
                    biome = vehicle.wheels[0].surfaceBiome;

                float dirtRate = biome switch
                {
                    BiomeType.Mud => dirtAccumulationRate * 3.5f,
                    BiomeType.Desert => dirtAccumulationRate * 2.2f,
                    BiomeType.Beach => dirtAccumulationRate * 1.8f,
                    BiomeType.Rainforest => dirtAccumulationRate * 1.5f,
                    _ => dirtAccumulationRate * 0.3f
                };

                if (WaterAndWeatherSystem.Instance != null && WaterAndWeatherSystem.Instance.rainIntensity > 0.3f)
                    dirtRate *= 0.6f; // rain washes some dirt

                _currentDirt = Mathf.Min(maxDirt, _currentDirt + dist * dirtRate * 0.01f);

                // Apply dirt to materials if available
                ApplyDirtToVehicle(vehicle.gameObject);
            }
        }

        private void ApplyDirtToVehicle(GameObject vehicle)
        {
            var renderers = vehicle.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.material != null && r.material.HasProperty("_DirtAmount"))
                    r.material.SetFloat("_DirtAmount", _currentDirt);
            }
        }

        public void WashVehicle()
        {
            _currentDirt = Mathf.Max(0f, _currentDirt - 0.35f);
            if (_currentDirt < 0.05f) _currentDirt = 0f;
            Debug.Log($"[Services] Vehicle washed. Dirt: {_currentDirt:F2}");
        }

        public bool Refuel(float amount, float pricePerUnit, string octane)
        {
            float cost = amount * pricePerUnit;
            if (playerCash < cost) return false;

            playerCash -= cost;
            currentFuel = Mathf.Min(fuelCapacity, currentFuel + amount);
            Debug.Log($"[Services] Refueled {amount:F2} of {octane}. Cash left: {playerCash:F0}");
            return true;
        }

        public bool RepairVehicle(float cost)
        {
            if (playerCash < cost) return false;

            var damage = FindFirstObjectByType<VehicleDamageAndDeformation>();
            if (damage != null)
            {
                playerCash -= cost;
                damage.FullRepair();
                return true;
            }
            return false;
        }

        private void GenerateParkingMissions()
        {
            for (int i = 0; i < totalParkingMissions; i++)
            {
                _missions.Add(new ParkingMission
                {
                    name = $"Parking Challenge #{i + 1}",
                    targetPosition = new Vector3(Random.Range(-200f, 200f), 0.5f, Random.Range(-200f, 200f)),
                    targetRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    tolerancePosition = Random.Range(0.25f, 0.5f),
                    toleranceAngle = Random.Range(5f, 12f),
                    reward = Random.Range(300f, 1200f),
                    isTimeTrial = Random.value > 0.6f,
                    timeLimit = Random.Range(40f, 90f)
                });
            }
        }

        private void CheckParkingMissions()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle == null) return;

            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude > 0.3f) return; // must be stopped

            foreach (var mission in _missions)
            {
                if (mission.completed) continue;

                float posError = Vector3.Distance(vehicle.transform.position, mission.targetPosition);
                float angleError = Quaternion.Angle(vehicle.transform.rotation, mission.targetRotation);

                if (posError <= mission.tolerancePosition && angleError <= mission.toleranceAngle)
                {
                    mission.completed = true;
                    _completedMissions++;
                    playerCash += mission.reward;
                    Debug.Log($"[Mission] {mission.name} completed! +${mission.reward:N0}");

                    if (GarageAndWorldSaveManager.Instance != null)
                        GarageAndWorldSaveManager.Instance.SaveAll();
                }
            }
        }

        public float DirtLevel => _currentDirt;
        public int CompletedMissions => _completedMissions;
    }
}
