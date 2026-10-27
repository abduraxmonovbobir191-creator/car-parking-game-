using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarParking.Mechanic
{
    /// <summary>
    /// MODULE 37 — Forced Induction, Cooling & Exhaust Pipe Routing
    /// Twin-turbo, supercharger, intercooler, wastegate, exhaust path, IAT, boost.
    /// </summary>
    public class ForcedInductionAndExhaustMechanic : MonoBehaviour
    {
        public static ForcedInductionAndExhaustMechanic Instance { get; private set; }

        [Header("Turbo")]
        public bool twinTurbo = true;
        public float turboShaftPlay = 0f;
        public float wastegatePressureBar = 1.2f;
        public float currentBoostBar = 0f;
        public float maxBoostBar = 2.0f;
        public float compressorEfficiency = 0.72f;

        [Header("Temperatures")]
        public float intakeAirTempC = 25f;
        public float intercoolerEfficiency = 0.65f;
        public float exhaustGasTempC = 400f;

        [Header("Exhaust")]
        public bool equalLengthHeaders = true;
        public float backpressureBar = 0.15f;
        public float mufflerValveOpen = 0.5f;
        public bool catalyticConverterHealthy = true;

        [Header("Supercharger")]
        public bool hasSupercharger = false;
        public float superchargerDriveRatio = 2.8f;

        private float _spool;
        private float _boostCreep;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void UpdateForcedInduction(float rpm, float throttle, float ambientTempC, float dt)
        {
            float targetBoost = 0f;
            if (throttle > 0.3f && rpm > 2500f)
            {
                float spoolRate = twinTurbo ? 3.5f : 2.2f;
                _spool = Mathf.MoveTowards(_spool, 1f, spoolRate * throttle * dt);
                targetBoost = Mathf.Lerp(0.2f, maxBoostBar, _spool) * throttle;
                if (hasSupercharger)
                    targetBoost += (rpm / 7000f) * superchargerDriveRatio * 0.15f;
            }
            else
            {
                _spool = Mathf.MoveTowards(_spool, 0f, 2f * dt);
                targetBoost = 0f;
            }

            currentBoostBar = Mathf.Lerp(currentBoostBar, Mathf.Min(targetBoost, wastegatePressureBar), dt * 4f);

            if (currentBoostBar > wastegatePressureBar * 0.95f && rpm > 5500f)
                _boostCreep += dt * 0.02f;
            else
                _boostCreep = Mathf.Max(0f, _boostCreep - dt * 0.05f);
            currentBoostBar += _boostCreep;

            float compressedTemp = ambientTempC + (currentBoostBar * 45f) / compressorEfficiency;
            float afterIntercooler = Mathf.Lerp(compressedTemp, ambientTempC + 10f, intercoolerEfficiency);
            intakeAirTempC = Mathf.Lerp(intakeAirTempC, afterIntercooler, dt * 2f);

            float load = throttle * (rpm / 7000f);
            exhaustGasTempC = Mathf.Lerp(exhaustGasTempC, 350f + load * 550f + currentBoostBar * 80f, dt * 0.5f);

            if (currentBoostBar > 1.8f)
                turboShaftPlay = Mathf.Clamp01(turboShaftPlay + dt * 0.0003f);

            float valveFactor = 1f - mufflerValveOpen * 0.4f;
            backpressureBar = 0.08f + (equalLengthHeaders ? 0.02f : 0.06f) + (catalyticConverterHealthy ? 0.04f : 0.12f);
            backpressureBar *= valveFactor;
        }

        public float GetPowerMultiplier()
        {
            float boostMul = 1f + currentBoostBar * 0.35f;
            float tempPenalty = Mathf.Clamp01((intakeAirTempC - 25f) / 80f) * 0.15f;
            float wearPenalty = turboShaftPlay * 0.2f;
            return Mathf.Max(0.6f, boostMul - tempPenalty - wearPenalty);
        }

        public string GetForcedInductionReport()
        {
            return $"Boost: {currentBoostBar:F2} bar | IAT: {intakeAirTempC:F1}C | EGT: {exhaustGasTempC:F0}C | ShaftPlay: {turboShaftPlay*100f:F1}% | Backpressure: {backpressureBar:F2}";
        }
    }
}
