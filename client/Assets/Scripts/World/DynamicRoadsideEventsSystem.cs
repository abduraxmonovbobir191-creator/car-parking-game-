using UnityEngine;

namespace CarParking.World
{
    /// <summary>
    /// MODULE 43 — Dynamic World Random Events & Roadside Encounters
    /// Stranded motorists, rockslides, barn finds, ambushes.
    /// </summary>
    public class DynamicRoadsideEventsSystem : MonoBehaviour
    {
        public static DynamicRoadsideEventsSystem Instance { get; private set; }

        public enum EventType { StrandedMotorist, Rockslide, BarnFind, RacingAmbush, FuelTanker, HelicopterCrash, None }

        public EventType activeEvent = EventType.None;
        public Vector3 eventPosition;
        public float eventTimer;
        public float spawnChancePerMinute = 0.15f;

        private Transform _player;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
            if (v != null) _player = v.transform;
        }

        private void Update()
        {
            if (_player == null) return;
            if (activeEvent == EventType.None && Random.value < spawnChancePerMinute * Time.deltaTime / 60f)
                SpawnRandomEvent();

            if (activeEvent != EventType.None)
            {
                eventTimer -= Time.deltaTime;
                if (eventTimer <= 0f) ClearEvent();
            }
        }

        private void SpawnRandomEvent()
        {
            activeEvent = (EventType)Random.Range(1, 7);
            Vector2 o = Random.insideUnitCircle * 80f;
            eventPosition = _player.position + new Vector3(o.x, 0f, o.y);
            eventTimer = 120f;
            Debug.Log($"[Event] Spawned {activeEvent} at {eventPosition}");
        }

        public void InteractWithEvent()
        {
            if (activeEvent == EventType.None) return;
            Debug.Log($"[Event] Interacted with {activeEvent}");
            if (activeEvent == EventType.BarnFind)
                Debug.Log("[Event] Barn find discovered!");
            ClearEvent();
        }

        private void ClearEvent()
        {
            activeEvent = EventType.None;
        }
    }
}
