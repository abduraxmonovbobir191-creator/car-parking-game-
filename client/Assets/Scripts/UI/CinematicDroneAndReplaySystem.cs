using System.Collections.Generic;
using UnityEngine;
using CarParking.Vehicle;

namespace CarParking.UI
{
    /// <summary>
    /// Cinematic FPV drone camera + 30-second rolling replay buffer.
    /// </summary>
    public class CinematicDroneAndReplaySystem : MonoBehaviour
    {
        public static CinematicDroneAndReplaySystem Instance { get; private set; }

        [Header("Drone")]
        public KeyCode droneToggleKey = KeyCode.F7;
        public float droneSpeed = 25f;
        public float droneFastMultiplier = 2.5f;
        public float droneFOV = 80f;
        public float mouseSensitivity = 2f;

        [Header("Replay")]
        public KeyCode replayKey = KeyCode.F8;
        public float replayDuration = 30f;
        public int replayFps = 30;

        private Camera _droneCam;
        private bool _droneActive;
        private float _yaw, _pitch;
        private Camera _mainCam;

        private struct ReplayFrame
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public float time;
        }

        private List<ReplayFrame> _buffer = new();
        private int _maxFrames;
        private bool _isReplaying;
        private int _replayIndex;
        private Transform _replayTarget;
        private Rigidbody _replayRb;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _maxFrames = Mathf.RoundToInt(replayDuration * replayFps);
        }

        private void Start()
        {
            _mainCam = Camera.main;

            var go = new GameObject("CinematicDroneCam");
            go.transform.SetParent(transform);
            _droneCam = go.AddComponent<Camera>();
            _droneCam.enabled = false;
            _droneCam.fieldOfView = droneFOV;
            _droneCam.nearClipPlane = 0.15f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(droneToggleKey))
                ToggleDrone();

            if (Input.GetKeyDown(replayKey) && !_isReplaying)
                StartReplay();

            if (_droneActive)
                UpdateDroneMovement();

            if (!_isReplaying)
                RecordFrame();
            else
                UpdateReplay();
        }

        private void ToggleDrone()
        {
            _droneActive = !_droneActive;
            _droneCam.enabled = _droneActive;

            if (_mainCam != null)
                _mainCam.enabled = !_droneActive;

            if (_droneActive && _mainCam != null)
            {
                _droneCam.transform.position = _mainCam.transform.position + Vector3.up * 5f;
                _yaw = _mainCam.transform.eulerAngles.y;
                _pitch = 20f;
            }

            Cursor.lockState = _droneActive ? CursorLockMode.Locked : CursorLockMode.None;
            Debug.Log(_droneActive ? "[Drone] FPV ON" : "[Drone] FPV OFF");
        }

        private void UpdateDroneMovement()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            _yaw += mouseX;
            _pitch = Mathf.Clamp(_pitch - mouseY, -85f, 85f);
            _droneCam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            float speed = droneSpeed * (Input.GetKey(KeyCode.LeftShift) ? droneFastMultiplier : 1f);
            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += _droneCam.transform.forward;
            if (Input.GetKey(KeyCode.S)) dir -= _droneCam.transform.forward;
            if (Input.GetKey(KeyCode.A)) dir -= _droneCam.transform.right;
            if (Input.GetKey(KeyCode.D)) dir += _droneCam.transform.right;
            if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) dir -= Vector3.up;

            _droneCam.transform.position += dir.normalized * speed * Time.deltaTime;

            // FOV adjust
            if (Input.GetKey(KeyCode.Z))
                _droneCam.fieldOfView = Mathf.Max(30f, _droneCam.fieldOfView - 30f * Time.deltaTime);
            if (Input.GetKey(KeyCode.C))
                _droneCam.fieldOfView = Mathf.Min(110f, _droneCam.fieldOfView + 30f * Time.deltaTime);
        }

        private void RecordFrame()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle == null) return;

            var rb = vehicle.GetComponent<Rigidbody>();
            var frame = new ReplayFrame
            {
                position = vehicle.transform.position,
                rotation = vehicle.transform.rotation,
                velocity = rb != null ? rb.linearVelocity : Vector3.zero,
                time = Time.time
            };

            _buffer.Add(frame);
            if (_buffer.Count > _maxFrames)
                _buffer.RemoveAt(0);
        }

        private void StartReplay()
        {
            if (_buffer.Count < 10) return;

            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle == null) return;

            _replayTarget = vehicle.transform;
            _replayRb = vehicle.GetComponent<Rigidbody>();
            if (_replayRb != null) _replayRb.isKinematic = true;

            _isReplaying = true;
            _replayIndex = 0;

            // Switch to cinematic angle
            if (_droneCam != null && !_droneActive)
            {
                _droneCam.enabled = true;
                if (_mainCam != null) _mainCam.enabled = false;
            }

            Debug.Log("[Replay] Playing last " + replayDuration + " seconds");
        }

        private void UpdateReplay()
        {
            if (_replayIndex >= _buffer.Count)
            {
                EndReplay();
                return;
            }

            var frame = _buffer[_replayIndex];
            if (_replayTarget != null)
                _replayTarget.SetPositionAndRotation(frame.position, frame.rotation);

            // Cinematic camera follow
            if (_droneCam != null && _droneCam.enabled)
            {
                Vector3 camPos = frame.position - frame.rotation * Vector3.forward * 8f + Vector3.up * 3f;
                _droneCam.transform.position = Vector3.Lerp(_droneCam.transform.position, camPos, Time.deltaTime * 5f);
                _droneCam.transform.LookAt(frame.position + Vector3.up * 1f);
            }

            _replayIndex++;
        }

        private void EndReplay()
        {
            _isReplaying = false;
            if (_replayRb != null) _replayRb.isKinematic = false;

            if (!_droneActive && _droneCam != null)
            {
                _droneCam.enabled = false;
                if (_mainCam != null) _mainCam.enabled = true;
            }

            Debug.Log("[Replay] Finished");
        }

        public bool IsDroneActive => _droneActive;
        public bool IsReplaying => _isReplaying;
    }
}
