using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using NextGenDrive.Network;
using NextGenDrive.Physics;

namespace NextGenDrive.Police
{
    [Serializable]
    public class SpeedTrapZone
    {
        public string zoneId;          // must match a zone id in server/src/police_fines.js
        public Transform center;
        public float radius = 15f;
        public float speedLimitKmh = 60f; // display only; the server uses its own limit
    }

    // Server replies: {"event":"POLICE_FINE","amount":900,"heat":1} or {"event":"BRIBE_RESULT","success":true,"heat":0,"cost":500}
    [Serializable]
    public class PoliceServerEvent
    {
        public string @event;
        public int amount;
        public int heat;
        public bool success;
        public int cost;
    }

    public class PolicePursuitAndRadarManager : MonoBehaviour
    {
        [Header("References")]
        public RealisticCarController playerCar;
        public MultiplayerNetworkClient network;

        [Header("Speed Traps")]
        public SpeedTrapZone[] speedTraps;
        public float reportCooldownSeconds = 8f;

        [Header("Heat & Pursuit")]
        [Range(0, 5)] public int heatLevel;
        [Tooltip("Heat from which interceptors chase")]
        public int pursuitHeatThreshold = 3;
        public GameObject interceptorPrefab;
        public int maxInterceptors = 3;
        public float interceptorSpawnDistance = 60f;
        public float interceptorSpeedKmh = 180f;
        public float catchDistance = 5f;
        public AudioSource sirenSource;

        public event Action<int> OnHeatChanged;
        public event Action OnPlayerCaught;

        // Network callbacks arrive on a background thread; they are queued and handled in Update
        private readonly ConcurrentQueue<string> pendingServerEvents = new ConcurrentQueue<string>();
        private readonly Dictionary<string, float> lastReportTime = new Dictionary<string, float>();
        private readonly List<Transform> interceptors = new List<Transform>();

        private void OnEnable()
        {
            if (network != null) network.OnServerEventReceived += EnqueueServerEvent;
        }

        private void OnDisable()
        {
            if (network != null) network.OnServerEventReceived -= EnqueueServerEvent;
        }

        private void EnqueueServerEvent(string json)
        {
            pendingServerEvents.Enqueue(json);
        }

        private void Update()
        {
            while (pendingServerEvents.TryDequeue(out string json))
            {
                HandleServerEvent(json);
            }

            if (playerCar == null) return;
            CheckSpeedTraps();
            UpdatePursuit();
        }

        private void CheckSpeedTraps()
        {
            if (speedTraps == null || network == null) return;

            float speedKmh = playerCar.GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f;
            foreach (var zone in speedTraps)
            {
                if (zone.center == null || string.IsNullOrEmpty(zone.zoneId)) continue;
                if (Vector3.Distance(playerCar.transform.position, zone.center.position) > zone.radius) continue;

                // Report at most once per zone per cooldown; the server verifies the speed and position
                if (lastReportTime.TryGetValue(zone.zoneId, out float last) && Time.time - last < reportCooldownSeconds) continue;
                lastReportTime[zone.zoneId] = Time.time;
                _ = network.ReportSpeedTrap(playerCar.vehicleVIN, zone.zoneId, speedKmh);
            }
        }

        private void HandleServerEvent(string json)
        {
            PoliceServerEvent ev;
            try
            {
                ev = JsonUtility.FromJson<PoliceServerEvent>(json);
            }
            catch (ArgumentException)
            {
                return; // not a police message
            }

            switch (ev?.@event)
            {
                case "POLICE_FINE":
                    SetHeat(ev.heat);
                    break;
                case "BRIBE_RESULT":
                    SetHeat(ev.heat);
                    break;
            }
        }

        private void SetHeat(int heat)
        {
            int clamped = Mathf.Clamp(heat, 0, 5);
            if (clamped == heatLevel) return;
            heatLevel = clamped;
            OnHeatChanged?.Invoke(heatLevel);
        }

        /// <summary>Asks the server to bribe the officer. Cost and outcome are decided by the server.</summary>
        public void RequestBribe()
        {
            if (network == null || playerCar == null || heatLevel == 0) return;
            _ = network.RequestBribe(playerCar.vehicleVIN);
        }

        private void UpdatePursuit()
        {
            bool chasing = heatLevel >= pursuitHeatThreshold && interceptorPrefab != null;
            if (chasing)
            {
                while (interceptors.Count < maxInterceptors) SpawnInterceptor();
            }
            else if (interceptors.Count > 0)
            {
                DespawnInterceptors();
            }

            if (sirenSource != null)
            {
                if (interceptors.Count > 0 && !sirenSource.isPlaying) sirenSource.Play();
                else if (interceptors.Count == 0 && sirenSource.isPlaying) sirenSource.Stop();
            }

            Vector3 playerPos = playerCar.transform.position;
            float speed = interceptorSpeedKmh / 3.6f;
            for (int i = interceptors.Count - 1; i >= 0; i--)
            {
                Transform car = interceptors[i];
                if (car == null) { interceptors.RemoveAt(i); continue; }

                Vector3 toPlayer = playerPos - car.position;
                toPlayer.y = 0f;
                if (toPlayer.magnitude <= catchDistance)
                {
                    OnPlayerCaught?.Invoke();
                    continue;
                }
                car.position += toPlayer.normalized * Mathf.Min(speed * Time.deltaTime, toPlayer.magnitude);
                car.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            }
        }

        private void SpawnInterceptor()
        {
            Vector3 behind = -playerCar.transform.forward * interceptorSpawnDistance;
            var go = Instantiate(interceptorPrefab, playerCar.transform.position + behind, Quaternion.identity);
            interceptors.Add(go.transform);
        }

        private void DespawnInterceptors()
        {
            foreach (var t in interceptors)
            {
                if (t != null) Destroy(t.gameObject);
            }
            interceptors.Clear();
        }
    }
}
