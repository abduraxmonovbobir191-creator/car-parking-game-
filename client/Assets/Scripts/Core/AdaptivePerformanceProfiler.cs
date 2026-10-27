using UnityEngine;
using UnityEngine.Rendering;

namespace CarParking.Core
{
    /// <summary>
    /// Adaptive performance: monitors FPS and scales shadows, SSR, resolution
    /// to target 120 FPS on flagship mobile and high-end PC.
    /// </summary>
    public class AdaptivePerformanceProfiler : MonoBehaviour
    {
        public static AdaptivePerformanceProfiler Instance { get; private set; }

        [Header("Target")]
        public float targetFPS = 120f;
        public float minFPS = 50f;
        public float adjustInterval = 0.75f;

        [Header("Dynamic Resolution")]
        public float minScale = 0.6f;
        public float maxScale = 1f;
        public float currentScale = 1f;

        [Header("Quality Tiers")]
        public int shadowCascades = 4;
        public bool ssrEnabled = true;
        public bool softParticles = true;

        private float _fpsTimer;
        private int _fpsFrames;
        private float _currentFps = 60f;
        private float _adjustTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Application.targetFrameRate = Mathf.RoundToInt(targetFPS);
            QualitySettings.vSyncCount = 0;

            // Mobile detection
            if (Application.isMobilePlatform)
            {
                targetFPS = 120f;
                minScale = 0.55f;
            }
        }

        private void Update()
        {
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.4f)
            {
                _currentFps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0f;
            }

            _adjustTimer += Time.unscaledDeltaTime;
            if (_adjustTimer >= adjustInterval)
            {
                _adjustTimer = 0f;
                AdaptQuality();
            }
        }

        private void AdaptQuality()
        {
            float ratio = _currentFps / targetFPS;

            if (ratio < 0.75f)
            {
                // Scale down
                currentScale = Mathf.Max(minScale, currentScale - 0.08f);
                shadowCascades = Mathf.Max(1, shadowCascades - 1);
                if (ratio < 0.55f) ssrEnabled = false;
                softParticles = false;
            }
            else if (ratio > 1.05f)
            {
                // Scale up carefully
                currentScale = Mathf.Min(maxScale, currentScale + 0.04f);
                if (currentScale > 0.9f)
                {
                    shadowCascades = Mathf.Min(4, shadowCascades + 1);
                    ssrEnabled = true;
                    softParticles = true;
                }
            }

            ApplySettings();
        }

        private void ApplySettings()
        {
            // Dynamic resolution via ScalableBufferManager (supported platforms)
            try
            {
                ScalableBufferManager.ResizeBuffers(currentScale, currentScale);
            }
            catch
            {
                // Not supported on all backends — ignore
            }

            QualitySettings.shadowCascades = shadowCascades;
            QualitySettings.softParticles = softParticles;

            // SSR toggle would be HDRP volume override in full project
            Shader.SetGlobalFloat("_SSREnabled", ssrEnabled ? 1f : 0f);
        }

        public float CurrentFPS => _currentFps;
        public float ResolutionScale => currentScale;

        public void ForceTargetFPS(int fps)
        {
            targetFPS = fps;
            Application.targetFrameRate = fps;
        }
    }
}
