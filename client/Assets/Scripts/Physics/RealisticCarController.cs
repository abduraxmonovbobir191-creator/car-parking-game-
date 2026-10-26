using System;
using UnityEngine;

namespace NextGenDrive.Physics
{
    [Serializable]
    public class WheelData
    {
        public string wheelName;
        public WheelCollider collider;
        public Transform visualMesh;
        public bool isSteerable;
        public bool isPowered;
        public bool hasHandbrake;
        [HideInInspector] public float camberAngle;      // unsigned; SyncWheelVisuals applies the side sign
        [HideInInspector] public float baseForwardStiffness = 1f;
        [HideInInspector] public float baseSidewaysStiffness = 1f;
    }

    public class RealisticCarController : MonoBehaviour
    {
        [Header("Vehicle Identity (VIN)")]
        public string vehicleVIN;
        public float odometerKm = 0f;

        [Header("Engine & Transmission")]
        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(1000, 280),
            new Keyframe(4500, 650),
            new Keyframe(7000, 580),
            new Keyframe(8500, 0)
        );
        public float maxRPM = 8200f;
        public float idleRPM = 900f;
        public float currentRPM;
        public float[] gearRatios = { -3.2f, 0f, 3.6f, 2.19f, 1.41f, 1.12f, 0.89f, 0.72f };
        public float finalDriveRatio = 3.42f;
        public int currentGear = 2; // 0 = R, 1 = N, 2 = 1st
        public float shiftTime = 0.18f;
        private bool isShifting = false;

        [Header("Steering & Aerodynamics")]
        public float maxSteerAngle = 38f;
        public float ackermannFactor = 0.18f;
        public float downforceCoefficient = 1.85f;
        public float dragCoefficient = 0.31f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0.05f);

        [Header("Braking & Drift")]
        public float maxBrakeTorque = 4200f;
        public float handbrakeTorque = 6500f;
        [Range(0f, 1f)] public float brakeBiasFront = 0.65f;
        public float driftStiffnessMultiplier = 0.55f;

        [Header("Wheels")]
        public WheelData[] wheels;
        [Tooltip("Fixed suspension travel in meters; ride height is set through the spring's target position")]
        public float suspensionTravelMeters = 0.30f;

        [Header("Surface")]
        [Tooltip("Set by WeatherAndSurfaceManager: 1 = dry asphalt, lower on wet roads")]
        [Range(0.1f, 1f)] public float surfaceGripMultiplier = 1f;

        private Rigidbody rb;
        private float throttleInput;
        private float steerInput;
        private float brakeInput;
        private bool handbrakeInput;
        private Vector3 lastPosition;

        // Public getters so Audio/HUD/Network scripts never call Input.GetAxis directly
        public float CurrentThrottle => throttleInput;
        public float CurrentSteer => steerInput;
        public float CurrentBrake => brakeInput;
        public bool IsHandbrakeEngaged => handbrakeInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMassOffset;
            lastPosition = transform.position;

            foreach (var w in wheels)
            {
                if (w.collider == null) continue;
                w.collider.suspensionDistance = suspensionTravelMeters;
                w.baseForwardStiffness = w.collider.forwardFriction.stiffness;
                w.baseSidewaysStiffness = w.collider.sidewaysFriction.stiffness;
            }
        }

        public void SetInputs(float throttle, float steer, float brake, bool handbrake)
        {
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            brakeInput = Mathf.Clamp01(brake);
            handbrakeInput = handbrake;
        }

        private void FixedUpdate()
        {
            UpdateOdometer();
            ApplyAerodynamics();
            CalculateEngineRPM();
            AutoGearboxLogic();
            ApplySteering();
            ApplyDriveAndBrakes();
            SyncWheelVisuals();
        }

        private void UpdateOdometer()
        {
            float distanceMeters = Vector3.Distance(transform.position, lastPosition);
            odometerKm += distanceMeters / 1000f;
            lastPosition = transform.position;
        }

        private void ApplyAerodynamics()
        {
            float speedSq = rb.linearVelocity.sqrMagnitude;
            Vector3 dragForce = -rb.linearVelocity.normalized * (0.5f * 1.225f * dragCoefficient * speedSq);
            Vector3 downforce = -transform.up * (downforceCoefficient * speedSq);
            rb.AddForce(dragForce + downforce, ForceMode.Force);
        }

        private void CalculateEngineRPM()
        {
            float avgWheelRPM = 0f;
            int poweredCount = 0;

            foreach (var w in wheels)
            {
                if (w.isPowered && w.collider != null)
                {
                    avgWheelRPM += w.collider.rpm;
                    poweredCount++;
                }
            }

            if (poweredCount > 0) avgWheelRPM /= poweredCount;

            float gearRatio = gearRatios[currentGear];
            if (Mathf.Abs(gearRatio) > 0.01f)
            {
                float targetRPM = Mathf.Abs(avgWheelRPM * gearRatio * finalDriveRatio);
                currentRPM = Mathf.Lerp(currentRPM, Mathf.Clamp(targetRPM, idleRPM, maxRPM), Time.fixedDeltaTime * 12f);
            }
            else
            {
                currentRPM = Mathf.Lerp(currentRPM, idleRPM + (Mathf.Abs(throttleInput) * (maxRPM - idleRPM)), Time.fixedDeltaTime * 8f);
            }
        }

        private void AutoGearboxLogic()
        {
            if (isShifting) return;

            if (currentRPM > maxRPM * 0.91f && currentGear < gearRatios.Length - 1 && currentGear >= 2)
            {
                StartCoroutine(ShiftGear(currentGear + 1));
            }
            else if (currentRPM < maxRPM * 0.35f && currentGear > 2)
            {
                StartCoroutine(ShiftGear(currentGear - 1));
            }
        }

        private System.Collections.IEnumerator ShiftGear(int targetGear)
        {
            isShifting = true;
            yield return new WaitForSeconds(shiftTime);
            currentGear = targetGear;
            isShifting = false;
        }

        private void ApplySteering()
        {
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / 70f);
            float dynamicMaxAngle = Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.28f, speedFactor);

            foreach (var w in wheels)
            {
                if (!w.isSteerable || w.collider == null) continue;

                float angle = steerInput * dynamicMaxAngle;
                if (steerInput > 0 && w.collider.transform.localPosition.x > 0)
                    angle *= (1f + ackermannFactor);
                else if (steerInput < 0 && w.collider.transform.localPosition.x < 0)
                    angle *= (1f + ackermannFactor);

                w.collider.steerAngle = Mathf.Lerp(w.collider.steerAngle, angle, Time.fixedDeltaTime * 15f);
            }
        }

        private void ApplyDriveAndBrakes()
        {
            int poweredWheels = 0;
            foreach (var w in wheels) if (w.isPowered) poweredWheels++;

            float engineTorque = isShifting ? 0f : torqueCurve.Evaluate(currentRPM) * throttleInput;
            float totalWheelTorque = engineTorque * gearRatios[currentGear] * finalDriveRatio;
            float torquePerWheel = poweredWheels > 0 ? totalWheelTorque / poweredWheels : 0f;

            foreach (var w in wheels)
            {
                if (w.collider == null) continue;
                w.collider.motorTorque = w.isPowered ? torquePerWheel : 0f;

                bool isFront = w.collider.transform.localPosition.z > 0;
                float bias = isFront ? brakeBiasFront : (1f - brakeBiasFront);
                float currentBrake = brakeInput * maxBrakeTorque * bias;

                if (handbrakeInput && w.hasHandbrake)
                {
                    currentBrake = handbrakeTorque;
                    AdjustGrip(w, driftStiffnessMultiplier);
                }
                else
                {
                    AdjustGrip(w, 1.0f);
                }

                w.collider.brakeTorque = currentBrake;
            }
        }

        // Forward and sideways stiffness both scale with the surface, so wet roads cut grip in every direction
        private void AdjustGrip(WheelData w, float sidewaysFactor)
        {
            WheelCollider wc = w.collider;

            WheelFrictionCurve forward = wc.forwardFriction;
            forward.stiffness = w.baseForwardStiffness * surfaceGripMultiplier;
            wc.forwardFriction = forward;

            WheelFrictionCurve sideways = wc.sidewaysFriction;
            float target = w.baseSidewaysStiffness * sidewaysFactor * surfaceGripMultiplier;
            sideways.stiffness = Mathf.Lerp(sideways.stiffness, target, Time.fixedDeltaTime * 10f);
            wc.sidewaysFriction = sideways;
        }

        private void SyncWheelVisuals()
        {
            foreach (var w in wheels)
            {
                if (w.visualMesh == null || w.collider == null) continue;

                w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);

                // Camber in wheel-local space: the tilt follows steering and rolling, so the axis never wobbles
                float sideSign = w.collider.transform.localPosition.x >= 0f ? 1f : -1f;
                w.visualMesh.SetPositionAndRotation(pos, rot * Quaternion.Euler(0f, 0f, w.camberAngle * sideSign));
            }
        }
    }
}
