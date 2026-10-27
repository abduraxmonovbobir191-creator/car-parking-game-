using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.World
{
    public enum DisasterType { None, Tornado, FlashFlood, Earthquake }

    /// <summary>
    /// Extreme weather disasters: Tornado vortex, Flash Flood, Earthquake.
    /// Integrates with WaterAndWeatherSystem and vehicle physics.
    /// </summary>
    public class WeatherDisasterSystem : MonoBehaviour
    {
        public static WeatherDisasterSystem Instance { get; private set; }

        [Header("General")]
        public DisasterType currentDisaster = DisasterType.None;
        public float disasterDuration = 45f;
        public float randomChancePerMinute = 0.08f;

        [Header("Tornado")]
        public float tornadoRadius = 55f;
        public float tornadoPullForce = 35000f;
        public float tornadoLiftForce = 22000f;
        public float tornadoMoveSpeed = 12f;
        public GameObject tornadoVisualPrefab;

        [Header("Flash Flood")]
        public float floodRiseSpeed = 0.4f;
        public float maxFloodHeight = 3.5f;
        public float floodCurrentForce = 8000f;

        [Header("Earthquake")]
        public float quakeIntensity = 0.35f;
        public float quakeDuration = 12f;

        private float _disasterTimer;
        private Vector3 _tornadoPos;
        private GameObject _tornadoVisual;
        private float _floodHeight;
        private float _quakeTimer;
        private Transform _player;
        private Camera _mainCam;
        private Vector3 _camOriginalLocalPos;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _mainCam = Camera.main;
            if (_mainCam != null)
                _camOriginalLocalPos = _mainCam.transform.localPosition;

            var v = FindFirstObjectByType<RealisticVehicleController>();
            if (v != null) _player = v.transform;
        }

        private void Update()
        {
            if (_player == null)
            {
                var v = FindFirstObjectByType<RealisticVehicleController>();
                if (v != null) _player = v.transform;
            }

            if (currentDisaster == DisasterType.None)
            {
                // Random trigger
                if (Random.value < (randomChancePerMinute / 60f) * Time.deltaTime)
                {
                    DisasterType[] types = { DisasterType.Tornado, DisasterType.FlashFlood, DisasterType.Earthquake };
                    StartDisaster(types[Random.Range(0, types.Length)]);
                }
                return;
            }

            _disasterTimer -= Time.deltaTime;
            if (_disasterTimer <= 0f)
            {
                EndDisaster();
                return;
            }

            switch (currentDisaster)
            {
                case DisasterType.Tornado: UpdateTornado(); break;
                case DisasterType.FlashFlood: UpdateFlood(); break;
                case DisasterType.Earthquake: UpdateEarthquake(); break;
            }
        }

        public void StartDisaster(DisasterType type)
        {
            currentDisaster = type;
            _disasterTimer = disasterDuration;

            if (type == DisasterType.Tornado && _player != null)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * 80f;
                _tornadoPos = _player.position + new Vector3(offset.x, 0f, offset.y);

                if (tornadoVisualPrefab != null)
                    _tornadoVisual = Instantiate(tornadoVisualPrefab, _tornadoPos, Quaternion.identity);
            }
            else if (type == DisasterType.FlashFlood)
            {
                _floodHeight = 0f;
                if (WaterAndWeatherSystem.Instance != null)
                    WaterAndWeatherSystem.Instance.SetWeather(WeatherType.AmazonHeavyRain);
            }
            else if (type == DisasterType.Earthquake)
            {
                _quakeTimer = quakeDuration;
            }

            Debug.Log("[Disaster] Started: " + type);
        }

        private void EndDisaster()
        {
            currentDisaster = DisasterType.None;
            _floodHeight = 0f;

            if (_tornadoVisual != null)
            {
                Destroy(_tornadoVisual);
                _tornadoVisual = null;
            }

            if (_mainCam != null)
                _mainCam.transform.localPosition = _camOriginalLocalPos;

            Debug.Log("[Disaster] Ended");
        }

        private void UpdateTornado()
        {
            if (_player == null) return;

            // Move tornado slowly toward / around player
            Vector3 toPlayer = (_player.position - _tornadoPos);
            toPlayer.y = 0f;
            _tornadoPos += toPlayer.normalized * tornadoMoveSpeed * Time.deltaTime * 0.3f;
            _tornadoPos += new Vector3(Mathf.Sin(Time.time * 0.7f), 0f, Mathf.Cos(Time.time * 0.5f)) * 4f * Time.deltaTime;

            if (_tornadoVisual != null)
            {
                _tornadoVisual.transform.position = _tornadoPos;
                _tornadoVisual.transform.Rotate(0f, 180f * Time.deltaTime, 0f);
            }

            // Affect nearby rigidbodies
            Collider[] cols = Physics.OverlapSphere(_tornadoPos, tornadoRadius);
            foreach (var col in cols)
            {
                var rb = col.attachedRigidbody;
                if (rb == null) continue;

                Vector3 toCenter = _tornadoPos - rb.position;
                float dist = toCenter.magnitude;
                if (dist < 0.1f) continue;

                float falloff = 1f - Mathf.Clamp01(dist / tornadoRadius);
                Vector3 pull = toCenter.normalized * tornadoPullForce * falloff;
                Vector3 lift = Vector3.up * tornadoLiftForce * falloff * falloff;
                Vector3 tangent = Vector3.Cross(Vector3.up, toCenter.normalized) * tornadoPullForce * 0.4f * falloff;

                rb.AddForce(pull + lift + tangent, ForceMode.Force);
            }
        }

        private void UpdateFlood()
        {
            _floodHeight = Mathf.Min(maxFloodHeight, _floodHeight + floodRiseSpeed * Time.deltaTime);

            if (_player == null) return;

            var rb = _player.GetComponent<Rigidbody>();
            if (rb == null) return;

            // If vehicle is in low area (approx) apply buoyancy + current
            float groundY = _player.position.y;
            if (groundY < _floodHeight + 1.5f)
            {
                float submersion = Mathf.Clamp01((_floodHeight + 1.5f - groundY) / 2f);
                rb.AddForce(Vector3.up * submersion * 15000f, ForceMode.Force);

                // Current drag along X
                rb.AddForce(Vector3.right * floodCurrentForce * submersion * 0.3f, ForceMode.Force);

                // Engine drowning
                if (submersion > 0.75f)
                {
                    var drivetrain = _player.GetComponent<DrivetrainSystem>();
                    if (drivetrain != null)
                        drivetrain.throttleInput *= 0.2f;
                }
            }
        }

        private void UpdateEarthquake()
        {
            _quakeTimer -= Time.deltaTime;

            if (_mainCam != null)
            {
                float shake = quakeIntensity * (_quakeTimer / quakeDuration);
                Vector3 offset = new Vector3(
                    Mathf.Sin(Time.time * 28f) * shake,
                    Mathf.Cos(Time.time * 33f) * shake * 0.6f,
                    0f
                );
                _mainCam.transform.localPosition = _camOriginalLocalPos + offset;
            }

            // Light force on player vehicle
            if (_player != null)
            {
                var rb = _player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 jolt = new Vector3(
                        Mathf.Sin(Time.time * 15f),
                        0f,
                        Mathf.Cos(Time.time * 12f)
                    ) * 4000f * (_quakeTimer / quakeDuration);
                    rb.AddForce(jolt, ForceMode.Force);
                }
            }

            if (_quakeTimer <= 0f)
                EndDisaster();
        }

        public float CurrentFloodHeight => _floodHeight;
    }
}
