using UnityEngine;
using UnityEngine.UI;
using CarParking.Vehicle;
using CarParking.World;
using CarParking.Core;

namespace CarParking.UI
{
    /// <summary>
    /// 8K / HDRP graphics controller + full in-game HUD.
    /// </summary>
    public class Graphics8KAndHUDController : MonoBehaviour
    {
        public static Graphics8KAndHUDController Instance { get; private set; }

        [Header("Graphics")]
        public bool enableRayTracing = false;
        public bool enableDLSS = true;
        public int targetFPS = 60;
        public int resolutionScale = 100; // percent

        [Header("HUD References")]
        public Text speedText;
        public Text gearText;
        public Text rpmText;
        public Text fuelText;
        public Text cashText;
        public Text dirtText;
        public Text weatherText;
        public Text fpsText;
        public Image minimapImage;
        public RectTransform playerArrow;
        public GameObject parkingSensorOverlay;
        public GameObject winchPanel;
        public GameObject tuningPanel;

        private float _fpsTimer;
        private int _fpsFrames;
        private float _currentFps;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Application.targetFrameRate = targetFPS;
            QualitySettings.vSyncCount = 0;
        }

        private void Update()
        {
            UpdateFPS();
            UpdateHUD();
            HandleHotkeys();
        }

        private void UpdateFPS()
        {
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _currentFps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0f;

                if (fpsText != null)
                    fpsText.text = $"{_currentFps:F0} FPS";
            }
        }

        private void UpdateHUD()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            var drivetrain = vehicle != null ? vehicle.GetComponent<DrivetrainSystem>() : null;
            var rb = vehicle != null ? vehicle.GetComponent<Rigidbody>() : null;

            if (rb != null && speedText != null)
                speedText.text = $"{rb.linearVelocity.magnitude * 3.6f:F0} km/h";

            if (drivetrain != null)
            {
                if (gearText != null)
                    gearText.text = drivetrain.currentGear == -1 ? "R" : drivetrain.currentGear == 0 ? "N" : drivetrain.currentGear.ToString();

                if (rpmText != null)
                    rpmText.text = $"{drivetrain.currentRpm:F0} RPM";
            }

            if (WorldServicesAndEconomySystem.Instance != null)
            {
                if (fuelText != null)
                    fuelText.text = $"{WorldServicesAndEconomySystem.Instance.currentFuel * 100f:F0}%";

                if (cashText != null)
                    cashText.text = $"${WorldServicesAndEconomySystem.Instance.playerCash:N0}";

                if (dirtText != null)
                    dirtText.text = $"Dirt: {WorldServicesAndEconomySystem.Instance.DirtLevel * 100f:F0}%";
            }

            if (WaterAndWeatherSystem.Instance != null && weatherText != null)
                weatherText.text = WaterAndWeatherSystem.Instance.currentWeather.ToString();

            // Minimap player arrow
            if (playerArrow != null && vehicle != null)
            {
                playerArrow.localRotation = Quaternion.Euler(0f, 0f, -vehicle.transform.eulerAngles.y);
            }
        }

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.F1) && tuningPanel != null)
                tuningPanel.SetActive(!tuningPanel.activeSelf);

            if (Input.GetKeyDown(KeyCode.F2) && winchPanel != null)
                winchPanel.SetActive(!winchPanel.activeSelf);

            if (Input.GetKeyDown(KeyCode.F3))
                ToggleRayTracing();

            if (Input.GetKeyDown(KeyCode.F4))
                CycleResolutionScale();
        }

        public void ToggleRayTracing()
        {
            enableRayTracing = !enableRayTracing;
            // In a full HDRP project this would set HDRenderPipeline volume overrides
            Debug.Log($"[Graphics] Ray Tracing: {enableRayTracing}");
        }

        public void CycleResolutionScale()
        {
            resolutionScale = resolutionScale switch
            {
                50 => 75,
                75 => 100,
                100 => 150,
                150 => 200,
                _ => 100
            };

            float scale = resolutionScale / 100f;
            // Screen.SetResolution would be used in builds; here we log
            Debug.Log($"[Graphics] Resolution scale: {resolutionScale}%");
        }

        public void SetTargetFPS(int fps)
        {
            targetFPS = fps;
            Application.targetFrameRate = fps;
        }
    }
}
