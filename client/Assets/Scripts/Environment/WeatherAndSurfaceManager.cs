using UnityEngine;
using NextGenDrive.Physics;

namespace NextGenDrive.Environment
{
    /// <summary>
    /// Day/night cycle, rain and road wetness. Wetness is published as the global shader
    /// float "_RoadWetness" (0 dry .. 1 soaked), which road materials read to lower roughness
    /// and add reflections. Cars get a grip multiplier that RealisticCarController applies to
    /// both forward and sideways wheel friction.
    /// </summary>
    public class WeatherAndSurfaceManager : MonoBehaviour
    {
        [Header("Day / Night")]
        public Light sunLight;
        [Tooltip("Real seconds for one full 24-hour cycle")]
        public float dayLengthSeconds = 1200f;
        [Range(0f, 24f)] public float startTimeOfDay = 9f;
        public float maxSunIntensity = 1.2f;
        [Range(0f, 1f)] public float nightAmbient = 0.08f;
        [Range(0f, 1f)] public float dayAmbient = 0.9f;

        [Header("Rain")]
        public ParticleSystem rainParticles;
        [Tooltip("Particles per second at full rain")]
        public float maxRainEmission = 1500f;
        [Tooltip("Rain intensity change per second")]
        public float rainChangeRate = 0.2f;

        [Header("Wet Road")]
        [Tooltip("Wetness lost per second once the rain stops; drying is slower than wetting")]
        public float dryingPerSecond = 0.02f;
        [Range(0.1f, 1f)] public float wetGripMultiplier = 0.55f;

        public static readonly int RoadWetnessID = Shader.PropertyToID("_RoadWetness");

        private readonly System.Collections.Generic.List<RealisticCarController> cars = new System.Collections.Generic.List<RealisticCarController>();
        private float rainTarget;

        public float TimeOfDay { get; private set; }
        public float RainIntensity { get; private set; }
        public float RoadWetness { get; private set; }
        public bool IsNight => TimeOfDay < 6f || TimeOfDay >= 19f;

        private void Awake()
        {
            TimeOfDay = Mathf.Repeat(startTimeOfDay, 24f);
            foreach (var car in FindObjectsByType<RealisticCarController>(FindObjectsSortMode.None))
                RegisterCar(car);
        }

        public void RegisterCar(RealisticCarController car)
        {
            if (car != null && !cars.Contains(car)) cars.Add(car);
        }

        /// <summary>Target rain intensity, 0 = clear, 1 = downpour. Eased in Update.</summary>
        public void SetRainTarget(float intensity)
        {
            rainTarget = Mathf.Clamp01(intensity);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            TimeOfDay = Mathf.Repeat(TimeOfDay + 24f * dt / Mathf.Max(1f, dayLengthSeconds), 24f);
            UpdateSun();

            RainIntensity = Mathf.MoveTowards(RainIntensity, rainTarget, rainChangeRate * dt);

            // Water runs in fast, leaves the asphalt slowly
            float wetTarget = RainIntensity;
            float rate = wetTarget > RoadWetness ? rainChangeRate * 1.5f : dryingPerSecond;
            RoadWetness = Mathf.MoveTowards(RoadWetness, wetTarget, rate * dt);

            Shader.SetGlobalFloat(RoadWetnessID, RoadWetness);
            UpdateRainParticles();
            ApplyGripToCars();
        }

        private void UpdateSun()
        {
            // Elevation: 0 at 6:00 and 18:00, peaks at noon
            float elevation = Mathf.Sin(Mathf.Clamp01((TimeOfDay - 6f) / 12f) * Mathf.PI);
            bool daytime = TimeOfDay >= 6f && TimeOfDay < 18f;
            float height = daytime ? elevation : 0f;

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(TimeOfDay / 24f * 360f - 90f, 170f, 0f);
                sunLight.intensity = Mathf.Max(0f, height) * maxSunIntensity;
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbient, dayAmbient, Mathf.Clamp01(height * 2f));
        }

        private void UpdateRainParticles()
        {
            if (rainParticles == null) return;

            var emission = rainParticles.emission;
            emission.rateOverTime = RainIntensity * maxRainEmission;

            bool raining = RainIntensity > 0.01f;
            if (raining && !rainParticles.isPlaying) rainParticles.Play();
            else if (!raining && rainParticles.isPlaying) rainParticles.Stop();
        }

        private void ApplyGripToCars()
        {
            float multiplier = Mathf.Lerp(1f, wetGripMultiplier, RoadWetness);
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                if (cars[i] == null) { cars.RemoveAt(i); continue; }
                cars[i].surfaceGripMultiplier = multiplier;
            }
        }
    }
}
