using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.World
{
    /// <summary>
    /// Volumetric headlights, wet asphalt specular, golden hour / midnight grading.
    /// Designed for HDRP (lights work in Built-in too as standard spotlights).
    /// </summary>
    public class VolumetricLightingAndNightAtmosphere : MonoBehaviour
    {
        public static VolumetricLightingAndNightAtmosphere Instance { get; private set; }

        [Header("Headlights")]
        public Light[] headlightSpots;
        public float headlightRange = 60f;
        public float headlightAngle = 45f;
        public float headlightIntensityNight = 8f;
        public float headlightIntensityDay = 1.5f;

        [Header("Atmosphere")]
        public Light sunLight;
        public Gradient skyColorByTime;
        public AnimationCurve sunIntensityByTime;
        public float timeOfDay = 14f; // 0-24
        public float timeSpeed = 0.5f;
        public bool autoCycleTime = false;

        [Header("Wet Surface")]
        public float wetSpecularBoost = 2.5f;

        private float _baseSunIntensity = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (sunLight != null)
                _baseSunIntensity = sunLight.intensity;

            if (skyColorByTime == null || skyColorByTime.colorKeys.Length == 0)
            {
                skyColorByTime = new Gradient();
                skyColorByTime.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 0f),
                        new GradientColorKey(new Color(1f, 0.6f, 0.3f), 0.25f),
                        new GradientColorKey(new Color(0.7f, 0.85f, 1f), 0.5f),
                        new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.75f),
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 1f)
                    },
                    new[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });
            }
        }

        private void Start()
        {
            // Auto-find headlights on player vehicle
            if (headlightSpots == null || headlightSpots.Length == 0)
            {
                var v = FindFirstObjectByType<RealisticVehicleController>();
                if (v != null)
                {
                    var lights = v.GetComponentsInChildren<Light>();
                    var list = new System.Collections.Generic.List<Light>();
                    foreach (var l in lights)
                        if (l.type == LightType.Spot) list.Add(l);
                    headlightSpots = list.ToArray();
                }
            }

            ConfigureHeadlights();
        }

        private void Update()
        {
            if (autoCycleTime)
            {
                timeOfDay += Time.deltaTime * timeSpeed / 60f;
                if (timeOfDay >= 24f) timeOfDay -= 24f;
            }

            UpdateSunAndSky();
            UpdateHeadlightsByTime();
            UpdateWetness();
        }

        private void ConfigureHeadlights()
        {
            if (headlightSpots == null) return;
            foreach (var l in headlightSpots)
            {
                if (l == null) continue;
                l.type = LightType.Spot;
                l.range = headlightRange;
                l.spotAngle = headlightAngle;
                l.shadows = LightShadows.Soft;
            }
        }

        private void UpdateSunAndSky()
        {
            float t = timeOfDay / 24f;

            if (sunLight != null)
            {
                // Sun orbit
                float sunAngle = (timeOfDay - 6f) / 24f * 360f;
                sunLight.transform.rotation = Quaternion.Euler(sunAngle, 30f, 0f);

                float intensity = sunIntensityByTime != null && sunIntensityByTime.keys.Length > 0
                    ? sunIntensityByTime.Evaluate(t)
                    : Mathf.Clamp01(Mathf.Sin((timeOfDay - 6f) / 12f * Mathf.PI));

                sunLight.intensity = _baseSunIntensity * Mathf.Max(0.02f, intensity);
                sunLight.color = skyColorByTime.Evaluate(t);
            }

            RenderSettings.ambientLight = skyColorByTime.Evaluate(t) * 0.4f;
        }

        private void UpdateHeadlightsByTime()
        {
            bool night = timeOfDay < 6f || timeOfDay > 19f;
            float intensity = night ? headlightIntensityNight : headlightIntensityDay;

            if (headlightSpots == null) return;
            foreach (var l in headlightSpots)
            {
                if (l == null) continue;
                l.intensity = intensity;
                l.enabled = night || intensity > 2f;
            }
        }

        private void UpdateWetness()
        {
            float wet = 0f;
            if (WaterAndWeatherSystem.Instance != null)
                wet = WaterAndWeatherSystem.Instance.globalWetness;

            // Global shader property for wet specular on roads
            Shader.SetGlobalFloat("_WetSpecularBoost", 1f + wet * wetSpecularBoost);
            Shader.SetGlobalFloat("_GlobalWetness", wet);
        }

        public void SetTimeOfDay(float hour)
        {
            timeOfDay = Mathf.Repeat(hour, 24f);
        }

        public bool IsNight => timeOfDay < 6f || timeOfDay > 19f;
    }
}
