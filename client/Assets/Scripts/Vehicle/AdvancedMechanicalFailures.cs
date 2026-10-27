using UnityEngine;
using CarParking.Vehicle;
using CarParking.World;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Advanced soft-body mechanical failures: punctured tires, bent axle (steering pull),
    /// engine block damage, fuel leaks with fire ignition risk.
    /// Integrates with VehicleDamageAndDeformation and DrivetrainSystem.
    /// </summary>
    public class AdvancedMechanicalFailures : MonoBehaviour
    {
        [Header("Tire Puncture")]
        public float[] tirePressure = { 1f, 1f, 1f, 1f };
        public float punctureThresholdImpulse = 12000f;
        public float flatTireGripMult = 0.25f;
        public float flatTireRadiusScale = 0.82f;

        [Header("Axle / Steering")]
        public float axleHealth = 1f;
        public float steeringPullAmount;

        [Header("Engine Block")]
        public float engineBlockHealth = 1f;
        public float overheatRate = 0.02f;

        [Header("Fuel Leak & Fire")]
        public float fuelTankHealth = 1f;
        public float fuelLeakRate;
        public bool isLeaking;
        public bool isOnFire;
        public float fireDamagePerSecond = 0.08f;
        public GameObject fireVfxPrefab;

        private RealisticVehicleController _vehicle;
        private DrivetrainSystem _drivetrain;
        private VehicleDamageAndDeformation _damage;
        private Rigidbody _rb;
        private GameObject _fireInstance;
        private float _engineTemp = 0.4f;

        private void Awake()
        {
            _vehicle = GetComponent<RealisticVehicleController>();
            _drivetrain = GetComponent<DrivetrainSystem>();
            _damage = GetComponent<VehicleDamageAndDeformation>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision col)
        {
            if (col.contactCount == 0) return;
            float impulse = col.impulse.magnitude;
            ContactPoint cp = col.GetContact(0);
            Vector3 local = transform.InverseTransformPoint(cp.point);

            // Tire puncture — impact near wheel
            if (_vehicle != null && _vehicle.wheels != null && impulse > punctureThresholdImpulse)
            {
                for (int i = 0; i < _vehicle.wheels.Length && i < tirePressure.Length; i++)
                {
                    float d = Vector3.Distance(cp.point, _vehicle.wheels[i].wheelTransform.position);
                    if (d < 1.1f)
                    {
                        tirePressure[i] = 0f;
                        Debug.Log($"[MechFail] Tire {i} punctured");
                    }
                }
            }

            // Axle bend — side impact
            if (Mathf.Abs(local.x) > 0.7f && impulse > 9000f)
            {
                axleHealth = Mathf.Max(0f, axleHealth - impulse / 40000f);
                steeringPullAmount = (1f - axleHealth) * Mathf.Sign(local.x) * 0.35f;
            }

            // Engine block — front hard impact
            if (local.z > 1.2f && impulse > 15000f)
            {
                engineBlockHealth = Mathf.Max(0f, engineBlockHealth - impulse / 50000f);
                if (_drivetrain != null)
                    _drivetrain.maxTorque *= Mathf.Lerp(0.3f, 1f, engineBlockHealth);
            }

            // Fuel tank — rear impact
            if (local.z < -1.2f && impulse > 10000f)
            {
                fuelTankHealth = Mathf.Max(0f, fuelTankHealth - impulse / 35000f);
                if (fuelTankHealth < 0.6f)
                {
                    isLeaking = true;
                    fuelLeakRate = (0.6f - fuelTankHealth) * 0.05f;
                }
            }

            // Fire ignition from spark + leak
            if (isLeaking && impulse > 18000f && !isOnFire && Random.value > 0.4f)
                Ignite();
        }

        private void Update()
        {
            // Flat tire effects
            if (_vehicle != null && _vehicle.wheels != null)
            {
                for (int i = 0; i < _vehicle.wheels.Length && i < tirePressure.Length; i++)
                {
                    if (tirePressure[i] < 0.15f)
                    {
                        // Reduce effective radius visually
                        var wt = _vehicle.wheels[i].wheelTransform;
                        if (wt != null)
                        {
                            Vector3 s = wt.localScale;
                            s.y = flatTireRadiusScale;
                            wt.localScale = s;
                        }
                    }
                }
            }

            // Steering pull from bent axle
            if (Mathf.Abs(steeringPullAmount) > 0.01f && _rb != null)
            {
                _rb.AddTorque(transform.up * steeringPullAmount * 800f * Time.deltaTime, ForceMode.Acceleration);
            }

            // Engine overheat when block damaged
            if (engineBlockHealth < 0.7f && _drivetrain != null && _drivetrain.throttleInput > 0.3f)
            {
                _engineTemp = Mathf.Min(1f, _engineTemp + overheatRate * (1f - engineBlockHealth) * Time.deltaTime);
                if (_engineTemp > 0.9f)
                    _drivetrain.maxRpm = Mathf.Lerp(_drivetrain.maxRpm, 4000f, Time.deltaTime);
            }
            else
            {
                _engineTemp = Mathf.Max(0.3f, _engineTemp - Time.deltaTime * 0.05f);
            }

            // Fuel leak drain
            if (isLeaking && WorldServicesAndEconomySystemExists())
            {
                // Drain handled by economy fuel system if present
            }

            // Fire damage
            if (isOnFire)
            {
                engineBlockHealth = Mathf.Max(0f, engineBlockHealth - fireDamagePerSecond * Time.deltaTime);
                fuelTankHealth = Mathf.Max(0f, fuelTankHealth - fireDamagePerSecond * 0.5f * Time.deltaTime);
                if (engineBlockHealth <= 0.05f)
                {
                    if (_drivetrain != null) _drivetrain.throttleInput = 0f;
                }
            }
        }

        private void Ignite()
        {
            isOnFire = true;
            if (fireVfxPrefab != null && _fireInstance == null)
            {
                _fireInstance = Instantiate(fireVfxPrefab, transform.position, Quaternion.identity, transform);
                _fireInstance.transform.localPosition = new Vector3(0f, 0.5f, -1.5f);
            }
            Debug.Log("[MechFail] Fuel fire ignited!");
        }

        public void ExtinguishFire()
        {
            isOnFire = false;
            if (_fireInstance != null) Destroy(_fireInstance);
            _fireInstance = null;
        }

        public void RepairAllMechanical()
        {
            for (int i = 0; i < tirePressure.Length; i++) tirePressure[i] = 1f;
            axleHealth = 1f;
            steeringPullAmount = 0f;
            engineBlockHealth = 1f;
            fuelTankHealth = 1f;
            isLeaking = false;
            fuelLeakRate = 0f;
            ExtinguishFire();
            _engineTemp = 0.4f;

            if (_drivetrain != null)
            {
                var tuning = GetComponent<VehicleTuningSystem>();
                if (tuning != null) tuning.ApplyEngineAndECU();
            }

            Debug.Log("[MechFail] All mechanical systems repaired");
        }

        private bool WorldServicesAndEconomySystemExists()
        {
            return FindFirstObjectByType<CarParking.Core.WorldServicesAndEconomySystem>() != null;
        }

        public float GetTireGripMultiplier(int wheelIndex)
        {
            if (wheelIndex < 0 || wheelIndex >= tirePressure.Length) return 1f;
            return tirePressure[wheelIndex] < 0.15f ? flatTireGripMult : Mathf.Lerp(flatTireGripMult, 1f, tirePressure[wheelIndex]);
        }
    }
}
