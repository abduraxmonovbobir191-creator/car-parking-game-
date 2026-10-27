using UnityEngine;

namespace CarParking.Mechanic
{
    /// <summary>
    /// MODULE 40 — Interactive Garage Lift, Dyno Cell & OBD Oscilloscope
    /// Hydraulic lift, chassis dyno, live HP/Torque/AFR/Boost curves.
    /// </summary>
    public class InteractiveGarageLiftAndDynoRoom : MonoBehaviour
    {
        public static InteractiveGarageLiftAndDynoRoom Instance { get; private set; }

        public bool liftRaised;
        public float liftHeight = 0f;
        public float maxLiftHeight = 1.8f;

        public bool dynoRunning;
        public float dynoSpeedKmh;
        public float dynoHp;
        public float dynoTorqueNm;
        public float dynoAfr = 14.7f;
        public float dynoBoost;

        public float[] hpCurve = new float[20];
        public float[] torqueCurve = new float[20];

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
                ToggleLift();

            float target = liftRaised ? maxLiftHeight : 0f;
            liftHeight = Mathf.MoveTowards(liftHeight, target, 0.4f * Time.deltaTime);

            if (dynoRunning)
                UpdateDyno(Time.deltaTime);
        }

        public void ToggleLift()
        {
            liftRaised = !liftRaised;
            Debug.Log(liftRaised ? "[Garage] Lift UP" : "[Garage] Lift DOWN");
        }

        public void StartDyno()
        {
            dynoRunning = true;
            Debug.Log("[Dyno] Rolling road started");
        }

        public void StopDyno() => dynoRunning = false;

        private void UpdateDyno(float dt)
        {
            dynoSpeedKmh = Mathf.MoveTowards(dynoSpeedKmh, 220f, 15f * dt);
            float rpmProxy = dynoSpeedKmh * 40f;
            dynoTorqueNm = 420f + Mathf.Sin(rpmProxy * 0.002f) * 80f;
            dynoHp = dynoTorqueNm * rpmProxy / 5252f * 0.85f;
            dynoBoost = ForcedInductionAndExhaustMechanic.Instance != null ? ForcedInductionAndExhaustMechanic.Instance.currentBoostBar : 0f;
            dynoAfr = 14.7f - dynoBoost * 0.8f;

            int idx = Mathf.Clamp(Mathf.FloorToInt(dynoSpeedKmh / 12f), 0, 19);
            hpCurve[idx] = Mathf.Lerp(hpCurve[idx], dynoHp, 0.2f);
            torqueCurve[idx] = Mathf.Lerp(torqueCurve[idx], dynoTorqueNm, 0.2f);
        }

        public string GetDynoReport()
        {
            return $"Dyno {(dynoRunning?"RUN":"STOP")} | {dynoSpeedKmh:F0} km/h | {dynoHp:F0} HP | {dynoTorqueNm:F0} Nm | AFR {dynoAfr:F1} | Boost {dynoBoost:F2}";
        }
    }
}
