using UnityEngine;
using CarParking.World;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Full bridge between vehicle physics, terrain deformation, biomes and weather.
    /// Queries biome, wetness, sink depth and water state for every wheel each FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(RealisticVehicleController))]
    [RequireComponent(typeof(DrivetrainSystem))]
    public class VehicleEnvironmentBridge : MonoBehaviour
    {
        private RealisticVehicleController _vehicle;
        private DrivetrainSystem _drivetrain;
        private Rigidbody _rb;

        [Header("Water Sampling")]
        [SerializeField] private Transform[] buoyancySamplePoints;

        private void Awake()
        {
            _vehicle = GetComponent<RealisticVehicleController>();
            _drivetrain = GetComponent<DrivetrainSystem>();
            _rb = GetComponent<Rigidbody>();

            if (buoyancySamplePoints == null || buoyancySamplePoints.Length == 0)
            {
                // Auto-create 4 sample points roughly at wheel positions if none assigned
                buoyancySamplePoints = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    var go = new GameObject($"BuoyancyPoint_{i}");
                    go.transform.SetParent(transform);
                    go.transform.localPosition = new Vector3(
                        (i % 2 == 0 ? -0.8f : 0.8f),
                        0.1f,
                        (i < 2 ? 1.3f : -1.3f)
                    );
                    buoyancySamplePoints[i] = go.transform;
                }
            }
        }

        private void FixedUpdate()
        {
            if (_vehicle == null || _vehicle.wheels == null) return;

            float globalWetness = 0f;
            float mudViscosityMult = 1f;

            if (WaterAndWeatherSystem.Instance != null)
            {
                globalWetness = WaterAndWeatherSystem.Instance.globalWetness;
                mudViscosityMult = 1f + globalWetness * 1.8f;

                // Buoyancy when in water
                Vector3[] points = new Vector3[buoyancySamplePoints.Length];
                for (int i = 0; i < buoyancySamplePoints.Length; i++)
                    points[i] = buoyancySamplePoints[i].position;

                WaterAndWeatherSystem.Instance.ApplyBuoyancy(_rb, points, 0f);
            }

            foreach (var wheel in _vehicle.wheels)
            {
                if (!wheel.isGrounded) continue;

                BiomeType biome = QueryBiomeAtPosition(wheel.hitPoint);
                wheel.surfaceBiome = biome;

                float sink = 0f;
                if (TerrainDeformationManager.Instance != null)
                {
                    sink = TerrainDeformationManager.Instance.GetSinkDepth(wheel.hitPoint, biome);

                    float wheelSpin = Vector3.Dot(_rb.GetPointVelocity(wheel.hitPoint), wheel.wheelTransform.forward);
                    TerrainDeformationManager.Instance.ApplyWheelDeformation(
                        wheel.hitPoint,
                        wheel.radius,
                        _rb.mass,
                        wheelSpin,
                        biome
                    );
                }

                // Feed surface data back into grip (already used inside RealisticVehicleController via surfaceBiome)
                // Additional rolling resistance from mud/sand/wetness
                float resistance = CalculateRollingResistance(biome, sink, globalWetness);
                Vector3 vel = _rb.GetPointVelocity(wheel.hitPoint);
                Vector3 resistanceForce = -vel.normalized * resistance * _rb.mass * 0.15f;
                _rb.AddForceAtPosition(resistanceForce, wheel.hitPoint);
            }
        }

        private BiomeType QueryBiomeAtPosition(Vector3 worldPos)
        {
            // Simple spatial query - in a full implementation this would
            // ask the ProceduralTerrainChunkStreamer for the chunk under the point.
            // For now we use a lightweight noise-based approximation that matches the streamer logic.

            float biomeScale = 0.0008f;
            float nx = worldPos.x * biomeScale;
            float nz = worldPos.z * biomeScale;

            float moisture = Mathf.PerlinNoise(nx * 1.7f, nz * 1.7f);
            float temperature = Mathf.PerlinNoise(nx * 0.9f + 100f, nz * 0.9f + 100f);
            float continental = Mathf.PerlinNoise(nx * 0.4f, nz * 0.4f);

            if (continental < 0.28f) return BiomeType.Ocean;
            if (continental < 0.34f) return BiomeType.Beach;
            if (temperature > 0.65f && moisture < 0.35f) return BiomeType.Desert;
            if (moisture > 0.7f && temperature > 0.45f) return BiomeType.Rainforest;
            if (moisture > 0.55f && temperature < 0.5f) return BiomeType.Mud;
            if (continental > 0.75f && temperature < 0.4f) return BiomeType.Mountain;

            float cityNoise = Mathf.PerlinNoise(nx * 3.1f + 50f, nz * 3.1f + 50f);
            if (cityNoise > 0.82f && continental > 0.4f && continental < 0.7f)
                return BiomeType.City;

            return BiomeType.Grassland;
        }

        private float CalculateRollingResistance(BiomeType biome, float sink, float wetness)
        {
            float baseRes = biome switch
            {
                BiomeType.City => 0.012f,
                BiomeType.Grassland => 0.025f,
                BiomeType.Beach => 0.08f,
                BiomeType.Desert => 0.11f,
                BiomeType.Mud => 0.18f + sink * 0.4f,
                BiomeType.Rainforest => 0.09f,
                BiomeType.Ocean => 0.35f,
                _ => 0.03f
            };

            return baseRes * (1f + wetness * 0.6f);
        }
    }
}
