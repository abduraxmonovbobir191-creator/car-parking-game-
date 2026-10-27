using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.World
{
    /// <summary>
    /// Advanced hydrodynamics: water current drift, buoyancy refinement,
    /// boat/jet-ski style controls when floating, and aerodynamic downforce.
    /// </summary>
    public class AdvancedHydrodynamicsSystem : MonoBehaviour
    {
        public static AdvancedHydrodynamicsSystem Instance { get; private set; }

        [Header("Water")]
        public float waterLevel = 0f;
        public float currentStrength = 4f;
        public Vector3 currentDirection = new Vector3(1f, 0f, 0.3f);
        public float buoyancyForce = 18000f;
        public float waterDrag = 2.5f;

        [Header("Boat / Jet-Ski")]
        public float boatAcceleration = 25f;
        public float boatTurnTorque = 12000f;

        [Header("Aerodynamics")]
        public float downforceCoefficient = 0.8f;
        public float dragCoefficient = 0.35f;
        public float referenceArea = 2.2f;
        public float airDensity = 1.225f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            currentDirection.Normalize();
        }

        private void FixedUpdate()
        {
            var vehicles = FindObjectsByType<RealisticVehicleController>(FindObjectsSortMode.None);
            foreach (var v in vehicles)
            {
                var rb = v.GetComponent<Rigidbody>();
                if (rb == null) continue;

                ApplyAerodynamics(rb, v.transform);
                ApplyWaterForces(rb, v);
            }
        }

        private void ApplyAerodynamics(Rigidbody rb, Transform t)
        {
            Vector3 vel = rb.linearVelocity;
            float speed = vel.magnitude;
            if (speed < 1f) return;

            // Drag opposite to velocity
            float dragForce = 0.5f * airDensity * speed * speed * dragCoefficient * referenceArea;
            rb.AddForce(-vel.normalized * dragForce, ForceMode.Force);

            // Downforce proportional to speed squared (presses car to road)
            float downforce = 0.5f * airDensity * speed * speed * downforceCoefficient * referenceArea;
            rb.AddForce(-t.up * downforce, ForceMode.Force);
        }

        private void ApplyWaterForces(Rigidbody rb, RealisticVehicleController vehicle)
        {
            float floodExtra = 0f;
            if (WeatherDisasterSystem.Instance != null)
                floodExtra = WeatherDisasterSystem.Instance.CurrentFloodHeight;

            float effectiveWaterLevel = waterLevel + floodExtra;
            Vector3 pos = rb.position;

            // Sample approximate submersion from vehicle center and corners
            float submersion = effectiveWaterLevel - (pos.y - 0.5f);
            if (submersion <= 0f) return;

            float subFactor = Mathf.Clamp01(submersion / 2f);

            // Buoyancy
            rb.AddForce(Vector3.up * buoyancyForce * subFactor, ForceMode.Force);

            // Water drag
            rb.AddForce(-rb.linearVelocity * waterDrag * subFactor, ForceMode.Force);

            // Current drift
            rb.AddForce(currentDirection * currentStrength * subFactor * rb.mass * 0.15f, ForceMode.Force);

            // Boat-like controls when mostly floating
            if (subFactor > 0.55f)
            {
                float throttle = 0f;
                float steer = Input.GetAxis("Horizontal");
                var dt = vehicle.GetComponent<DrivetrainSystem>();
                if (dt != null) throttle = dt.throttleInput;
                else throttle = Mathf.Max(0f, Input.GetAxis("Vertical"));

                rb.AddForce(vehicle.transform.forward * throttle * boatAcceleration * subFactor, ForceMode.Acceleration);
                rb.AddTorque(vehicle.transform.up * steer * boatTurnTorque * subFactor * 0.001f, ForceMode.Acceleration);

                // Kill engine if fully underwater
                if (subFactor > 0.9f && dt != null)
                    dt.throttleInput *= 0.1f;
            }
        }

        public void SetWaterLevel(float level) => waterLevel = level;
        public void SetCurrent(Vector3 dir, float strength)
        {
            currentDirection = dir.normalized;
            currentStrength = strength;
        }
    }
}
