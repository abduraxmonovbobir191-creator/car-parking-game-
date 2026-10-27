using UnityEngine;
using CarParking.World;
using CarParking.Core;
using CarParking.Vehicle;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Progressive dirt/mud/dust mask on vehicle body by biome.
    /// Washes off in water or car wash.
    /// </summary>
    public class AdvancedDirtMaskController : MonoBehaviour
    {
        [Header("Dirt")]
        public float dirtAmount;
        public float mudAmount;
        public float dustAmount;
        public float maxLayer = 1f;
        public float accumulateRate = 0.08f;
        public float washRate = 0.5f;

        [Header("Materials")]
        public Renderer[] bodyRenderers;
        public string dirtProperty = "_DirtAmount";
        public string mudProperty = "_MudAmount";
        public string dustProperty = "_DustAmount";

        private RealisticVehicleController _vehicle;
        private float _distAcc;

        private void Awake()
        {
            _vehicle = GetComponent<RealisticVehicleController>();
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                bodyRenderers = GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            if (_vehicle == null || _vehicle.wheels == null) return;

            BiomeType biome = BiomeType.Grassland;
            bool grounded = false;
            foreach (var w in _vehicle.wheels)
            {
                if (w.isGrounded)
                {
                    grounded = true;
                    biome = w.surfaceBiome;
                    break;
                }
            }

            var rb = GetComponent<Rigidbody>();
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;

            if (grounded && speed > 1f)
            {
                float rate = accumulateRate * speed * 0.05f * Time.deltaTime;

                switch (biome)
                {
                    case BiomeType.Mud:
                        mudAmount = Mathf.Min(maxLayer, mudAmount + rate * 2.5f);
                        dirtAmount = Mathf.Min(maxLayer, dirtAmount + rate);
                        break;
                    case BiomeType.Desert:
                    case BiomeType.Beach:
                        dustAmount = Mathf.Min(maxLayer, dustAmount + rate * 2f);
                        dirtAmount = Mathf.Min(maxLayer, dirtAmount + rate * 0.8f);
                        break;
                    case BiomeType.Ocean:
                        Wash(washRate * 2f * Time.deltaTime);
                        break;
                    default:
                        dirtAmount = Mathf.Min(maxLayer, dirtAmount + rate * 0.15f);
                        break;
                }
            }

            // Rain wash
            if (WaterAndWeatherSystem.Instance != null && WaterAndWeatherSystem.Instance.rainIntensity > 0.4f)
                Wash(washRate * 0.3f * WaterAndWeatherSystem.Instance.rainIntensity * Time.deltaTime);

            ApplyToMaterials();
        }

        public void Wash(float amount)
        {
            dirtAmount = Mathf.Max(0f, dirtAmount - amount);
            mudAmount = Mathf.Max(0f, mudAmount - amount * 1.2f);
            dustAmount = Mathf.Max(0f, dustAmount - amount * 1.5f);
        }

        public void FullWash()
        {
            dirtAmount = mudAmount = dustAmount = 0f;
            ApplyToMaterials();
            if (WorldServicesAndEconomySystem.Instance != null)
                WorldServicesAndEconomySystem.Instance.WashVehicle();
            Debug.Log("[Dirt] Vehicle fully washed");
        }

        private void ApplyToMaterials()
        {
            if (bodyRenderers == null) return;
            foreach (var r in bodyRenderers)
            {
                if (r == null || r.material == null) continue;
                if (r.material.HasProperty(dirtProperty)) r.material.SetFloat(dirtProperty, dirtAmount);
                if (r.material.HasProperty(mudProperty)) r.material.SetFloat(mudProperty, mudAmount);
                if (r.material.HasProperty(dustProperty)) r.material.SetFloat(dustProperty, dustAmount);
                // Fallback single property
                if (r.material.HasProperty("_DirtAmount"))
                    r.material.SetFloat("_DirtAmount", Mathf.Max(dirtAmount, Mathf.Max(mudAmount, dustAmount)));
            }
        }

        public float TotalDirt => Mathf.Max(dirtAmount, Mathf.Max(mudAmount, dustAmount));
    }
}
