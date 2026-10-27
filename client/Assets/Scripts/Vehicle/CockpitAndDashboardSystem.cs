using UnityEngine;
using CarParking.World;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Interactive 1st-person cockpit: steering wheel, gauges, signals, lights, wipers and mirrors.
    /// </summary>
    public class CockpitAndDashboardSystem : MonoBehaviour
    {
        [Header("Steering")]
        public Transform steeringWheel;
        public float maxSteerAngle = 900f;
        public float steerDamping = 8f;

        [Header("Pedals & Shifter")]
        public Transform gasPedal;
        public Transform brakePedal;
        public Transform clutchPedal;
        public Transform gearStick;

        [Header("Gauges")]
        public Transform rpmNeedle;
        public Transform speedNeedle;
        public Transform boostNeedle;
        public Transform tempNeedle;
        public Transform fuelNeedle;
        public TextMesh gearText;
        public GameObject shiftLight;
        public GameObject diffLockIcon;

        [Header("Lights & Signals")]
        public Light[] headlightsLow;
        public Light[] headlightsHigh;
        public Light[] turnSignalsLeft;
        public Light[] turnSignalsRight;
        public Light[] interiorLights;
        public float signalBlinkRate = 0.4f;

        [Header("Wipers")]
        public Transform[] wiperBlades;
        public float maxWiperAngle = 90f;

        [Header("Mirrors")]
        public Camera rearMirrorCam;
        public Camera leftMirrorCam;
        public Camera rightMirrorCam;

        private DrivetrainSystem _drivetrain;
        private RealisticVehicleController _vehicle;
        private float _currentSteerAngle;
        private bool _leftSignal, _rightSignal, _hazard;
        private float _signalTimer;
        private bool _signalState;
        private bool _highBeam;
        private float _wiperPhase;

        private void Awake()
        {
            _drivetrain = GetComponent<DrivetrainSystem>();
            _vehicle = GetComponent<RealisticVehicleController>();
        }

        private void Update()
        {
            UpdateSteeringWheel();
            UpdatePedals();
            UpdateGauges();
            UpdateSignalsAndLights();
            UpdateWipers();
            UpdateMirrors();
        }

        private void UpdateSteeringWheel()
        {
            if (steeringWheel == null) return;

            float input = Input.GetAxis("Horizontal");
            float target = input * maxSteerAngle * 0.5f;
            float speedFactor = 1f;

            if (_vehicle != null)
            {
                var rb = _vehicle.GetComponent<Rigidbody>();
                if (rb != null)
                    speedFactor = Mathf.Clamp01(1f - rb.linearVelocity.magnitude / 40f);
            }

            _currentSteerAngle = Mathf.Lerp(_currentSteerAngle, target * (0.5f + speedFactor * 0.5f), Time.deltaTime * steerDamping);
            steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -_currentSteerAngle);
        }

        private void UpdatePedals()
        {
            float throttle = _drivetrain != null ? _drivetrain.throttleInput : Input.GetAxis("Vertical");
            float brake = Input.GetKey(KeyCode.Space) || Input.GetAxis("Vertical") < -0.1f ? 1f : 0f;
            float clutch = _drivetrain != null ? 1f - _drivetrain.clutchInput : 0f;

            if (gasPedal) gasPedal.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 25f, throttle), 0f, 0f);
            if (brakePedal) brakePedal.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 20f, brake), 0f, 0f);
            if (clutchPedal) clutchPedal.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 22f, clutch), 0f, 0f);
        }

        private void UpdateGauges()
        {
            if (_drivetrain == null) return;

            float rpmNorm = Mathf.Clamp01(_drivetrain.currentRpm / _drivetrain.maxRpm);
            float speedKmh = 0f;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) speedKmh = rb.linearVelocity.magnitude * 3.6f;

            if (rpmNeedle) rpmNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(120f, -120f, rpmNorm));
            if (speedNeedle) speedNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(120f, -120f, Mathf.Clamp01(speedKmh / 280f)));
            if (boostNeedle) boostNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(90f, -90f, (_drivetrain.turboMaxBoost - 1f) / 1.5f));

            if (gearText) gearText.text = _drivetrain.currentGear == -1 ? "R" : _drivetrain.currentGear == 0 ? "N" : _drivetrain.currentGear.ToString();

            if (shiftLight) shiftLight.SetActive(rpmNorm > 0.92f && Mathf.Sin(Time.time * 20f) > 0f);
            if (diffLockIcon) diffLockIcon.SetActive(_drivetrain.diffLock);
        }

        private void UpdateSignalsAndLights()
        {
            if (Input.GetKeyDown(KeyCode.Q)) { _leftSignal = !_leftSignal; _rightSignal = false; _hazard = false; }
            if (Input.GetKeyDown(KeyCode.E)) { _rightSignal = !_rightSignal; _leftSignal = false; _hazard = false; }
            if (Input.GetKeyDown(KeyCode.X)) { _hazard = !_hazard; _leftSignal = _rightSignal = false; }
            if (Input.GetKeyDown(KeyCode.L)) _highBeam = !_highBeam;

            _signalTimer += Time.deltaTime;
            if (_signalTimer >= signalBlinkRate)
            {
                _signalTimer = 0f;
                _signalState = !_signalState;
            }

            bool leftOn = (_leftSignal || _hazard) && _signalState;
            bool rightOn = (_rightSignal || _hazard) && _signalState;

            SetLights(turnSignalsLeft, leftOn);
            SetLights(turnSignalsRight, rightOn);
            SetLights(headlightsLow, true);
            SetLights(headlightsHigh, _highBeam);
        }

        private void SetLights(Light[] lights, bool on)
        {
            if (lights == null) return;
            foreach (var l in lights)
                if (l != null) l.enabled = on;
        }

        private void UpdateWipers()
        {
            if (wiperBlades == null || wiperBlades.Length == 0) return;

            float intensity = 0f;
            if (WaterAndWeatherSystem.Instance != null)
                intensity = WaterAndWeatherSystem.Instance.rainIntensity;

            if (intensity > 0.05f || Input.GetKey(KeyCode.W))
            {
                _wiperPhase += Time.deltaTime * Mathf.Lerp(2f, 6f, intensity);
                float angle = Mathf.Sin(_wiperPhase) * maxWiperAngle;

                foreach (var w in wiperBlades)
                    if (w != null) w.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        private void UpdateMirrors()
        {
            // Cameras are assumed to be already positioned correctly on the vehicle.
            // Just keep them enabled when in cockpit view.
            bool cockpitActive = true; // can be driven by camera mode later
            if (rearMirrorCam) rearMirrorCam.enabled = cockpitActive;
            if (leftMirrorCam) leftMirrorCam.enabled = cockpitActive;
            if (rightMirrorCam) rightMirrorCam.enabled = cockpitActive;
        }
    }
}
