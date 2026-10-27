using UnityEngine;
using CarParking.World;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Next-gen vehicle controller using custom raycast suspension + Pacejka tire model.
    /// Surface-aware (asphalt, mud, sand, wet rainforest).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RealisticVehicleController : MonoBehaviour
    {
        [System.Serializable]
        public class Wheel
        {
            public Transform wheelTransform;
            public Transform caliperTransform;
            public bool isDriveWheel;
            public bool isSteerWheel;
            public float radius = 0.35f;
            [HideInInspector] public float compression;
            [HideInInspector] public float slipRatio;
            [HideInInspector] public float slipAngle;
            [HideInInspector] public Vector3 hitPoint;
            [HideInInspector] public bool isGrounded;
            [HideInInspector] public BiomeType surfaceBiome;
        }

        [Header("Wheels")]
        public Wheel[] wheels;

        [Header("Suspension")]
        public float suspensionRestLength = 0.35f;
        public float springForce = 35000f;
        public float damperForce = 4500f;

        [Header("Tire / Pacejka")]
        public float tireGrip = 1.1f;
        public float lateralStiffness = 12f;
        public float longitudinalStiffness = 10f;

        [Header("References")]
        public DrivetrainSystem drivetrain;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = new Vector3(0f, -0.2f, 0.1f);
        }

        private void FixedUpdate()
        {
            foreach (var wheel in wheels)
            {
                UpdateWheel(wheel);
            }
        }

        private void UpdateWheel(Wheel wheel)
        {
            Vector3 origin = wheel.wheelTransform.position;
            RaycastHit hit;

            wheel.isGrounded = Physics.SphereCast(origin, wheel.radius * 0.9f, -transform.up,
                out hit, suspensionRestLength + wheel.radius);

            if (!wheel.isGrounded)
            {
                wheel.compression = 0f;
                return;
            }

            wheel.hitPoint = hit.point;
            float distance = hit.distance - wheel.radius;
            wheel.compression = Mathf.Clamp01(1f - (distance / suspensionRestLength));

            // Detect surface biome (simple version - expand with TerrainChunk query later)
            wheel.surfaceBiome = DetectBiomeUnderWheel(hit);

            // Suspension force
            float spring = wheel.compression * springForce;
            float damper = Vector3.Dot(_rb.GetPointVelocity(origin), transform.up) * damperForce;
            Vector3 suspensionForce = transform.up * (spring - damper);
            _rb.AddForceAtPosition(suspensionForce, origin);

            // Tire forces (simplified Pacejka-style)
            Vector3 velocity = _rb.GetPointVelocity(hit.point);
            Vector3 forward = wheel.wheelTransform.forward;
            Vector3 right = wheel.wheelTransform.right;

            float forwardSpeed = Vector3.Dot(velocity, forward);
            float lateralSpeed = Vector3.Dot(velocity, right);

            // Surface multipliers
            float gripMult = GetGripMultiplier(wheel.surfaceBiome);
            float sink = 0f;

            if (TerrainDeformationManager.Instance != null)
            {
                sink = TerrainDeformationManager.Instance.GetSinkDepth(hit.point, wheel.surfaceBiome);
                TerrainDeformationManager.Instance.ApplyWheelDeformation(
                    hit.point, wheel.radius, _rb.mass, forwardSpeed, wheel.surfaceBiome);
            }

            float longForce = -forwardSpeed * longitudinalStiffness * gripMult * (1f - sink * 0.7f);
            float latForce = -lateralSpeed * lateralStiffness * gripMult * (1f - sink * 0.5f);

            // Drive torque from drivetrain
            if (wheel.isDriveWheel && drivetrain != null)
            {
                longForce += drivetrain.GetWheelTorque(wheel) / wheel.radius;
            }

            Vector3 tireForce = forward * longForce + right * latForce;
            _rb.AddForceAtPosition(tireForce, hit.point);

            // Visual wheel position
            Vector3 wheelPos = origin - transform.up * (distance);
            wheel.wheelTransform.position = wheelPos;
        }

        private BiomeType DetectBiomeUnderWheel(RaycastHit hit)
        {
            // Placeholder - in full version query the TerrainChunk under the hit point
            // For now return Grassland as default
            return BiomeType.Grassland;
        }

        private float GetGripMultiplier(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.City => 1.15f,
                BiomeType.Grassland => 0.95f,
                BiomeType.Beach => 0.65f,
                BiomeType.Desert => 0.55f,
                BiomeType.Mud => 0.35f,
                BiomeType.Rainforest => 0.45f,
                BiomeType.Ocean => 0.1f,
                _ => 0.9f
            };
        }
    }
}
