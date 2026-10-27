using UnityEngine;
using CarParking.World;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Off-road winch (lebyodka) and recovery system.
    /// Attach to trees, rocks, bridge pylons or other vehicles.
    /// Catenary rope rendering + physical pull force.
    /// </summary>
    public class OffRoadWinchAndRecoverySystem : MonoBehaviour
    {
        [Header("Winch Settings")]
        public float maxCableLength = 35f;
        public float maxPullForce = 28000f;
        public float reelSpeed = 4.5f;
        public float cableSnapForce = 45000f;
        public LayerMask anchorMask;

        [Header("References")]
        public Transform winchOrigin;
        public LineRenderer cableRenderer;
        public int cableSegments = 16;

        [Header("Controls")]
        public KeyCode attachKey = KeyCode.T;
        public KeyCode reelInKey = KeyCode.R;
        public KeyCode freeSpoolKey = KeyCode.G;

        private bool _isAttached;
        private bool _isReeling;
        private bool _isFreeSpool;
        private Vector3 _anchorPoint;
        private Transform _anchorTransform;
        private float _currentCableLength;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (winchOrigin == null)
            {
                var go = new GameObject("WinchOrigin");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(0f, 0.6f, 1.8f);
                winchOrigin = go.transform;
            }

            if (cableRenderer == null)
            {
                cableRenderer = gameObject.AddComponent<LineRenderer>();
                cableRenderer.positionCount = cableSegments;
                cableRenderer.startWidth = 0.04f;
                cableRenderer.endWidth = 0.03f;
                cableRenderer.material = new Material(Shader.Find("Sprites/Default"));
                cableRenderer.startColor = new Color(0.15f, 0.15f, 0.15f);
                cableRenderer.endColor = new Color(0.25f, 0.25f, 0.25f);
            }

            cableRenderer.enabled = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(attachKey))
            {
                if (_isAttached) Detach();
                else TryAttach();
            }

            _isReeling = Input.GetKey(reelInKey);
            _isFreeSpool = Input.GetKey(freeSpoolKey);

            if (_isAttached)
                UpdateCableVisual();
        }

        private void FixedUpdate()
        {
            if (!_isAttached || _rb == null) return;

            Vector3 origin = winchOrigin.position;
            Vector3 anchor = _anchorTransform != null ? _anchorTransform.position : _anchorPoint;
            Vector3 toAnchor = anchor - origin;
            float distance = toAnchor.magnitude;

            if (distance > maxCableLength + 1.5f)
            {
                // Overstretched - risk of snap
                if (distance > maxCableLength * 1.15f)
                {
                    Debug.Log("[Winch] Cable snapped!");
                    Detach();
                    return;
                }
            }

            if (_isFreeSpool)
            {
                _currentCableLength = Mathf.Min(distance + 0.5f, maxCableLength);
                return;
            }

            if (_isReeling)
            {
                _currentCableLength = Mathf.Max(2f, _currentCableLength - reelSpeed * Time.fixedDeltaTime);
            }

            if (distance > _currentCableLength)
            {
                Vector3 pullDir = toAnchor.normalized;
                float excess = distance - _currentCableLength;
                float forceMag = Mathf.Min(excess * 1800f, maxPullForce);

                _rb.AddForceAtPosition(pullDir * forceMag, origin);

                // If anchored to another rigidbody, pull it too
                if (_anchorTransform != null)
                {
                    var otherRb = _anchorTransform.GetComponentInParent<Rigidbody>();
                    if (otherRb != null && otherRb != _rb)
                        otherRb.AddForceAtPosition(-pullDir * forceMag * 0.7f, anchor);
                }
            }
        }

        private void TryAttach()
        {
            Vector3 origin = winchOrigin.position;
            Vector3 forward = transform.forward;

            // SphereCast + Raycast for better detection
            if (Physics.SphereCast(origin, 0.3f, forward, out RaycastHit hit, maxCableLength, anchorMask))
            {
                _anchorPoint = hit.point;
                _anchorTransform = hit.transform;
                _currentCableLength = hit.distance;
                _isAttached = true;
                cableRenderer.enabled = true;
                Debug.Log($"[Winch] Attached to {hit.collider.name}");
            }
            else if (Physics.Raycast(origin, forward, out hit, maxCableLength, anchorMask))
            {
                _anchorPoint = hit.point;
                _anchorTransform = hit.transform;
                _currentCableLength = hit.distance;
                _isAttached = true;
                cableRenderer.enabled = true;
            }
        }

        private void Detach()
        {
            _isAttached = false;
            _anchorTransform = null;
            cableRenderer.enabled = false;
        }

        private void UpdateCableVisual()
        {
            Vector3 start = winchOrigin.position;
            Vector3 end = _anchorTransform != null ? _anchorTransform.position : _anchorPoint;

            float distance = Vector3.Distance(start, end);
            float sag = 0f;

            if (distance < _currentCableLength)
            {
                // Slack - apply catenary-like sag
                sag = (_currentCableLength - distance) * 0.35f;
            }

            for (int i = 0; i < cableSegments; i++)
            {
                float t = i / (float)(cableSegments - 1);
                Vector3 point = Vector3.Lerp(start, end, t);

                // Parabolic sag
                float sagAmount = sag * 4f * t * (1f - t);
                point.y -= sagAmount;

                cableRenderer.SetPosition(i, point);
            }
        }

        public bool IsAttached => _isAttached;
        public float CableTension
        {
            get
            {
                if (!_isAttached) return 0f;
                Vector3 origin = winchOrigin.position;
                Vector3 anchor = _anchorTransform != null ? _anchorTransform.position : _anchorPoint;
                float dist = Vector3.Distance(origin, anchor);
                return Mathf.Clamp01((dist - _currentCableLength) / 3f);
            }
        }
    }
}
