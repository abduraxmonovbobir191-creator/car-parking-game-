using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.Audio
{
    /// <summary>
    /// Procedural multi-layer engine, turbo, exhaust ALS and surface audio.
    /// Adapts to Engine Swap and real-time drivetrain state.
    /// </summary>
    public class VehicleAudioSynthesizer : MonoBehaviour
    {
        [Header("Engine Layers")]
        public AudioSource idleSource;
        public AudioSource lowSource;
        public AudioSource midSource;
        public AudioSource highSource;
        public AudioSource redlineSource;

        [Header("Turbo & Exhaust")]
        public AudioSource turboSource;
        public AudioSource blowOffSource;
        public AudioSource exhaustPopSource;

        [Header("Surface")]
        public AudioSource tireSource;
        public AudioSource mudSource;
        public AudioSource sandSource;
        public AudioSource waterSource;

        [Header("Clips")]
        public AudioClip blowOffClip;
        public AudioClip[] popClips;

        private DrivetrainSystem _drivetrain;
        private RealisticVehicleController _vehicle;
        private VehicleTuningSystem _tuning;
        private Rigidbody _rb;

        private float _prevThrottle;
        private float _turboWhine;

        private void Awake()
        {
            _drivetrain = GetComponent<DrivetrainSystem>();
            _vehicle = GetComponent<RealisticVehicleController>();
            _tuning = GetComponent<VehicleTuningSystem>();
            _rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_drivetrain == null) return;

            float rpmNorm = Mathf.Clamp01(_drivetrain.currentRpm / _drivetrain.maxRpm);
            float throttle = _drivetrain.throttleInput;
            float load = throttle;

            // Engine layer crossfade
            SetLayer(idleSource, rpmNorm, 0f, 0.15f, 0.6f);
            SetLayer(lowSource, rpmNorm, 0.1f, 0.4f, 0.8f);
            SetLayer(midSource, rpmNorm, 0.35f, 0.65f, 1f);
            SetLayer(highSource, rpmNorm, 0.55f, 0.85f, 1f);
            SetLayer(redlineSource, rpmNorm, 0.85f, 1f, 1f);

            // Pitch adapts slightly to engine type
            float pitchBase = 0.85f + rpmNorm * 0.5f;
            if (_tuning != null)
            {
                pitchBase *= _tuning.currentEngine switch
                {
                    EngineType.I4 => 1.1f,
                    EngineType.I6_2JZ => 1.0f,
                    EngineType.V8_Biturbo => 0.92f,
                    EngineType.V12 => 0.88f,
                    _ => 1f
                };
            }

            ApplyPitch(idleSource, pitchBase * 0.9f);
            ApplyPitch(lowSource, pitchBase);
            ApplyPitch(midSource, pitchBase * 1.05f);
            ApplyPitch(highSource, pitchBase * 1.1f);
            ApplyPitch(redlineSource, pitchBase * 1.15f);

            // Turbo
            if (turboSource != null && _drivetrain.hasTurbo)
            {
                float boostTarget = throttle * (_drivetrain.turboMaxBoost - 1f);
                _turboWhine = Mathf.MoveTowards(_turboWhine, boostTarget, Time.deltaTime * 3f);
                turboSource.volume = Mathf.Clamp01(_turboWhine * 0.7f);
                turboSource.pitch = 0.7f + _turboWhine * 0.8f;
            }

            // Blow-off on sudden throttle lift under boost
            if (blowOffSource != null && blowOffClip != null)
            {
                if (_prevThrottle > 0.6f && throttle < 0.2f && _turboWhine > 0.4f)
                {
                    blowOffSource.PlayOneShot(blowOffClip);
                }
            }

            // ALS / Pop & Bang on high RPM decel
            if (exhaustPopSource != null && popClips != null && popClips.Length > 0)
            {
                if (throttle < 0.1f && rpmNorm > 0.7f && Random.value < 0.02f)
                {
                    exhaustPopSource.PlayOneShot(popClips[Random.Range(0, popClips.Length)]);
                }
            }

            UpdateSurfaceAudio();

            _prevThrottle = throttle;
        }

        private void SetLayer(AudioSource src, float rpmNorm, float min, float peak, float max)
        {
            if (src == null) return;

            float vol = 0f;
            if (rpmNorm >= min && rpmNorm <= max)
            {
                if (rpmNorm < peak)
                    vol = Mathf.InverseLerp(min, peak, rpmNorm);
                else
                    vol = Mathf.InverseLerp(max, peak, rpmNorm);
            }
            src.volume = vol;

            if (!src.isPlaying && vol > 0.01f) src.Play();
        }

        private void ApplyPitch(AudioSource src, float pitch)
        {
            if (src != null) src.pitch = pitch;
        }

        private void UpdateSurfaceAudio()
        {
            if (_vehicle == null || _vehicle.wheels == null) return;

            BiomeType dominant = BiomeType.Grassland;
            int grounded = 0;
            foreach (var w in _vehicle.wheels)
            {
                if (w.isGrounded)
                {
                    grounded++;
                    dominant = w.surfaceBiome;
                }
            }

            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            float speedFactor = Mathf.Clamp01(speed / 20f);

            SetSurface(tireSource, dominant == BiomeType.City || dominant == BiomeType.Grassland, speedFactor);
            SetSurface(mudSource, dominant == BiomeType.Mud, speedFactor);
            SetSurface(sandSource, dominant == BiomeType.Desert || dominant == BiomeType.Beach, speedFactor);
            SetSurface(waterSource, dominant == BiomeType.Ocean, speedFactor);
        }

        private void SetSurface(AudioSource src, bool active, float volume)
        {
            if (src == null) return;
            src.volume = active ? volume * 0.7f : 0f;
            if (active && !src.isPlaying) src.Play();
        }
    }
}
