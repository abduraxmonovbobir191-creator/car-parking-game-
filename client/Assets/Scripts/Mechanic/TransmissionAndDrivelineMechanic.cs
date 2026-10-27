using System;
using UnityEngine;

namespace CarParking.Mechanic
{
    /// <summary>
    /// MODULE 38 — Gearbox, Clutch, Differential & Driveshaft Rebuild
    /// Individual gears, synchros, clutch wear, differentials, CV joints.
    /// </summary>
    public class TransmissionAndDrivelineMechanic : MonoBehaviour
    {
        public static TransmissionAndDrivelineMechanic Instance { get; private set; }

        [Serializable]
        public class Gear
        {
            public int number;
            public float ratio;
            public float synchroWear;
            public bool isDamaged;
        }

        public Gear[] gears = new Gear[9];
        public float clutchWear = 0f;
        public float flywheelWear = 0f;
        public float driveshaftWear = 0f;
        public float frontDiffWear = 0f;
        public float rearDiffWear = 0f;
        public float centerDiffWear = 0f;
        public bool lockingDiffEngaged;
        public float currentTorqueNm;
        public int currentGear = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitGears();
        }

        private void InitGears()
        {
            float[] ratios = { 0f, 3.6f, 2.2f, 1.5f, 1.15f, 0.92f, 0.78f, 0.68f, 0.58f, -3.2f };
            for (int i = 0; i < 9; i++)
            {
                gears[i] = new Gear { number = i == 8 ? -1 : i+1, ratio = ratios[i+1], synchroWear = 0f };
            }
        }

        public void UpdateDriveline(float engineTorque, float clutchPedal, float rpm, float dt, bool isAWD)
        {
            currentTorqueNm = engineTorque * (1f - clutchWear * 0.4f);
            if (clutchPedal > 0.8f)
                clutchWear = Mathf.Clamp01(clutchWear + dt * 0.002f * (rpm / 5000f));

            float load = Mathf.Abs(engineTorque) / 600f;
            driveshaftWear = Mathf.Clamp01(driveshaftWear + load * dt * 0.0001f);

            if (isAWD)
            {
                frontDiffWear = Mathf.Clamp01(frontDiffWear + load * dt * 0.00008f);
                rearDiffWear = Mathf.Clamp01(rearDiffWear + load * dt * 0.00008f);
                centerDiffWear = Mathf.Clamp01(centerDiffWear + load * dt * 0.00005f);
            }
            else
            {
                rearDiffWear = Mathf.Clamp01(rearDiffWear + load * dt * 0.0001f);
            }

            if (currentGear >= 0 && currentGear < gears.Length && gears[currentGear].synchroWear > 0.7f)
                gears[currentGear].isDamaged = true;
        }

        public float GetGearRatio() => gears[Mathf.Clamp(currentGear, 0, 8)].ratio;

        public float GetEfficiency()
        {
            float eff = 1f;
            eff -= clutchWear * 0.15f;
            eff -= driveshaftWear * 0.08f;
            eff -= (frontDiffWear + rearDiffWear + centerDiffWear) * 0.05f;
            return Mathf.Max(0.55f, eff);
        }

        public string GetDrivelineReport()
        {
            return $"Gear {currentGear} ratio {GetGearRatio():F2} | Clutch {clutchWear*100f:F0}% | Driveshaft {driveshaftWear*100f:F0}% | Diffs F/R/C {frontDiffWear*100f:F0}/{rearDiffWear*100f:F0}/{centerDiffWear*100f:F0}";
        }
    }
}
