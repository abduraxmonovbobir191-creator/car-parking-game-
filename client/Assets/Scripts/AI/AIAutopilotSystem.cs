using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.AI
{
    /// <summary>
    /// Tesla-style FSD / AI Autopilot: lane follow, waypoints, speed limits,
    /// obstacle yield, traffic-light style stops. Toggle with key.
    /// </summary>
    public class AIAutopilotSystem : MonoBehaviour
    {
        public static AIAutopilotSystem Instance { get; private set; }

        [Header("Toggle")]
        public KeyCode toggleKey = KeyCode.F12;
        public bool autopilotEnabled;

        [Header("Navigation")]
        public float targetSpeedKmh = 60f;
        public float maxSpeedKmh = 120f;
        public float steerSensitivity = 2.5f;
        public float lookAhead = 25f;
        public float obstacleCheckDistance = 30f;
        public LayerMask obstacleMask;

        [Header("Lane / Waypoints")]
        public Transform[] waypoints;
        public int currentWaypointIndex;
        public float waypointReachRadius = 12f;

        private RealisticVehicleController _vehicle;
        private DrivetrainSystem _drivetrain;
        private Rigidbody _rb;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _vehicle = GetComponent<RealisticVehicleController>();
            if (_vehicle == null)
                _vehicle = FindFirstObjectByType<RealisticVehicleController>();

            if (_vehicle != null)
            {
                _drivetrain = _vehicle.GetComponent<DrivetrainSystem>();
                _rb = _vehicle.GetComponent<Rigidbody>();
            }

            if (waypoints == null || waypoints.Length == 0)
                GenerateDefaultWaypoints();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                autopilotEnabled = !autopilotEnabled;
                Debug.Log(autopilotEnabled ? "[FSD] Autopilot ENGAGED" : "[FSD] Autopilot DISENGAGED");
            }

            if (!autopilotEnabled || _vehicle == null || _rb == null) return;

            FollowPath();
            HandleObstaclesAndSpeed();
        }

        private void GenerateDefaultWaypoints()
        {
            var list = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * Mathf.PI * 2f;
                var go = new GameObject("WP_" + i);
                go.transform.position = new Vector3(Mathf.Cos(angle) * 200f, 0.5f, Mathf.Sin(angle) * 200f);
                go.transform.SetParent(transform);
                list.Add(go.transform);
            }
            waypoints = list.ToArray();
        }

        private void FollowPath()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Transform wp = waypoints[currentWaypointIndex];
            if (wp == null) return;

            Vector3 toWp = wp.position - _vehicle.transform.position;
            toWp.y = 0f;

            if (toWp.magnitude < waypointReachRadius)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                return;
            }

            // Look-ahead point
            Vector3 lookPoint = _vehicle.transform.position + _vehicle.transform.forward * lookAhead;
            Vector3 desiredDir = (wp.position - _vehicle.transform.position).normalized;

            float angle = Vector3.SignedAngle(_vehicle.transform.forward, desiredDir, Vector3.up);
            float steer = Mathf.Clamp(angle / 45f, -1f, 1f) * steerSensitivity;
            steer = Mathf.Clamp(steer, -1f, 1f);

            // Apply via drivetrain throttle; steering is simulated by torque for simplicity
            if (_drivetrain != null)
            {
                float speedKmh = _rb.linearVelocity.magnitude * 3.6f;
                float speedError = targetSpeedKmh - speedKmh;
                _drivetrain.throttleInput = Mathf.Clamp01(speedError / 20f);

                if (speedKmh > targetSpeedKmh + 5f)
                    _drivetrain.throttleInput = 0f;
            }

            // Steering torque
            _rb.AddTorque(_vehicle.transform.up * steer * 15000f * Time.deltaTime, ForceMode.Acceleration);
        }

        private void HandleObstaclesAndSpeed()
        {
            // Raycast ahead for obstacles
            Vector3 origin = _vehicle.transform.position + Vector3.up * 0.8f;
            if (Physics.Raycast(origin, _vehicle.transform.forward, out RaycastHit hit, obstacleCheckDistance, obstacleMask))
            {
                float dist = hit.distance;
                targetSpeedKmh = Mathf.Lerp(5f, maxSpeedKmh, Mathf.Clamp01(dist / obstacleCheckDistance));

                // Yield / stop
                if (dist < 8f && _drivetrain != null)
                    _drivetrain.throttleInput = 0f;
            }
            else
            {
                // Adapt to biome speed limits (city slower)
                targetSpeedKmh = 80f;
                if (_vehicle.wheels != null && _vehicle.wheels.Length > 0 && _vehicle.wheels[0].isGrounded)
                {
                    if (_vehicle.wheels[0].surfaceBiome == BiomeType.City)
                        targetSpeedKmh = 50f;
                }
            }
        }

        public void SetWaypoints(Transform[] wps)
        {
            waypoints = wps;
            currentWaypointIndex = 0;
        }

        public bool IsEngaged => autopilotEnabled;
    }
}
