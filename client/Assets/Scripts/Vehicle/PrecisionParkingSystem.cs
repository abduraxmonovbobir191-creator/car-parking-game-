using UnityEngine;
using UnityEngine.UI;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Precision parking suite: ultrasonic sensors, 360 camera, trajectory lines, air suspension.
    /// </summary>
    public class PrecisionParkingSystem : MonoBehaviour
    {
        [Header("Sensors")]
        public Transform[] frontSensors;
        public Transform[] rearSensors;
        public float sensorRange = 3.5f;
        public LayerMask obstacleMask;

        [Header("Audio")]
        public AudioSource beepSource;
        public float minBeepInterval = 0.08f;
        public float maxBeepInterval = 0.6f;

        [Header("UI")]
        public Text frontDistanceText;
        public Text rearDistanceText;
        public RawImage birdEyeCameraImage;
        public LineRenderer trajectoryLine;

        [Header("Air Suspension")]
        public float[] heightLevels = { 0.12f, 0.22f, 0.35f };
        public int currentHeightLevel = 1;

        private float _beepTimer;
        private Camera _birdEyeCam;
        private RealisticVehicleController _vehicle;

        private void Start()
        {
            _vehicle = GetComponent<RealisticVehicleController>();

            // Create simple bird's-eye camera
            var camGo = new GameObject("BirdEyeParkingCam");
            camGo.transform.SetParent(transform);
            camGo.transform.localPosition = new Vector3(0f, 8f, 0f);
            camGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _birdEyeCam = camGo.AddComponent<Camera>();
            _birdEyeCam.orthographic = true;
            _birdEyeCam.orthographicSize = 7f;
            _birdEyeCam.enabled = false;

            if (birdEyeCameraImage != null)
            {
                var rt = new RenderTexture(512, 512, 16);
                _birdEyeCam.targetTexture = rt;
                birdEyeCameraImage.texture = rt;
            }
        }

        private void Update()
        {
            float frontDist = GetClosestDistance(frontSensors);
            float rearDist = GetClosestDistance(rearSensors);

            if (frontDistanceText) frontDistanceText.text = $"{frontDist * 100:F0} cm";
            if (rearDistanceText) rearDistanceText.text = $"{rearDist * 100:F0} cm";

            UpdateBeep(Mathf.Min(frontDist, rearDist));
            UpdateTrajectoryLine();

            if (Input.GetKeyDown(KeyCode.P))
                ToggleBirdEye();

            if (Input.GetKeyDown(KeyCode.H))
                CycleAirSuspension();
        }

        private float GetClosestDistance(Transform[] sensors)
        {
            float min = sensorRange;
            foreach (var s in sensors)
            {
                if (Physics.Raycast(s.position, s.forward, out RaycastHit hit, sensorRange, obstacleMask))
                {
                    if (hit.distance < min) min = hit.distance;
                }
            }
            return min;
        }

        private void UpdateBeep(float distance)
        {
            if (beepSource == null || distance >= sensorRange) return;

            float t = Mathf.InverseLerp(sensorRange, 0.15f, distance);
            float interval = Mathf.Lerp(maxBeepInterval, minBeepInterval, t);

            _beepTimer -= Time.deltaTime;
            if (_beepTimer <= 0f)
            {
                beepSource.Play();
                _beepTimer = interval;
            }
        }

        private void UpdateTrajectoryLine()
        {
            if (trajectoryLine == null || _vehicle == null) return;

            // Simple reverse trajectory prediction
            bool reversing = Input.GetAxis("Vertical") < -0.1f;
            trajectoryLine.enabled = reversing;

            if (!reversing) return;

            Vector3 pos = transform.position;
            Vector3 dir = -transform.forward;
            float steer = Input.GetAxis("Horizontal");

            trajectoryLine.positionCount = 12;
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                Vector3 offset = Quaternion.Euler(0f, steer * 35f * t, 0f) * dir * (t * 6f);
                trajectoryLine.SetPosition(i, pos + offset + Vector3.up * 0.1f);
            }
        }

        private void ToggleBirdEye()
        {
            if (_birdEyeCam == null) return;
            _birdEyeCam.enabled = !_birdEyeCam.enabled;
            if (birdEyeCameraImage) birdEyeCameraImage.enabled = _birdEyeCam.enabled;
        }

        private void CycleAirSuspension()
        {
            currentHeightLevel = (currentHeightLevel + 1) % heightLevels.Length;
            // In full implementation this would adjust suspensionRestLength on the vehicle
            Debug.Log($"Air Suspension Level: {currentHeightLevel} ({heightLevels[currentHeightLevel]:F2}m)");
        }
    }
}
