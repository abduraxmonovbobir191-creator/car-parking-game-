using System.Collections.Generic;
using UnityEngine;
using CarParking.AI;
using CarParking.Vehicle;

namespace CarParking.AI
{
    /// <summary>
    /// NPCs react to reckless driving: dive, panic, phone record, call police.
    /// </summary>
    public class DynamicCrowdReactionSystem : MonoBehaviour
    {
        public static DynamicCrowdReactionSystem Instance { get; private set; }

        [Header("Reaction")]
        public float awarenessRadius = 22f;
        public float panicSpeed = 4.5f;
        public float honkReactionRadius = 30f;
        public float callPoliceChance = 0.25f;
        public float callPoliceCooldown = 15f;

        private Transform _player;
        private float _lastCallTime = -100f;
        private List<Reactor> _reactors = new();

        private class Reactor
        {
            public Transform transform;
            public Vector3 originalPos;
            public float panicTimer;
            public bool isPanicking;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var v = FindFirstObjectByType<RealisticVehicleController>();
            if (v != null) _player = v.transform;
            RefreshReactors();
        }

        private void Update()
        {
            if (_player == null)
            {
                var v = FindFirstObjectByType<RealisticVehicleController>();
                if (v != null) _player = v.transform;
                return;
            }

            if (_reactors.Count == 0) RefreshReactors();

            var rb = _player.GetComponent<Rigidbody>();
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            bool reckless = speed > 15f;

            foreach (var r in _reactors)
            {
                if (r.transform == null) continue;

                float dist = Vector3.Distance(r.transform.position, _player.position);

                if (reckless && dist < awarenessRadius)
                {
                    if (!r.isPanicking)
                    {
                        r.isPanicking = true;
                        r.panicTimer = Random.Range(1.5f, 3.5f);

                        // Chance to call police
                        if (Time.time - _lastCallTime > callPoliceCooldown && Random.value < callPoliceChance)
                        {
                            _lastCallTime = Time.time;
                            if (PolicePursuitAndRadarSystem.Instance != null)
                            {
                                PolicePursuitAndRadarSystem.Instance.IncreaseWanted(1);
                                Debug.Log("[Crowd] Someone called the police!");
                            }
                        }
                    }
                }

                if (r.isPanicking)
                {
                    r.panicTimer -= Time.deltaTime;
                    Vector3 flee = (r.transform.position - _player.position).normalized;
                    flee.y = 0f;
                    r.transform.position += flee * panicSpeed * Time.deltaTime;
                    if (flee.sqrMagnitude > 0.01f)
                        r.transform.rotation = Quaternion.Slerp(r.transform.rotation, Quaternion.LookRotation(flee), Time.deltaTime * 8f);

                    if (r.panicTimer <= 0f)
                        r.isPanicking = false;
                }
            }
        }

        private void RefreshReactors()
        {
            _reactors.Clear();
            // Find objects tagged or named as pedestrians
            var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t.CompareTag("Pedestrian") || t.name.Contains("Pedestrian") || t.name.Contains("NPC"))
                {
                    _reactors.Add(new Reactor
                    {
                        transform = t,
                        originalPos = t.position
                    });
                }
            }
        }

        public void NotifyHonk(Vector3 fromPosition)
        {
            foreach (var r in _reactors)
            {
                if (r.transform == null) continue;
                if (Vector3.Distance(r.transform.position, fromPosition) < honkReactionRadius)
                {
                    r.isPanicking = true;
                    r.panicTimer = 1.2f;
                }
            }
        }

        public void NotifyCrash(Vector3 position)
        {
            foreach (var r in _reactors)
            {
                if (r.transform == null) continue;
                if (Vector3.Distance(r.transform.position, position) < awarenessRadius * 1.5f)
                {
                    r.isPanicking = true;
                    r.panicTimer = Random.Range(2f, 4f);
                }
            }

            if (Time.time - _lastCallTime > callPoliceCooldown * 0.5f && Random.value < callPoliceChance * 2f)
            {
                _lastCallTime = Time.time;
                if (PolicePursuitAndRadarSystem.Instance != null)
                    PolicePursuitAndRadarSystem.Instance.IncreaseWanted(1);
            }
        }
    }
}
