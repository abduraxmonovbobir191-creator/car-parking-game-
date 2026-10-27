using UnityEngine;
using UnityEngine.EventSystems;
using CarParking.Vehicle;

namespace CarParking.UI
{
    /// <summary>
    /// Mobile 120FPS touch + gyroscope controls.
    /// On-screen steering wheel, pressure pedals, clutch, handbrake, gears, diff-lock, winch, signals.
    /// Auto-detects platform and supports gyro tilt steering.
    /// </summary>
    public class MobileTouchAndGyroController : MonoBehaviour
    {
        public static MobileTouchAndGyroController Instance { get; private set; }

        [Header("Mode")]
        public bool forceMobileUI = false;
        public bool useGyroSteering = false;
        public float gyroSensitivity = 2.2f;
        public float gyroDeadzone = 0.08f;

        [Header("Steering Wheel UI")]
        public RectTransform steeringWheelRect;
        public float maxWheelAngle = 120f;
        public float wheelReturnSpeed = 8f;

        [Header("Pedals")]
        public RectTransform gasPedalRect;
        public RectTransform brakePedalRect;
        public RectTransform clutchPedalRect;

        [Header("Runtime Output")]
        public float steerInput;
        public float throttleInput;
        public float brakeInput;
        public float clutchInput = 1f;
        public bool handbrakeInput;
        public bool diffLockInput;

        private bool _isMobile;
        private float _wheelAngle;
        private bool _wheelDragging;
        private int _wheelFingerId = -1;
        private DrivetrainSystem _drivetrain;
        private RealisticVehicleController _vehicle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _isMobile = forceMobileUI || Application.isMobilePlatform;

            if (_isMobile)
            {
                Application.targetFrameRate = 120;
                QualitySettings.vSyncCount = 0;
                Input.gyro.enabled = true;
            }
        }

        private void Start()
        {
            _vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (_vehicle != null)
                _drivetrain = _vehicle.GetComponent<DrivetrainSystem>();
        }

        private void Update()
        {
            if (!_isMobile)
            {
                // Fall back to standard input on PC
                steerInput = Input.GetAxis("Horizontal");
                throttleInput = Mathf.Max(0f, Input.GetAxis("Vertical"));
                brakeInput = Input.GetKey(KeyCode.Space) || Input.GetAxis("Vertical") < 0f ? 1f : 0f;
                return;
            }

            UpdateSteering();
            UpdatePedals();
            ApplyToDrivetrain();
        }

        private void UpdateSteering()
        {
            if (useGyroSteering && SystemInfo.supportsGyroscope)
            {
                Vector3 gravity = Input.gyro.gravity;
                float tilt = -gravity.x;
                if (Mathf.Abs(tilt) < gyroDeadzone) tilt = 0f;
                steerInput = Mathf.Clamp(tilt * gyroSensitivity, -1f, 1f);
                _wheelAngle = steerInput * maxWheelAngle;
            }
            else
            {
                if (!_wheelDragging)
                {
                    _wheelAngle = Mathf.Lerp(_wheelAngle, 0f, Time.deltaTime * wheelReturnSpeed);
                    steerInput = _wheelAngle / maxWheelAngle;
                }
            }

            if (steeringWheelRect != null)
                steeringWheelRect.localRotation = Quaternion.Euler(0f, 0f, -_wheelAngle);
        }

        private void UpdatePedals()
        {
            // Values are set by UI event handlers (OnGas, OnBrake, OnClutch)
            // Keep last value if no touch
        }

        private void ApplyToDrivetrain()
        {
            if (_drivetrain == null) return;
            _drivetrain.throttleInput = throttleInput;
            _drivetrain.clutchInput = clutchInput;
            if (diffLockInput)
                _drivetrain.diffLock = true;
        }

        // --- UI Event Hooks (assign from Unity UI buttons / joysticks) ---

        public void OnSteeringDrag(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            if (pointer == null || steeringWheelRect == null) return;

            _wheelDragging = true;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                steeringWheelRect, pointer.position, pointer.pressEventCamera, out local);

            float angle = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
            _wheelAngle = Mathf.Clamp(angle, -maxWheelAngle, maxWheelAngle);
            steerInput = _wheelAngle / maxWheelAngle;
        }

        public void OnSteeringRelease(BaseEventData data)
        {
            _wheelDragging = false;
        }

        public void OnGasPress() { throttleInput = 1f; }
        public void OnGasRelease() { throttleInput = 0f; }
        public void OnGasDrag(float value) { throttleInput = Mathf.Clamp01(value); }

        public void OnBrakePress() { brakeInput = 1f; }
        public void OnBrakeRelease() { brakeInput = 0f; }
        public void OnBrakeDrag(float value) { brakeInput = Mathf.Clamp01(value); }

        public void OnClutchPress() { clutchInput = 0f; }
        public void OnClutchRelease() { clutchInput = 1f; }

        public void OnHandbrakePress() { handbrakeInput = true; }
        public void OnHandbrakeRelease() { handbrakeInput = false; }

        public void OnShiftUp() { if (_drivetrain != null) _drivetrain.ShiftUp(); }
        public void OnShiftDown() { if (_drivetrain != null) _drivetrain.ShiftDown(); }

        public void OnDiffLockToggle()
        {
            if (_drivetrain != null)
                _drivetrain.diffLock = !_drivetrain.diffLock;
            diffLockInput = _drivetrain != null && _drivetrain.diffLock;
        }

        public void OnToggleGyro()
        {
            useGyroSteering = !useGyroSteering;
        }

        public float GetSteerInput() => steerInput;
        public float GetThrottleInput() => throttleInput;
        public float GetBrakeInput() => brakeInput;
        public bool GetHandbrake() => handbrakeInput;
    }
}
