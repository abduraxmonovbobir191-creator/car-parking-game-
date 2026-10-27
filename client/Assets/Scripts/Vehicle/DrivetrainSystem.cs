using UnityEngine;

namespace CarParking.Vehicle
{
    public enum DriveType { FWD, RWD, AWD }
    public enum GearboxType { Automatic, Manual }

    /// <summary>
    /// Realistic drivetrain: engine torque curve, turbo, gearbox (manual + auto),
    /// AWD/RWD/FWD, lockable differentials.
    /// </summary>
    public class DrivetrainSystem : MonoBehaviour
    {
        [Header("Engine")]
        public float maxRpm = 7200f;
        public float idleRpm = 850f;
        public AnimationCurve torqueCurve; // x = normalized RPM (0-1), y = torque Nm
        public float maxTorque = 480f;
        public float engineInertia = 0.25f;

        [Header("Turbo")]
        public bool hasTurbo = true;
        public float turboMaxBoost = 1.4f;
        public float turboSpoolRate = 2.5f;

        [Header("Gearbox")]
        public GearboxType gearboxType = GearboxType.Automatic;
        public float[] gearRatios = { 3.5f, 2.2f, 1.5f, 1.1f, 0.85f, 0.7f };
        public float finalDrive = 3.7f;
        public float reverseRatio = 3.2f;

        [Header("Drive")]
        public DriveType driveType = DriveType.AWD;
        public bool diffLock = false;

        [Header("Runtime")]
        public float currentRpm;
        public int currentGear = 1;
        public float throttleInput;
        public float clutchInput = 1f; // 0 = fully pressed

        private float _turboBoost = 1f;
        private float _engineAngularVel;
        private RealisticVehicleController _vehicle;

        private void Awake()
        {
            _vehicle = GetComponent<RealisticVehicleController>();
            if (torqueCurve == null || torqueCurve.keys.Length == 0)
            {
                torqueCurve = new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.3f, 0.85f),
                    new Keyframe(0.55f, 1f),
                    new Keyframe(0.8f, 0.92f),
                    new Keyframe(1f, 0.7f)
                );
            }
        }

        private void Update()
        {
            throttleInput = Input.GetAxis("Vertical");
            if (throttleInput < 0f) throttleInput = 0f; // braking handled separately

            if (gearboxType == GearboxType.Manual)
            {
                if (Input.GetKeyDown(KeyCode.LeftShift)) ShiftUp();
                if (Input.GetKeyDown(KeyCode.LeftControl)) ShiftDown();
                clutchInput = Input.GetKey(KeyCode.C) ? 0f : 1f;
            }

            if (Input.GetKeyDown(KeyCode.L))
                diffLock = !diffLock;
        }

        private void FixedUpdate()
        {
            UpdateEngine();
            if (gearboxType == GearboxType.Automatic)
                AutomaticShiftLogic();
        }

        private void UpdateEngine()
        {
            float targetRpm = idleRpm + (maxRpm - idleRpm) * throttleInput;
            float gearRatio = GetCurrentGearRatio();

            // Simple RPM integration
            float engineTorque = torqueCurve.Evaluate(currentRpm / maxRpm) * maxTorque * throttleInput;
            if (hasTurbo)
            {
                _turboBoost = Mathf.MoveTowards(_turboBoost, 1f + (turboMaxBoost - 1f) * throttleInput, turboSpoolRate * Time.fixedDeltaTime);
                engineTorque *= _turboBoost;
            }

            _engineAngularVel += (engineTorque / engineInertia) * Time.fixedDeltaTime;
            currentRpm = _engineAngularVel * 60f / (2f * Mathf.PI);
            currentRpm = Mathf.Clamp(currentRpm, idleRpm * 0.8f, maxRpm * 1.05f);

            // Engine braking when throttle is low
            if (throttleInput < 0.1f)
                currentRpm = Mathf.Lerp(currentRpm, idleRpm, Time.fixedDeltaTime * 2f);
        }

        public float GetWheelTorque(RealisticVehicleController.Wheel wheel)
        {
            if (!wheel.isDriveWheel) return 0f;

            float gearRatio = GetCurrentGearRatio();
            float engineTorque = torqueCurve.Evaluate(currentRpm / maxRpm) * maxTorque * throttleInput * _turboBoost;
            float wheelTorque = engineTorque * gearRatio * finalDrive * clutchInput;

            // Simple open/locked diff distribution
            int driveWheels = 0;
            foreach (var w in _vehicle.wheels)
                if (w.isDriveWheel) driveWheels++;

            if (driveWheels == 0) return 0f;

            float share = 1f / driveWheels;
            if (diffLock) share = 1f / driveWheels; // equal
            else share *= 0.9f + Random.Range(0f, 0.2f); // open diff variation

            return wheelTorque * share;
        }

        private float GetCurrentGearRatio()
        {
            if (currentGear == -1) return -reverseRatio;
            if (currentGear <= 0 || currentGear > gearRatios.Length) return 0f;
            return gearRatios[currentGear - 1];
        }

        private void AutomaticShiftLogic()
        {
            if (currentRpm > maxRpm * 0.92f && currentGear < gearRatios.Length)
                ShiftUp();
            else if (currentRpm < maxRpm * 0.4f && currentGear > 1)
                ShiftDown();
        }

        public void ShiftUp()
        {
            if (currentGear < gearRatios.Length)
                currentGear++;
        }

        public void ShiftDown()
        {
            if (currentGear > -1)
                currentGear--;
        }
    }
}
