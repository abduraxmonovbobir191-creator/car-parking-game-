using UnityEngine;

namespace CarParking.Racing
{
    /// <summary>
    /// MODULE 46 — Rally Co-Driver Pacenote Synthesizer
    /// Real-time curvature and elevation calls.
    /// </summary>
    public class RallyCoDriverPacenoteSystem : MonoBehaviour
    {
        public static RallyCoDriverPacenoteSystem Instance { get; private set; }

        public float lookAheadDistance = 40f;
        public string lastCall = "";
        public float callCooldown = 1.2f;
        private float _timer;

        private Transform _car;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
            if (v != null) _car = v.transform;
        }

        private void Update()
        {
            if (_car == null) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            Vector3 ahead = _car.position + _car.forward * lookAheadDistance;
            float angle = Vector3.SignedAngle(_car.forward, ahead - _car.position, Vector3.up);
            float severity = Mathf.Abs(angle) / 15f;

            string note = severity switch
            {
                < 0.5f => "Straight",
                < 1.2f => angle > 0 ? "Right 1" : "Left 1",
                < 2.0f => angle > 0 ? "Right 3" : "Left 3",
                < 3.0f => angle > 0 ? "Right 5" : "Left 5",
                _ => angle > 0 ? "Hairpin Right" : "Hairpin Left"
            };

            if (note != lastCall)
            {
                lastCall = note;
                _timer = callCooldown;
                Debug.Log($"[Pacenote] {note}");
            }
        }

        public string GetCurrentNote() => lastCall;
    }
}
