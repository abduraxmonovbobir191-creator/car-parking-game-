using System.Collections.Generic;
using UnityEngine;
using CarParking.Vehicle;
using CarParking.Core;

namespace CarParking.Core
{
    public enum CargoType { FuelTanker, HeavyMachinery, FragileContainer, StandardContainer, Livestock }

    /// <summary>
    /// "Dalnoboyshik" trucking logistics & cargo business system.
    /// Job board, cargo trailers, distance/time/damage based payouts.
    /// </summary>
    public class TruckingLogisticsBusinessSystem : MonoBehaviour
    {
        public static TruckingLogisticsBusinessSystem Instance { get; private set; }

        [System.Serializable]
        public class CargoJob
        {
            public string jobId;
            public string title;
            public CargoType cargoType;
            public Vector3 pickupPos;
            public Vector3 deliveryPos;
            public float distanceKm;
            public float timeLimitSec;
            public float basePayout;
            public float cargoHealth = 1f;
            public bool isFragile;
            public bool accepted;
            public bool completed;
        }

        [Header("Company")]
        public string companyName = "My Transport";
        public int companyLevel = 1;
        public float companyReputation = 50f;

        [Header("Jobs")]
        public int maxActiveJobs = 3;

        private List<CargoJob> _availableJobs = new();
        private List<CargoJob> _activeJobs = new();
        private CargoJob _currentJob;
        private float _jobStartTime;
        private Vector3 _lastVehiclePos;
        private float _roughDrivingAccumulator;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            GenerateJobBoard();
        }

        private void Update()
        {
            if (_currentJob == null || _currentJob.completed) return;

            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle == null) return;

            // Cargo damage from rough driving
            if (_currentJob.isFragile)
            {
                var rb = vehicle.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float accel = (rb.linearVelocity - (Vector3)(_lastVehiclePos != Vector3.zero ? (vehicle.transform.position - _lastVehiclePos) / Time.deltaTime : Vector3.zero)).magnitude;
                    if (Time.deltaTime > 0f && accel > 15f)
                    {
                        _roughDrivingAccumulator += Time.deltaTime;
                        _currentJob.cargoHealth = Mathf.Max(0f, _currentJob.cargoHealth - Time.deltaTime * 0.04f);
                    }
                }
            }
            _lastVehiclePos = vehicle.transform.position;

            // Delivery check
            float distToDelivery = Vector3.Distance(vehicle.transform.position, _currentJob.deliveryPos);
            if (distToDelivery < 15f)
            {
                var rb = vehicle.GetComponent<Rigidbody>();
                if (rb != null && rb.linearVelocity.magnitude < 1.5f)
                    CompleteJob();
            }

            // Time limit
            if (Time.time - _jobStartTime > _currentJob.timeLimitSec)
            {
                FailJob("Time expired");
            }
        }

        private void GenerateJobBoard()
        {
            _availableJobs.Clear();
            string[] titles = {
                "Fuel delivery to industrial zone",
                "Heavy excavator transport",
                "Fragile electronics shipment",
                "Container haul to port",
                "Livestock transfer"
            };

            for (int i = 0; i < 8; i++)
            {
                Vector3 pickup = new Vector3(Random.Range(-400f, 400f), 0.5f, Random.Range(-400f, 400f));
                Vector3 delivery = new Vector3(Random.Range(-400f, 400f), 0.5f, Random.Range(-400f, 400f));
                float dist = Vector3.Distance(pickup, delivery) / 1000f;

                CargoType type = (CargoType)Random.Range(0, 5);
                bool fragile = type == CargoType.FragileContainer || type == CargoType.Livestock;

                _availableJobs.Add(new CargoJob
                {
                    jobId = System.Guid.NewGuid().ToString().Substring(0, 8),
                    title = titles[i % titles.Length],
                    cargoType = type,
                    pickupPos = pickup,
                    deliveryPos = delivery,
                    distanceKm = dist,
                    timeLimitSec = 180f + dist * 120f,
                    basePayout = 800f + dist * 1500f + (fragile ? 400f : 0f),
                    isFragile = fragile,
                    cargoHealth = 1f
                });
            }
        }

        public List<CargoJob> GetAvailableJobs() => new List<CargoJob>(_availableJobs);

        public bool AcceptJob(string jobId)
        {
            if (_activeJobs.Count >= maxActiveJobs) return false;

            CargoJob job = _availableJobs.Find(j => j.jobId == jobId);
            if (job == null) return false;

            job.accepted = true;
            _availableJobs.Remove(job);
            _activeJobs.Add(job);
            _currentJob = job;
            _jobStartTime = Time.time;
            _roughDrivingAccumulator = 0f;

            Debug.Log($"[Trucking] Accepted: {job.title} | {job.distanceKm:F1} km | ${job.basePayout:N0}");
            return true;
        }

        private void CompleteJob()
        {
            if (_currentJob == null) return;

            float timeBonus = 1f;
            float elapsed = Time.time - _jobStartTime;
            if (elapsed < _currentJob.timeLimitSec * 0.6f)
                timeBonus = 1.25f;

            float healthMult = 0.5f + _currentJob.cargoHealth * 0.5f;
            float payout = _currentJob.basePayout * timeBonus * healthMult * (1f + companyLevel * 0.05f);

            if (WorldServicesAndEconomySystem.Instance != null)
                WorldServicesAndEconomySystem.Instance.playerCash += payout;

            companyReputation = Mathf.Min(100f, companyReputation + 2f);
            if (companyReputation >= 50f + companyLevel * 20f)
            {
                companyLevel++;
                Debug.Log($"[Trucking] Company leveled up to {companyLevel}!");
            }

            _currentJob.completed = true;
            _activeJobs.Remove(_currentJob);
            Debug.Log($"[Trucking] Delivered! +${payout:N0} (health {_currentJob.cargoHealth * 100f:F0}%)");

            _currentJob = null;
            GenerateJobBoard();

            if (GarageAndWorldSaveManager.Instance != null)
                GarageAndWorldSaveManager.Instance.SaveAll();
        }

        private void FailJob(string reason)
        {
            if (_currentJob == null) return;
            companyReputation = Mathf.Max(0f, companyReputation - 5f);
            Debug.Log($"[Trucking] Job failed: {reason}");
            _activeJobs.Remove(_currentJob);
            _currentJob = null;
            GenerateJobBoard();
        }

        public CargoJob CurrentJob => _currentJob;
        public int ActiveJobCount => _activeJobs.Count;
    }
}
