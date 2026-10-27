using UnityEngine;
using CarParking.Vehicle;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Flying car mode: folding wings, 6-DOF flight, rocket boosters.
    /// </summary>
    public class FlyingCarAndRocketBooster : MonoBehaviour
    {
        [Header("Toggle")]
        public KeyCode flyModeKey = KeyCode.F;
        public KeyCode boostKey = KeyCode.LeftShift;
        public bool flyModeActive;

        [Header("Flight")]
        public float liftForce = 25000f;
        public float thrustForce = 40000f;
        public float boostForce = 80000f;
        public float turnTorque = 18000f;
        public float maxFlySpeed = 120f;
        public float hoverHeight = 15f;

        [Header("Visual")]
        public Transform[] wingTransforms;
        public float wingFoldAngle = 70f;
        public GameObject rocketVfx;

        private Rigidbody _rb;
        private RealisticVehicleController _vehicle;
        private DrivetrainSystem _drivetrain;
        private bool _boosting;
        private float[] _wingBaseX;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _vehicle = GetComponent<RealisticVehicleController>();
            _drivetrain = GetComponent<DrivetrainSystem>();

            if (wingTransforms != null && wingTransforms.Length > 0)
            {
                _wingBaseX = new float[wingTransforms.Length];
                for (int i = 0; i < wingTransforms.Length; i++)
                    if (wingTransforms[i] != null)
                        _wingBaseX[i] = wingTransforms[i].localEulerAngles.x;
            }
        }

        private void Update()
        {
            // Only toggle fly if not used by character enter — use Alt+F or dedicated
            if (Input.GetKeyDown(KeyCode.B) && Input.GetKey(KeyCode.LeftAlt))
            {
                flyModeActive = !flyModeActive;
                OnFlyModeChanged();
            }

            _boosting = flyModeActive && Input.GetKey(boostKey);

            if (rocketVfx != null)
                rocketVfx.SetActive(_boosting);

            AnimateWings();
        }

        private void FixedUpdate()
        {
            if (!flyModeActive || _rb == null) return;

            float pitch = Input.GetAxis("Vertical");
            float roll = -Input.GetAxis("Horizontal");
            float yaw = 0f;
            if (Input.GetKey(KeyCode.Q)) yaw = -1f;
            if (Input.GetKey(KeyCode.E)) yaw = 1f;

            // Lift / hover
            float altitudeError = hoverHeight - transform.position.y;
            Vector3 lift = Vector3.up * (liftForce + altitudeError * 2000f);
            _rb.AddForce(lift * Time.fixedDeltaTime, ForceMode.Force);

            // Thrust
            float throttle = Mathf.Max(0f, pitch);
            float thrust = _boosting ? boostForce : thrustForce;
            _rb.AddForce(transform.forward * throttle * thrust * Time.fixedDeltaTime, ForceMode.Force);

            // 6-DOF torque
            Vector3 torque = transform.right * pitch * turnTorque
                           + transform.forward * roll * turnTorque
                           + transform.up * yaw * turnTorque;
            _rb.AddTorque(torque * Time.fixedDeltaTime, ForceMode.Force);

            // Speed limit
            if (_rb.linearVelocity.magnitude > maxFlySpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxFlySpeed;

            // Reduce ground vehicle interference
            if (_vehicle != null) _vehicle.enabled = false;
            if (_drivetrain != null) _drivetrain.enabled = false;
        }

        private void OnFlyModeChanged()
        {
            if (flyModeActive)
            {
                Debug.Log("[Fly] Flight mode ENGAGED — wings deployed");
                if (_rb != null)
                {
                    _rb.useGravity = true;
                    _rb.linearDamping = 0.3f;
                    _rb.angularDamping = 1.5f;
                }
            }
            else
            {
                Debug.Log("[Fly] Flight mode OFF — landing");
                if (_vehicle != null) _vehicle.enabled = true;
                if (_drivetrain != null) _drivetrain.enabled = true;
                if (_rb != null)
                {
                    _rb.linearDamping = 0.05f;
                    _rb.angularDamping = 0.5f;
                }
            }
        }

        private void AnimateWings()
        {
            if (wingTransforms == null || _wingBaseX == null) return;
            float target = flyModeActive ? wingFoldAngle : 0f;
            for (int i = 0; i < wingTransforms.Length; i++)
            {
                if (wingTransforms[i] == null) continue;
                Vector3 e = wingTransforms[i].localEulerAngles;
                float baseX = _wingBaseX[i];
                float x = Mathf.LerpAngle(e.x, baseX + target, Time.deltaTime * 3f);
                wingTransforms[i].localEulerAngles = new Vector3(x, e.y, e.z);
            }
        }

        public bool IsFlying => flyModeActive;
    }
}
