using System.Collections.Generic;
using UnityEngine;
using CarParking.AI;
using CarParking.World;

namespace CarParking.AI
{
    /// <summary>
    /// Smart city pedestrians (crosswalks) and dynamic AI traffic that yields
    /// during police pursuits or accidents.
    /// </summary>
    public class SmartCityPedestriansAndTraffic : MonoBehaviour
    {
        public static SmartCityPedestriansAndTraffic Instance { get; private set; }

        [Header("Pedestrians")]
        public GameObject pedestrianPrefab;
        public int maxPedestrians = 40;
        public float pedSpeed = 1.4f;
        public float spawnRadius = 80f;

        [Header("Crosswalks")]
        public float crosswalkCheckRadius = 12f;

        [Header("Traffic Yield")]
        public float yieldDistance = 25f;
        public float accidentSlowRadius = 40f;

        private Transform _player;
        private List<PedestrianAgent> _peds = new();
        private List<Vector3> _crosswalkPoints = new();

        private class PedestrianAgent
        {
            public GameObject go;
            public Transform transform;
            public Vector3 target;
            public bool crossing;
            public float speed;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
            if (v != null) _player = v.transform;

            // Generate simple crosswalk grid for city biomes
            for (int x = -5; x <= 5; x++)
                for (int z = -5; z <= 5; z++)
                    _crosswalkPoints.Add(new Vector3(x * 60f, 0.1f, z * 60f));

            SpawnPedestrians();
        }

        private void Update()
        {
            if (_player == null)
            {
                var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
                if (v != null) _player = v.transform;
                return;
            }

            UpdatePedestrians();
            ApplyTrafficYield();
        }

        private void SpawnPedestrians()
        {
            if (pedestrianPrefab == null) return;

            for (int i = 0; i < maxPedestrians; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spawnRadius;
                Vector3 pos = (_player != null ? _player.position : Vector3.zero) + new Vector3(offset.x, 0f, offset.y);
                pos.y = 0.1f;

                GameObject go = Instantiate(pedestrianPrefab, pos, Quaternion.identity, transform);
                var agent = new PedestrianAgent
                {
                    go = go,
                    transform = go.transform,
                    target = pos + Random.insideUnitSphere * 15f,
                    speed = pedSpeed * Random.Range(0.8f, 1.2f)
                };
                agent.target.y = 0.1f;
                _peds.Add(agent);
            }
        }

        private void UpdatePedestrians()
        {
            bool pursuitActive = PolicePursuitAndRadarSystem.Instance != null &&
                                 PolicePursuitAndRadarSystem.Instance.WantedLevel > 0;

            foreach (var ped in _peds)
            {
                if (ped.transform == null) continue;

                // Flee if police pursuit nearby
                if (pursuitActive && _player != null)
                {
                    float d = Vector3.Distance(ped.transform.position, _player.position);
                    if (d < 30f)
                    {
                        Vector3 flee = (ped.transform.position - _player.position).normalized;
                        ped.target = ped.transform.position + flee * 20f;
                        ped.speed = pedSpeed * 2f;
                    }
                }

                Vector3 toTarget = ped.target - ped.transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < 1f)
                {
                    // Pick new target — prefer crosswalk
                    if (_crosswalkPoints.Count > 0 && Random.value > 0.5f)
                        ped.target = _crosswalkPoints[Random.Range(0, _crosswalkPoints.Count)];
                    else
                        ped.target = ped.transform.position + Random.insideUnitSphere * 20f;
                    ped.target.y = 0.1f;
                    ped.speed = pedSpeed * Random.Range(0.8f, 1.2f);
                }
                else
                {
                    ped.transform.position += toTarget.normalized * ped.speed * Time.deltaTime;
                    ped.transform.rotation = Quaternion.Slerp(
                        ped.transform.rotation,
                        Quaternion.LookRotation(toTarget),
                        Time.deltaTime * 5f);
                }

                // Despawn / respawn far ones
                if (_player != null && Vector3.Distance(ped.transform.position, _player.position) > spawnRadius * 1.5f)
                {
                    Vector2 offset = Random.insideUnitCircle * spawnRadius * 0.8f;
                    ped.transform.position = _player.position + new Vector3(offset.x, 0.1f, offset.y);
                }
            }
        }

        private void ApplyTrafficYield()
        {
            // Signal SmartTrafficSystem agents to slow down near accidents / player police chase
            bool shouldYield = false;
            if (PolicePursuitAndRadarSystem.Instance != null &&
                PolicePursuitAndRadarSystem.Instance.WantedLevel >= 2)
                shouldYield = true;

            if (!shouldYield || _player == null) return;

            // Find nearby rigidbodies that look like traffic and slow them
            Collider[] cols = Physics.OverlapSphere(_player.position, yieldDistance);
            foreach (var col in cols)
            {
                if (col.attachedRigidbody == null) continue;
                if (col.attachedRigidbody.gameObject == _player.gameObject) continue;

                // Gentle brake force
                Vector3 vel = col.attachedRigidbody.linearVelocity;
                col.attachedRigidbody.linearVelocity = Vector3.Lerp(vel, vel * 0.3f, Time.deltaTime * 2f);
            }
        }
    }
}
