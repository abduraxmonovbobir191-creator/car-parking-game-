using System.Collections.Generic;
using UnityEngine;
using CarParking.World;
using CarParking.Core;
using CarParking.Vehicle;

namespace CarParking.AI
{
    /// <summary>
    /// Speed radars + 1-to-5 star police pursuit AI.
    /// Fines via WorldServicesAndEconomySystem. Escape by hiding in rainforest or garages.
    /// </summary>
    public class PolicePursuitAndRadarSystem : MonoBehaviour
    {
        public static PolicePursuitAndRadarSystem Instance { get; private set; }

        [Header("Radar")]
        public float[] radarSpeedLimits = { 60f, 80f, 100f, 120f };
        public float radarRange = 45f;
        public float finePerKmhOver = 15f;

        [Header("Wanted")]
        public int wantedLevel;
        public float wantedDecayTime = 90f;
        public GameObject policePrefab;
        public int maxPoliceUnits = 6;

        [Header("Pursuit")]
        public float spawnDistance = 80f;
        public float catchDistance = 6f;

        private float _wantedTimer;
        private List<PoliceUnit> _activePolice = new();
        private Transform _player;

        private class PoliceUnit
        {
            public GameObject go;
            public Rigidbody rb;
            public Transform transform;
            public float aggressiveness;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle != null) _player = vehicle.transform;
        }

        private void Update()
        {
            if (_player == null)
            {
                var v = FindFirstObjectByType<RealisticVehicleController>();
                if (v != null) _player = v.transform;
                return;
            }

            CheckRadars();
            UpdateWantedLevel();
            UpdatePoliceAI();
        }

        private void CheckRadars()
        {
            if (_player == null) return;

            var rb = _player.GetComponent<Rigidbody>();
            if (rb == null) return;

            float speedKmh = rb.linearVelocity.magnitude * 3.6f;

            // Simple radar zones along major axes (city highways)
            bool inRadarZone = Mathf.Abs(_player.position.x) % 220f < 30f
                            || Mathf.Abs(_player.position.z) % 220f < 30f;

            if (!inRadarZone) return;

            float limit = radarSpeedLimits[Mathf.Clamp(wantedLevel, 0, radarSpeedLimits.Length - 1)];
            if (speedKmh > limit + 5f)
            {
                float over = speedKmh - limit;
                float fine = over * finePerKmhOver;

                if (WorldServicesAndEconomySystem.Instance != null)
                {
                    WorldServicesAndEconomySystem.Instance.playerCash =
                        Mathf.Max(0f, WorldServicesAndEconomySystem.Instance.playerCash - fine);
                }

                IncreaseWanted(1);
                Debug.Log($"[Radar] Speeding! +{over:F0} km/h over limit. Fine ${fine:F0}");
            }
        }

        public void IncreaseWanted(int stars)
        {
            wantedLevel = Mathf.Clamp(wantedLevel + stars, 0, 5);
            _wantedTimer = wantedDecayTime;

            int desiredUnits = wantedLevel * 1;
            while (_activePolice.Count < desiredUnits && _activePolice.Count < maxPoliceUnits)
                SpawnPoliceUnit();
        }

        private void UpdateWantedLevel()
        {
            if (wantedLevel <= 0) return;

            _wantedTimer -= Time.deltaTime;

            // Hide in rainforest or low speed in garage-like areas reduces wanted
            if (_player != null)
            {
                // Approximate biome check
                float nx = _player.position.x * 0.0008f;
                float nz = _player.position.z * 0.0008f;
                float moisture = Mathf.PerlinNoise(nx * 1.7f, nz * 1.7f);
                float temperature = Mathf.PerlinNoise(nx * 0.9f + 100f, nz * 0.9f + 100f);

                bool inRainforest = moisture > 0.7f && temperature > 0.45f;
                var rb = _player.GetComponent<Rigidbody>();
                bool almostStopped = rb != null && rb.linearVelocity.magnitude < 1.5f;

                if (inRainforest || almostStopped)
                    _wantedTimer -= Time.deltaTime * 1.5f;
            }

            if (_wantedTimer <= 0f)
            {
                wantedLevel = Mathf.Max(0, wantedLevel - 1);
                _wantedTimer = wantedDecayTime * 0.6f;

                while (_activePolice.Count > wantedLevel && _activePolice.Count > 0)
                {
                    var unit = _activePolice[_activePolice.Count - 1];
                    if (unit.go != null) Destroy(unit.go);
                    _activePolice.RemoveAt(_activePolice.Count - 1);
                }
            }
        }

        private void SpawnPoliceUnit()
        {
            if (policePrefab == null || _player == null) return;

            Vector2 offset = Random.insideUnitCircle.normalized * spawnDistance;
            Vector3 pos = _player.position + new Vector3(offset.x, 0f, offset.y);
            pos.y = _player.position.y;

            GameObject go = Instantiate(policePrefab, pos, Quaternion.identity);
            var unit = new PoliceUnit
            {
                go = go,
                transform = go.transform,
                rb = go.GetComponent<Rigidbody>(),
                aggressiveness = 0.6f + wantedLevel * 0.08f
            };
            _activePolice.Add(unit);
        }

        private void UpdatePoliceAI()
        {
            if (_player == null) return;

            foreach (var unit in _activePolice)
            {
                if (unit.transform == null) continue;

                Vector3 toPlayer = _player.position - unit.transform.position;
                float dist = toPlayer.magnitude;

                if (dist < catchDistance && wantedLevel >= 3)
                {
                    // Busted
                    Debug.Log("[Police] Player caught!");
                    if (WorldServicesAndEconomySystem.Instance != null)
                        WorldServicesAndEconomySystem.Instance.playerCash *=
                            0.85f;
                    wantedLevel = 0;
                    ClearAllPolice();
                    return;
                }

                // Simple pursuit: steer and accelerate toward player
                if (unit.rb != null)
                {
                    Vector3 dir = toPlayer.normalized;
                    float speed = 18f + wantedLevel * 3f;
                    Vector3 targetVel = dir * speed;
                    unit.rb.linearVelocity = Vector3.Lerp(unit.rb.linearVelocity, targetVel, Time.deltaTime * 2.5f * unit.aggressiveness);

                    if (dir.sqrMagnitude > 0.01f)
                    {
                        Quaternion look = Quaternion.LookRotation(dir);
                        unit.transform.rotation = Quaternion.Slerp(unit.transform.rotation, look, Time.deltaTime * 4f);
                    }

                    // PIT attempt at high wanted when close
                    if (wantedLevel >= 4 && dist < 12f)
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        unit.rb.AddForce(side * 30f * unit.aggressiveness, ForceMode.Acceleration);
                    }
                }
            }
        }

        private void ClearAllPolice()
        {
            foreach (var unit in _activePolice)
                if (unit.go != null) Destroy(unit.go);
            _activePolice.Clear();
        }

        public int WantedLevel => wantedLevel;
    }
}
