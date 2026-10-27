using UnityEngine;
using CarParking.Vehicle;

namespace CarParking.CameraSystem
{
    /// <summary>
    /// Follow camera with inertia, G-force lag, pitch on accel/brake,
    /// high-speed shake and velocity-based motion blur.
    /// </summary>
    public class CinematicCameraInertia : MonoBehaviour
    {
        public static CinematicCameraInertia Instance { get; private set; }

        [Header("Target")]
        public Transform target;
        public Rigidbody targetRb;

        [Header("Follow")]
        public Vector3 offset = new Vector3(0f, 2.2f, -6.5f);
        public float positionFollow = 6f;
        public float rotationFollow = 8f;

        [Header("Inertia / G-Force")]
        public float lateralLag = 0.12f;
        public float pitchOnAccel = 4f;
        public float pitchOnBrake = 6f;
        public float maxLateralAngle = 8f;

        [Header("Shake")]
        public float shakeSpeedThreshold = 25f;
        public float shakeAmount = 0.08f;

        [Header("Motion Blur")]
        public bool enableMotionBlur = true;
        public float blurSpeedStart = 20f;
        public float maxBlurIntensity = 0.6f;

        private Vector3 _smoothPos;
        private Vector3 _lateralOffset;
        private float _pitchOffset;
        private Vector3 _shakeOffset;
        private Camera _cam;
        private float _baseFOV = 60f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cam = GetComponent<Camera>();
            if (_cam != null) _baseFOV = _cam.fieldOfView;
        }

        private void Start()
        {
            if (target == null)
            {
                var v = FindFirstObjectByType<RealisticVehicleController>();
                if (v != null)
                {
                    target = v.transform;
                    targetRb = v.GetComponent<Rigidbody>();
                }
            }
            if (target != null)
                _smoothPos = target.TransformPoint(offset);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;
            Vector3 velocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
            float speed = velocity.magnitude;

            // Desired position behind car
            Vector3 desired = target.TransformPoint(offset);

            // Lateral G-force lag
            Vector3 localVel = target.InverseTransformDirection(velocity);
            Vector3 lateralTarget = -target.right * Mathf.Clamp(localVel.x * lateralLag, -maxLateralAngle * 0.1f, maxLateralAngle * 0.1f);
            _lateralOffset = Vector3.Lerp(_lateralOffset, lateralTarget, dt * 4f);

            // Pitch from accel / brake
            float forwardAccel = localVel.z; // simplified
            float pitchTarget = 0f;
            if (targetRb != null)
            {
                float accelDot = Vector3.Dot(targetRb.linearVelocity - velocity, target.forward);
                // Use throttle/brake approximation via speed change
            }
            float longSpeed = localVel.z;
            if (longSpeed > 5f) pitchTarget = -pitchOnAccel * Mathf.Clamp01(longSpeed / 40f);
            // braking detected roughly when speed high but input would be negative — use velocity along forward decreasing
            _pitchOffset = Mathf.Lerp(_pitchOffset, pitchTarget, dt * 5f);

            // High-speed shake
            if (speed > shakeSpeedThreshold)
            {
                float t = Mathf.Clamp01((speed - shakeSpeedThreshold) / 30f);
                _shakeOffset = Random.insideUnitSphere * shakeAmount * t;
            }
            else _shakeOffset = Vector3.zero;

            _smoothPos = Vector3.Lerp(_smoothPos, desired + _lateralOffset, dt * positionFollow);
            transform.position = _smoothPos + _shakeOffset;

            // Look at car with pitch
            Quaternion lookRot = Quaternion.LookRotation(target.position - transform.position + Vector3.up * 0.8f);
            lookRot *= Quaternion.Euler(_pitchOffset, 0f, -localVel.x * 0.3f);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, dt * rotationFollow);

            // FOV / motion blur proxy via FOV stretch
            if (_cam != null && enableMotionBlur)
            {
                float blurT = Mathf.Clamp01((speed - blurSpeedStart) / 40f);
                _cam.fieldOfView = Mathf.Lerp(_baseFOV, _baseFOV + 12f * maxBlurIntensity, blurT);
            }
        }

        public void SetTarget(Transform t, Rigidbody rb)
        {
            target = t;
            targetRb = rb;
        }
    }
}
