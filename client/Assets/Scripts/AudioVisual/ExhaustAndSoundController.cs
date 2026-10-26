using UnityEngine;
using NextGenDrive.Physics;

namespace NextGenDrive.AudioVisual
{
    public class ExhaustAndSoundController : MonoBehaviour
    {
        [Header("References")]
        public RealisticCarController carController;

        [Header("Audio Sources")]
        public AudioSource engineLowSource;
        public AudioSource engineHighSource;
        public AudioSource turboBlowOffSource;
        public AudioSource exhaustBackfireSource;

        [Header("Audio Clips")]
        public AudioClip[] popAndBangClips;
        public AudioClip turboFlutterClip;

        [Header("Exhaust Flame VFX")]
        public ParticleSystem[] exhaustFlameParticles;
        public Light[] exhaustFlashLights;
        public float popRpmThreshold = 5200f;

        private float previousThrottle = 0f;
        private int previousGear = 2;
        private float backfireCooldown = 0f;
        private float lightFlashTimer = 0f;

        private void Update()
        {
            if (carController == null) return;

            UpdateEnginePitchAndBlend();
            DetectBackfireAndTurboFlutter();
            UpdateExhaustFlashLight();
        }

        private void UpdateEnginePitchAndBlend()
        {
            float rpmNormalized = Mathf.InverseLerp(carController.idleRPM, carController.maxRPM, carController.currentRPM);
            float basePitch = Mathf.Lerp(0.75f, 2.35f, rpmNormalized);

            if (engineLowSource != null)
            {
                engineLowSource.pitch = basePitch;
                engineLowSource.volume = Mathf.Lerp(0.85f, 0.2f, rpmNormalized);
            }

            if (engineHighSource != null)
            {
                engineHighSource.pitch = basePitch * 0.92f;
                engineHighSource.volume = Mathf.Lerp(0.05f, 1.0f, rpmNormalized);
            }
        }

        private void DetectBackfireAndTurboFlutter()
        {
            backfireCooldown -= Time.deltaTime;
            float currentRPM = carController.currentRPM;
            float currentThrottle = Input.GetAxis("Vertical");

            // Trigger Turbo Flutter & Pop-and-Bangs when suddenly releasing throttle at high RPM
            bool suddenLiftOff = (previousThrottle > 0.65f && currentThrottle < 0.15f);
            bool gearShiftedUp = (carController.currentGear > previousGear && currentRPM > 4500f);

            if ((suddenLiftOff || gearShiftedUp) && currentRPM >= popRpmThreshold && backfireCooldown <= 0f)
            {
                TriggerPopAndBangs();
                if (suddenLiftOff && turboBlowOffSource != null && turboFlutterClip != null)
                {
                    turboBlowOffSource.PlayOneShot(turboFlutterClip, 0.85f);
                }
                backfireCooldown = Random.Range(0.14f, 0.35f);
            }

            previousThrottle = currentThrottle;
            previousGear = carController.currentGear;
        }

        private void TriggerPopAndBangs()
        {
            foreach (var ps in exhaustFlameParticles)
            {
                if (ps != null) ps.Play();
            }

            if (exhaustBackfireSource != null && popAndBangClips != null && popAndBangClips.Length > 0)
            {
                AudioClip clip = popAndBangClips[Random.Range(0, popAndBangClips.Length)];
                exhaustBackfireSource.pitch = Random.Range(0.90f, 1.12f);
                exhaustBackfireSource.PlayOneShot(clip, 1.0f);
            }

            lightFlashTimer = 0.08f;
        }

        private void UpdateExhaustFlashLight()
        {
            bool active = lightFlashTimer > 0f;
            if (active) lightFlashTimer -= Time.deltaTime;

            foreach (var l in exhaustFlashLights)
            {
                if (l != null) l.enabled = active;
            }
        }
    }
}
