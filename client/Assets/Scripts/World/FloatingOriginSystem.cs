using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParking.World
{
    /// <summary>
    /// Production-ready Floating Origin system for continental-scale worlds.
    /// Prevents floating-point precision loss by periodically shifting the entire world
    /// so that the player/camera stays near (0,0,0).
    /// Compatible with Unity 6 + HDRP + Physics.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class FloatingOriginSystem : MonoBehaviour
    {
        public static FloatingOriginSystem Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Distance from origin at which a shift is triggered (metres)")]
        [SerializeField] private float threshold = 5000f;

        [Tooltip("How often (seconds) we check distance")]
        [SerializeField] private float checkInterval = 1.0f;

        [Header("References")]
        [Tooltip("The transform that represents the player / focus point (usually the car or camera)")]
        [SerializeField] private Transform focusTransform;

        // Total accumulated world offset (double precision)
        public Vector3d TotalOriginOffset { get; private set; } = Vector3d.zero;

        public event Action<Vector3> OnOriginShifted;

        private float _timer;
        private bool _isShifting;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (focusTransform == null || _isShifting) return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;

            Vector3 focusPos = focusTransform.position;
            if (focusPos.sqrMagnitude > threshold * threshold)
            {
                PerformOriginShift(-focusPos);
            }
        }

        /// <summary>
        /// Manually trigger an origin shift by the given delta.
        /// </summary>
        public void PerformOriginShift(Vector3 delta)
        {
            if (_isShifting) return;
            _isShifting = true;

            MoveAllRootTransforms(delta);
            MovePhysicsBodies(delta);

            TotalOriginOffset += new Vector3d(delta.x, delta.y, delta.z);

            OnOriginShifted?.Invoke(delta);

            _isShifting = false;

#if UNITY_EDITOR
            Debug.Log($"[FloatingOrigin] Shifted by {delta} | Total offset: {TotalOriginOffset.x:F1}, {TotalOriginOffset.z:F1}");
#endif
        }

        private void MoveAllRootTransforms(Vector3 delta)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var roots = scene.GetRootGameObjects();
                foreach (var go in roots)
                {
                    if (go == gameObject) continue;
                    go.transform.position += delta;
                }
            }
        }

        private void MovePhysicsBodies(Vector3 delta)
        {
            var bodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var rb in bodies)
            {
                if (rb.transform.root == rb.transform)
                {
                    rb.position += delta;
                }
            }
        }

        public Vector3 WorldToLocal(Vector3d absoluteWorldPos)
        {
            return (absoluteWorldPos - TotalOriginOffset).ToVector3();
        }

        public Vector3d LocalToWorld(Vector3 localPos)
        {
            return TotalOriginOffset + new Vector3d(localPos.x, localPos.y, localPos.z);
        }

        public void SetFocus(Transform focus)
        {
            focusTransform = focus;
        }
    }

    [Serializable]
    public struct Vector3d
    {
        public double x, y, z;

        public static Vector3d zero => new Vector3d(0, 0, 0);

        public Vector3d(double x, double y, double z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);

        public Vector3 ToVector3() => new Vector3((float)x, (float)y, (float)z);
    }
}
