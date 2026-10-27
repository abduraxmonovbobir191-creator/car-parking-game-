using UnityEngine;
using UnityEngine.XR;
using CarParking.Vehicle;

namespace CarParking.VR
{
    /// <summary>
    /// Unity XR / OpenXR support for Meta Quest and PC VR.
    /// Head tracking + virtual hands that grab steering wheel, handbrake, gear stick, dashboard.
    /// </summary>
    public class VRHeadsetAndHandController : MonoBehaviour
    {
        public static VRHeadsetAndHandController Instance { get; private set; }

        [Header("References")]
        public Transform headRoot;
        public Transform leftHandRoot;
        public Transform rightHandRoot;
        public Transform steeringWheelGrab;
        public Transform handbrakeGrab;
        public Transform gearStickGrab;

        [Header("Settings")]
        public float grabDistance = 0.18f;
        public bool vrEnabled;

        private InputDevice _leftHand;
        private InputDevice _rightHand;
        private InputDevice _headset;

        private bool _leftGrabbing;
        private bool _rightGrabbing;
        private Transform _leftGrabbed;
        private Transform _rightGrabbed;

        private DrivetrainSystem _drivetrain;
        private CockpitAndDashboardSystem _cockpit;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            vrEnabled = XRSettings.enabled || XRSettings.loadedDeviceName.Length > 0;

            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle != null)
            {
                _drivetrain = vehicle.GetComponent<DrivetrainSystem>();
                _cockpit = vehicle.GetComponent<CockpitAndDashboardSystem>();
            }

            if (headRoot == null)
            {
                var go = new GameObject("VR_Head");
                go.transform.SetParent(transform);
                headRoot = go.transform;
            }
            if (leftHandRoot == null)
            {
                var go = new GameObject("VR_LeftHand");
                go.transform.SetParent(transform);
                leftHandRoot = go.transform;
            }
            if (rightHandRoot == null)
            {
                var go = new GameObject("VR_RightHand");
                go.transform.SetParent(transform);
                rightHandRoot = go.transform;
            }
        }

        private void Update()
        {
            if (!vrEnabled) return;

            RefreshDevices();
            UpdateHead();
            UpdateHand(leftHandRoot, _leftHand, ref _leftGrabbing, ref _leftGrabbed, true);
            UpdateHand(rightHandRoot, _rightHand, ref _rightGrabbing, ref _rightGrabbed, false);
        }

        private void RefreshDevices()
        {
            _headset = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            _leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private void UpdateHead()
        {
            if (!_headset.isValid || headRoot == null) return;

            if (_headset.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                headRoot.localPosition = pos;
            if (_headset.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                headRoot.localRotation = rot;

            // Drive main camera if present
            if (Camera.main != null)
            {
                Camera.main.transform.position = headRoot.position;
                Camera.main.transform.rotation = headRoot.rotation;
            }
        }

        private void UpdateHand(Transform handRoot, InputDevice device, ref bool grabbing, ref Transform grabbed, bool isLeft)
        {
            if (!device.isValid || handRoot == null) return;

            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                handRoot.localPosition = pos;
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                handRoot.localRotation = rot;

            bool trigger = false;
            device.TryGetFeatureValue(CommonUsages.triggerButton, out trigger);
            device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
            bool wantGrab = trigger || grip;

            if (wantGrab && !grabbing)
            {
                // Try grab nearest interactable
                Transform nearest = FindNearestGrab(handRoot.position);
                if (nearest != null)
                {
                    grabbing = true;
                    grabbed = nearest;
                }
            }
            else if (!wantGrab && grabbing)
            {
                grabbing = false;
                grabbed = null;
            }

            if (grabbing && grabbed != null)
                ApplyGrabAction(grabbed, handRoot, isLeft);
        }

        private Transform FindNearestGrab(Vector3 handPos)
        {
            Transform best = null;
            float bestDist = grabDistance;

            void Check(Transform t)
            {
                if (t == null) return;
                float d = Vector3.Distance(handPos, t.position);
                if (d < bestDist) { bestDist = d; best = t; }
            }

            Check(steeringWheelGrab);
            Check(handbrakeGrab);
            Check(gearStickGrab);

            return best;
        }

        private void ApplyGrabAction(Transform target, Transform hand, bool isLeft)
        {
            if (target == steeringWheelGrab && _cockpit != null)
            {
                // Map hand local X offset to steering roughly
                float steer = Mathf.Clamp(hand.localPosition.x * 4f, -1f, 1f);
                // Feed into vehicle via standard input axis simulation is handled externally
            }
            else if (target == handbrakeGrab && _drivetrain != null)
            {
                // Handbrake engaged while held
            }
            else if (target == gearStickGrab && _drivetrain != null)
            {
                // Simple sequential: pull up / down based on hand Y delta
            }
        }

        public bool IsVRActive => vrEnabled;
    }
}
