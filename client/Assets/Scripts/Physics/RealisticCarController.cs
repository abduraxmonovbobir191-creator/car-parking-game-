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
        [Tooltip("Visual camber in degrees, applied on top of the physics pose in SyncWheelVisuals")]
        public float camberAngle;
    }

    [RequireComponent(typeof(Rigidbody))]
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

        private Rigidbody rb;
        private float throttleInput;
        private float steerInput;
        private float brakeInput;
        private bool handbrakeInput;
        private Vector3 lastPosition;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMassOffset;
            lastPosition = transform.position;

            foreach (var w in wheels)
            {
                if (w.collider != null) w.collider.suspensionDistance = suspensionTravelMeters;
            }
        }

        public void SetInputs(float throttle, float steer, float brake, bool handbrake)
        {
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            brakeInput = Mathf.Clamp01(brake);
            handbrakeInput = handbrake;
        }

        /// <summary>Throttle after clamping, -1..1. Read by sound and HUD code so every car uses its own input.</summary>
        public float CurrentThrottle => throttleInput;

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
                if (w.isPowered)
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
                if (!w.isSteerable) continue;

                float angle = steerInput * dynamicMaxAngle;
                // Ackermann geometry: inner wheel turns sharper than outer wheel
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
                w.collider.motorTorque = w.isPowered ? torquePerWheel : 0f;

                bool isFront = w.collider.transform.localPosition.z > 0;
                float bias = isFront ? brakeBiasFront : (1f - brakeBiasFront);
                float currentBrake = brakeInput * maxBrakeTorque * bias;

                if (handbrakeInput && w.hasHandbrake)
                {
                    currentBrake = handbrakeTorque;
                    AdjustSidewaysGrip(w.collider, driftStiffnessMultiplier);
                }
                else
                {
                    AdjustSidewaysGrip(w.collider, 1.0f);
                }

                w.collider.brakeTorque = currentBrake;
            }
        }

        private void AdjustSidewaysGrip(WheelCollider wc, float stiffness)
        {
            WheelFrictionCurve sFriction = wc.sidewaysFriction;
            sFriction.stiffness = Mathf.Lerp(sFriction.stiffness, stiffness, Time.fixedDeltaTime * 10f);
            wc.sidewaysFriction = sFriction;
        }

        private void SyncWheelVisuals()
        {
            foreach (var w in wheels)
            {
                if (w.visualMesh == null) continue;
                w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                // WheelCollider has no camber, so the tilt is added here instead of writing the mesh transform
                rot *= Quaternion.Euler(0f, 0f, w.camberAngle);
                w.visualMesh.SetPositionAndRotation(pos, rot);
            }
        }
    }
}
