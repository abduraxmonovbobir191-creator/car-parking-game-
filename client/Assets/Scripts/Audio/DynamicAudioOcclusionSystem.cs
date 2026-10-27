using UnityEngine;
using CarParking.Audio;
using CarParking.Vehicle;

namespace CarParking.Audio
{
    /// <summary>
    /// Real-time raycast audio occlusion / low-pass when windows closed,
    /// inside tunnels, or blocked by buildings.
    /// </summary>
    public class DynamicAudioOcclusionSystem : MonoBehaviour
    {
        public static DynamicAudioOcclusionSystem Instance { get; private set; }

        [Header("Sources")]
        public AudioSource[] engineSources;
        public AudioSource radioSource;

        [Header("Occlusion")]
        public float checkInterval = 0.15f;
        public float maxCheckDistance = 35f;
        public LayerMask occlusionMask;
        public float closedWindowCutoff = 1800f;
        public float openWindowCutoff = 22000f;
        public float occludedCutoff = 900f;
        public float lerpSpeed = 6f;

        private float _timer;
        private float _currentCutoff = 22000f;
        private bool _windowsOpen = true;
        private AudioLowPassFilter[] _filters;
        private Transform _listener;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Camera.main != null) _listener = Camera.main.transform;

            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle != null)
            {
                if (engineSources == null || engineSources.Length == 0)
                    engineSources = vehicle.GetComponentsInChildren<AudioSource>();

                var radio = vehicle.GetComponent<InGameRadioSystem>();
                if (radio != null && radio.radioSource != null)
                    radioSource = radio.radioSource;
            }

            // Ensure low-pass on sources
            var list = new System.Collections.Generic.List<AudioLowPassFilter>();
            if (engineSources != null)
            {
                foreach (var src in engineSources)
                {
                    if (src == null) continue;
                    var f = src.GetComponent<AudioLowPassFilter>();
                    if (f == null) f = src.gameObject.AddComponent<AudioLowPassFilter>();
                    list.Add(f);
                }
            }
            if (radioSource != null)
            {
                var f = radioSource.GetComponent<AudioLowPassFilter>();
                if (f == null) f = radioSource.gameObject.AddComponent<AudioLowPassFilter>();
                list.Add(f);
            }
            _filters = list.ToArray();
        }

        private void Update()
        {
            // Sync window state from radio system if present
            if (InGameRadioSystem.Instance != null)
                _windowsOpen = InGameRadioSystem.Instance.windowsOpen;

            _timer += Time.deltaTime;
            if (_timer >= checkInterval)
            {
                _timer = 0f;
                UpdateOcclusion();
            }

            // Smooth cutoff
            if (_filters != null)
            {
                foreach (var f in _filters)
                {
                    if (f == null) continue;
                    f.cutoffFrequency = Mathf.Lerp(f.cutoffFrequency, _currentCutoff, Time.deltaTime * lerpSpeed);
                }
            }
        }

        private void UpdateOcclusion()
        {
            float target = _windowsOpen ? openWindowCutoff : closedWindowCutoff;

            // Raycast upward / forward for tunnel or building cover
            if (_listener != null)
            {
                bool blocked = Physics.Raycast(_listener.position, Vector3.up, maxCheckDistance, occlusionMask)
                            || Physics.Raycast(_listener.position, _listener.forward, 8f, occlusionMask);

                if (blocked)
                    target = Mathf.Min(target, occludedCutoff);
            }

            _currentCutoff = target;
        }

        public void SetWindowsOpen(bool open) => _windowsOpen = open;
    }
}
