using UnityEngine;

namespace CarParking.World
{
    public enum WeatherType
    {
        Clear,
        Cloudy,
        AmazonHeavyRain,
        ArabianSandstorm,
        Foggy
    }

    /// <summary>
    /// Gerstner-style ocean waves, buoyancy, dynamic weather that affects
    /// road wetness and mud viscosity.
    /// </summary>
    public class WaterAndWeatherSystem : MonoBehaviour
    {
        public static WaterAndWeatherSystem Instance { get; private set; }

        [Header("Ocean")]
        public Material oceanMaterial;
        public float waveAmplitude = 1.8f;
        public float waveLength = 45f;
        public float waveSpeed = 1.2f;
        public float steepness = 0.4f;

        [Header("Weather")]
        public WeatherType currentWeather = WeatherType.Clear;
        public float rainIntensity = 0f;
        public float sandstormIntensity = 0f;
        public float globalWetness = 0f;

        [Header("References")]
        public Light sunLight;
        public ParticleSystem rainParticles;
        public ParticleSystem sandstormParticles;

        private static readonly int WaveParamsId = Shader.PropertyToID("_WaveParams");
        private static readonly int WetnessId = Shader.PropertyToID("_GlobalWetness");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            UpdateWeatherTransition();
            UpdateOceanMaterial();
            ApplyWeatherEffects();
        }

        private void UpdateWeatherTransition()
        {
            // Simple target-based blending
            float targetRain = currentWeather == WeatherType.AmazonHeavyRain ? 1f : 0f;
            float targetSand = currentWeather == WeatherType.ArabianSandstorm ? 1f : 0f;
            float targetWet = (currentWeather == WeatherType.AmazonHeavyRain) ? 0.85f :
                             (currentWeather == WeatherType.Cloudy ? 0.3f : 0.05f);

            rainIntensity = Mathf.MoveTowards(rainIntensity, targetRain, Time.deltaTime * 0.25f);
            sandstormIntensity = Mathf.MoveTowards(sandstormIntensity, targetSand, Time.deltaTime * 0.3f);
            globalWetness = Mathf.MoveTowards(globalWetness, targetWet, Time.deltaTime * 0.15f);
        }

        private void UpdateOceanMaterial()
        {
            if (oceanMaterial == null) return;

            // Gerstner parameters: amplitude, wavelength, speed, steepness
            oceanMaterial.SetVector(WaveParamsId, new Vector4(waveAmplitude, waveLength, waveSpeed, steepness));
            oceanMaterial.SetFloat(WetnessId, globalWetness);
        }

        private void ApplyWeatherEffects()
        {
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = rainIntensity * 2500f;
            }

            if (sandstormParticles != null)
            {
                var emission = sandstormParticles.emission;
                emission.rateOverTime = sandstormIntensity * 1800f;
            }

            if (sunLight != null)
            {
                float intensity = currentWeather switch
                {
                    WeatherType.Clear => 1.2f,
                    WeatherType.Cloudy => 0.7f,
                    WeatherType.AmazonHeavyRain => 0.35f,
                    WeatherType.ArabianSandstorm => 0.5f,
                    WeatherType.Foggy => 0.4f,
                    _ => 1f
                };
                sunLight.intensity = Mathf.Lerp(sunLight.intensity, intensity, Time.deltaTime * 1.5f);
            }

            // Push wetness to all materials that support it (global shader property)
            Shader.SetGlobalFloat("_GlobalWetness", globalWetness);
            Shader.SetGlobalFloat("_MudViscosityMultiplier", 1f + globalWetness * 1.8f);
        }

        /// <summary>
        /// Simple buoyancy force for vehicles in water.
        /// Call from vehicle FixedUpdate when in Ocean biome.
        /// </summary>
        public void ApplyBuoyancy(Rigidbody rb, Vector3[] samplePoints, float waterLevel = 0f)
        {
            foreach (var point in samplePoints)
            {
                float depth = waterLevel - point.y;
                if (depth > 0f)
                {
                    Vector3 force = Vector3.up * depth * 12f * rb.mass / samplePoints.Length;
                    // Add simple drag
                    force += -rb.GetPointVelocity(point) * 0.8f;
                    rb.AddForceAtPosition(force, point);
                }
            }
        }

        public void SetWeather(WeatherType weather)
        {
            currentWeather = weather;
        }
    }
}
