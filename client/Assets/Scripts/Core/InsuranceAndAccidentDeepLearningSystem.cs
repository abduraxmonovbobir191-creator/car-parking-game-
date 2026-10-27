using UnityEngine;
using CarParking.Vehicle;
using CarParking.Core;
using CarParking.AI;

namespace CarParking.Core
{
    /// <summary>
    /// Collision analysis: impact vectors, structural damage, fault scoring,
    /// automatic insurance claim or reckless penalty.
    /// </summary>
    public class InsuranceAndAccidentDeepLearningSystem : MonoBehaviour
    {
        public static InsuranceAndAccidentDeepLearningSystem Instance { get; private set; }

        [Header("Analysis")]
        public float minImpulseToAnalyze = 3000f;
        public float recklessSpeedThreshold = 25f;

        [Header("Weights (pseudo neural)")]
        public float wImpulse = 0.35f;
        public float wSpeed = 0.25f;
        public float wDamage = 0.25f;
        public float wFault = 0.15f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Call from vehicle collision handler with impact data.
        /// </summary>
        public AccidentReport AnalyzeCollision(
            GameObject vehicle,
            Collision collision,
            float vehicleSpeed,
            float structuralDamage01,
            bool playerAtFault)
        {
            float impulse = collision.impulse.magnitude;
            if (impulse < minImpulseToAnalyze)
                return null;

            ContactPoint cp = collision.GetContact(0);
            Vector3 impactDir = collision.relativeVelocity.normalized;

            // Feature vector
            float fImpulse = Mathf.Clamp01(impulse / 25000f);
            float fSpeed = Mathf.Clamp01(vehicleSpeed / 50f);
            float fDamage = Mathf.Clamp01(structuralDamage01);
            float fFault = playerAtFault ? 1f : 0.2f;

            // Simple dense "network" score
            float severity = fImpulse * wImpulse + fSpeed * wSpeed + fDamage * wDamage + fFault * wFault;
            severity = Mathf.Clamp01(severity);

            float repairCost = 200f + severity * 8000f + fDamage * 5000f;
            bool reckless = vehicleSpeed > recklessSpeedThreshold && playerAtFault;

            var report = new AccidentReport
            {
                severity = severity,
                repairCost = repairCost,
                playerAtFault = playerAtFault,
                reckless = reckless,
                impactPoint = cp.point,
                impactDirection = impactDir,
                impulse = impulse,
                speed = vehicleSpeed
            };

            ProcessClaim(report);
            return report;
        }

        private void ProcessClaim(AccidentReport report)
        {
            var economy = WorldServicesAndEconomySystem.Instance;
            var insurance = OBDScannerAndEconomySystem.Instance;

            if (report.reckless)
            {
                // Penalty + wanted
                float fine = report.repairCost * 0.15f;
                if (economy != null)
                    economy.playerCash = Mathf.Max(0f, economy.playerCash - fine);

                if (PolicePursuitAndRadarSystem.Instance != null && report.severity > 0.5f)
                    PolicePursuitAndRadarSystem.Instance.IncreaseWanted(1);

                Debug.Log($"[AccidentAI] Reckless driving. Fine ${fine:N0}. Severity {report.severity:F2}");
            }

            if (insurance != null && insurance.hasInsurance && !report.reckless)
            {
                float covered = insurance.ClaimInsurance(report.repairCost);
                float playerPays = report.repairCost - covered;
                if (economy != null && playerPays > 0f)
                    economy.playerCash = Mathf.Max(0f, economy.playerCash - playerPays);

                Debug.Log($"[AccidentAI] Insurance covered ${covered:N0}. Player pays ${playerPays:N0}");
            }
            else if (economy != null)
            {
                economy.playerCash = Mathf.Max(0f, economy.playerCash - report.repairCost * 0.5f);
                Debug.Log($"[AccidentAI] No insurance. Partial cost charged.");
            }

            // Notify crowd
            if (DynamicCrowdReactionSystem.Instance != null)
                DynamicCrowdReactionSystem.Instance.NotifyCrash(report.impactPoint);
        }

        public class AccidentReport
        {
            public float severity;
            public float repairCost;
            public bool playerAtFault;
            public bool reckless;
            public Vector3 impactPoint;
            public Vector3 impactDirection;
            public float impulse;
            public float speed;
        }
    }

    /// <summary>
    /// Attach to vehicle to feed collisions into the insurance AI.
    /// </summary>
    public class AccidentReportForwarder : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            if (InsuranceAndAccidentDeepLearningSystem.Instance == null) return;

            var rb = GetComponent<Rigidbody>();
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;

            float dmg = 0f;
            var damage = GetComponent<VehicleDamageAndDeformation>();
            if (damage != null)
                dmg = 1f - (damage.engineHealth + damage.radiatorHealth) * 0.5f;

            bool atFault = speed > 8f; // simplified: moving vehicle is at fault in this heuristic

            InsuranceAndAccidentDeepLearningSystem.Instance.AnalyzeCollision(
                gameObject, collision, speed, dmg, atFault);
        }
    }
}
