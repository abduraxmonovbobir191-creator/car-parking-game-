using UnityEngine;

namespace CarParking.Mechanic
{
    /// <summary>
    /// MODULE 39 — Suspension Geometry, Brake Hydraulics & Tire Compound Lab
    /// Spring rates, damping, anti-roll, brake bias, fluid boiling, rotor types.
    /// </summary>
    public class SuspensionAndBrakeHydraulicsMechanic : MonoBehaviour
    {
        public static SuspensionAndBrakeHydraulicsMechanic Instance { get; private set; }

        public enum SuspensionType { MacPherson, DoubleWishbone, Pushrod }
        public SuspensionType frontType = SuspensionType.DoubleWishbone;
        public SuspensionType rearType = SuspensionType.DoubleWishbone;

        public float frontSpringRate = 85000f;
        public float rearSpringRate = 75000f;
        public float frontCompressionDamping = 3500f;
        public float frontReboundDamping = 4200f;
        public float antiRollFront = 18000f;
        public float antiRollRear = 14000f;

        public float brakeBiasFront = 0.62f;
        public float brakeFluidTempC = 40f;
        public float brakeFluidBoilingPoint = 260f;
        public bool vaporLock;
        public float frontRotorTempC = 50f;
        public float rearRotorTempC = 45f;
        public bool carbonCeramicRotors;

        public float tireCompoundGrip = 1.0f;
        public float tireTempC = 30f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void UpdateSuspensionAndBrakes(float speedKmh, float brakeInput, float lateralG, float dt)
        {
            float heatIn = brakeInput * speedKmh * 0.8f;
            frontRotorTempC = Mathf.Lerp(frontRotorTempC, 40f + heatIn * 2.2f, dt * 0.3f);
            rearRotorTempC = Mathf.Lerp(rearRotorTempC, 35f + heatIn * 1.6f, dt * 0.3f);

            if (carbonCeramicRotors)
            {
                frontRotorTempC *= 0.75f;
                rearRotorTempC *= 0.75f;
            }

            brakeFluidTempC = Mathf.Lerp(brakeFluidTempC, (frontRotorTempC + rearRotorTempC) * 0.35f, dt * 0.2f);
            vaporLock = brakeFluidTempC > brakeFluidBoilingPoint;

            tireTempC = Mathf.Lerp(tireTempC, 25f + speedKmh * 0.15f + Mathf.Abs(lateralG) * 12f, dt * 0.4f);
            tireCompoundGrip = 1f - Mathf.Clamp01((tireTempC - 90f) / 80f) * 0.25f;
        }

        public float GetBrakeEffectiveness()
        {
            if (vaporLock) return 0.3f;
            float fade = Mathf.Clamp01((frontRotorTempC - 500f) / 200f);
            return Mathf.Max(0.4f, 1f - fade * 0.5f);
        }

        public string GetChassisReport()
        {
            return $"Springs F/R {frontSpringRate:F0}/{rearSpringRate:F0} | Rotors F/R {frontRotorTempC:F0}/{rearRotorTempC:F0}C | Fluid {brakeFluidTempC:F0}C {(vaporLock?"VAPOR LOCK":"OK")} | Tire {tireTempC:F0}C grip {tireCompoundGrip:F2}";
        }
    }
}
