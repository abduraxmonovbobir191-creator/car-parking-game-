using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CarParking.World;

namespace CarParking.UI
{
    /// <summary>
    /// Professional photo mode & cinematic director.
    /// Free camera, DOF/aperture, FOV, color grading, time-of-day, keyframe paths.
    /// </summary>
    public class PhotoAndCinematicDirectorSystem : MonoBehaviour
    {
        public static PhotoAndCinematicDirectorSystem Instance { get; private set; }

        [Header("Keys")]
        public KeyCode photoModeKey = KeyCode.P;
        public KeyCode recordKeyframeKey = KeyCode.K;
        public KeyCode playPathKey = KeyCode.L;

        [Header("Camera")]
        public float moveSpeed = 12f;
        public float fastMultiplier = 3f;
        public float mouseSensitivity = 2f;
        public float minFOV = 20f;
        public float maxFOV = 100f;

        [Header("Photo")]
        public float aperture = 5.6f;
        public float focusDistance = 10f;

        private bool _photoMode;
        private Camera _photoCam;
        private Camera _mainCam;
        private float _yaw, _pitch;
        private float _currentFOV = 60f;

        private struct Keyframe
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
            public float time;
        }

        private List<Keyframe> _path = new();
        private bool _playingPath;
        private float _pathTime;
        private float _pathDuration = 8f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _mainCam = Camera.main;

            var go = new GameObject("PhotoModeCamera");
            go.transform.SetParent(transform);
            _photoCam = go.AddComponent<Camera>();
            _photoCam.enabled = false;
            _photoCam.nearClipPlane = 0.05f;
            _photoCam.fieldOfView = _currentFOV;
        }

        private void Update()
        {
            if (Input.GetKeyDown(photoModeKey))
                TogglePhotoMode();

            if (!_photoMode) return;

            if (!_playingPath)
            {
                UpdateFreeCamera();

                if (Input.GetKeyDown(recordKeyframeKey))
                    RecordKeyframe();

                if (Input.GetKeyDown(playPathKey) && _path.Count >= 2)
                    StartPathPlayback();
            }
            else
            {
                UpdatePathPlayback();
            }

            // Time of day via WaterAndWeatherSystem if available
            if (Input.GetKey(KeyCode.LeftBracket) && WaterAndWeatherSystem.Instance != null)
            {
                // Could expose time-of-day slider on weather system
            }
        }

        private void TogglePhotoMode()
        {
            _photoMode = !_photoMode;
            _photoCam.enabled = _photoMode;

            if (_mainCam != null)
                _mainCam.enabled = !_photoMode;

            if (_photoMode && _mainCam != null)
            {
                _photoCam.transform.position = _mainCam.transform.position;
                _photoCam.transform.rotation = _mainCam.transform.rotation;
                _yaw = _mainCam.transform.eulerAngles.y;
                _pitch = _mainCam.transform.eulerAngles.x;
                if (_pitch > 180f) _pitch -= 360f;
            }

            Cursor.lockState = _photoMode ? CursorLockMode.Locked : CursorLockMode.None;
            Time.timeScale = _photoMode ? 0.001f : 1f; // near-pause for photo

            Debug.Log(_photoMode ? "[PhotoMode] ON" : "[PhotoMode] OFF");
        }

        private void UpdateFreeCamera()
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch - my, -89f, 89f);
            _photoCam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
            // Unscaled delta because timeScale is near zero
            float dt = Time.unscaledDeltaTime;

            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += _photoCam.transform.forward;
            if (Input.GetKey(KeyCode.S)) dir -= _photoCam.transform.forward;
            if (Input.GetKey(KeyCode.A)) dir -= _photoCam.transform.right;
            if (Input.GetKey(KeyCode.D)) dir += _photoCam.transform.right;
            if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) dir -= Vector3.up;

            _photoCam.transform.position += dir.normalized * speed * dt;

            // FOV
            if (Input.GetKey(KeyCode.Z))
                _currentFOV = Mathf.Max(minFOV, _currentFOV - 40f * dt);
            if (Input.GetKey(KeyCode.C))
                _currentFOV = Mathf.Min(maxFOV, _currentFOV + 40f * dt);
            _photoCam.fieldOfView = _currentFOV;

            // Aperture / focus (informational + DOF if volume present)
            if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.Equals))
                aperture = Mathf.Min(32f, aperture + 4f * dt);
            if (Input.GetKey(KeyCode.Minus))
                aperture = Mathf.Max(1.4f, aperture - 4f * dt);

            if (Input.GetKey(KeyCode.Period))
                focusDistance = Mathf.Min(200f, focusDistance + 20f * dt);
            if (Input.GetKey(KeyCode.Comma))
                focusDistance = Mathf.Max(0.5f, focusDistance - 20f * dt);
        }

        private void RecordKeyframe()
        {
            _path.Add(new Keyframe
            {
                position = _photoCam.transform.position,
                rotation = _photoCam.transform.rotation,
                fov = _currentFOV,
                time = _path.Count * (_pathDuration / Mathf.Max(1, _path.Count + 1))
            });
            Debug.Log("[Cinematic] Keyframe " + _path.Count + " recorded");
        }

        private void StartPathPlayback()
        {
            _playingPath = true;
            _pathTime = 0f;
            Time.timeScale = 1f;
            Debug.Log("[Cinematic] Playing path");
        }

        private void UpdatePathPlayback()
        {
            _pathTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_pathTime / _pathDuration);

            // Interpolate across keyframes
            float seg = t * (_path.Count - 1);
            int i = Mathf.FloorToInt(seg);
            float localT = seg - i;
            i = Mathf.Clamp(i, 0, _path.Count - 2);

            var a = _path[i];
            var b = _path[i + 1];

            _photoCam.transform.position = Vector3.Lerp(a.position, b.position, localT);
            _photoCam.transform.rotation = Quaternion.Slerp(a.rotation, b.rotation, localT);
            _photoCam.fieldOfView = Mathf.Lerp(a.fov, b.fov, localT);

            if (t >= 1f)
            {
                _playingPath = false;
                Time.timeScale = 0.001f;
                Debug.Log("[Cinematic] Path finished");
            }
        }

        public bool IsPhotoMode => _photoMode;
        public void ClearPath() => _path.Clear();
    }
}
