using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using CarParking.World;

namespace CarParking.World
{
    /// <summary>
    /// Bridges live weather API data into WaterAndWeatherSystem / HDRP climate.
    /// Uses Open-Meteo (no API key) as default free endpoint.
    /// </summary>
    public class RealWorldWeatherAndSatelliteSync : MonoBehaviour
    {
        public static RealWorldWeatherAndSatelliteSync Instance { get; private set; }

        [Header("Location")]
        public float latitude = 41.3f;  // Tashkent default
        public float longitude = 69.2f;
        public float refreshMinutes = 15f;

        [Header("Mapped State")]
        public float liveTemperature = 20f;
        public float liveWindSpeed;
        public float livePrecipitation;
        public float liveCloudCover;
        public string liveCondition = "Clear";

        private float _timer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(FetchWeather());
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= refreshMinutes * 60f)
            {
                _timer = 0f;
                StartCoroutine(FetchWeather());
            }
        }

        private IEnumerator FetchWeather()
        {
            // Open-Meteo free API
            string url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m,precipitation,cloud_cover,wind_speed_10m,weather_code";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[WeatherSync] Fetch failed: " + req.error);
                    yield break;
                }

                ParseAndApply(req.downloadHandler.text);
            }
        }

        private void ParseAndApply(string json)
        {
            // Lightweight parse without full JSON lib
            liveTemperature = ExtractFloat(json, "temperature_2m");
            livePrecipitation = ExtractFloat(json, "precipitation");
            liveCloudCover = ExtractFloat(json, "cloud_cover");
            liveWindSpeed = ExtractFloat(json, "wind_speed_10m");
            int code = Mathf.RoundToInt(ExtractFloat(json, "weather_code"));

            liveCondition = code switch
            {
                0 => "Clear",
                1 or 2 or 3 => "Cloudy",
                45 or 48 => "Foggy",
                51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => "Rain",
                71 or 73 or 75 or 85 or 86 => "Snow",
                95 or 96 or 99 => "Storm",
                _ => "Cloudy"
            };

            ApplyToGameSystems();
            Debug.Log($"[WeatherSync] {liveCondition} | {liveTemperature:F1}C | wind {liveWindSpeed:F1} | rain {livePrecipitation:F1}");
        }

        private void ApplyToGameSystems()
        {
            if (WaterAndWeatherSystem.Instance == null) return;

            WeatherType wt = liveCondition switch
            {
                "Clear" => WeatherType.Clear,
                "Cloudy" => WeatherType.Cloudy,
                "Foggy" => WeatherType.Foggy,
                "Rain" or "Storm" => WeatherType.AmazonHeavyRain,
                "Snow" => WeatherType.Cloudy,
                _ => WeatherType.Clear
            };

            // Desert sandstorm if high wind + dry
            if (liveWindSpeed > 40f && livePrecipitation < 0.1f && liveTemperature > 30f)
                wt = WeatherType.ArabianSandstorm;

            WaterAndWeatherSystem.Instance.SetWeather(wt);

            // Wind into hydrodynamics if present
            if (AdvancedHydrodynamicsSystem.Instance != null && liveWindSpeed > 5f)
            {
                Vector3 windDir = new Vector3(1f, 0f, 0.2f).normalized;
                AdvancedHydrodynamicsSystem.Instance.SetCurrent(windDir, liveWindSpeed * 0.05f);
            }
        }

        private float ExtractFloat(string json, string key)
        {
            string pattern = "\"" + key + "\":";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return 0f;
            idx += pattern.Length;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-' || json[end] == 'e' || json[end] == 'E' || json[end] == '+'))
                end++;
            string num = json.Substring(idx, end - idx).Trim();
            if (float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                return v;
            return 0f;
        }

        public void SetLocation(float lat, float lon)
        {
            latitude = lat;
            longitude = lon;
            StartCoroutine(FetchWeather());
        }
    }
}
