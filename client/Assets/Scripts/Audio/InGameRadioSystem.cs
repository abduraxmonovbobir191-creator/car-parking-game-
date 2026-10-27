using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CarParking.Vehicle;

namespace CarParking.Audio
{
    /// <summary>
    /// In-game radio & custom music player with spatial cockpit audio
    /// and low-pass filter when windows are closed.
    /// </summary>
    public class InGameRadioSystem : MonoBehaviour
    {
        public static InGameRadioSystem Instance { get; private set; }

        [System.Serializable]
        public class RadioStation
        {
            public string name;
            public AudioClip[] clips;
            public bool isProcedural;
        }

        [Header("Stations")]
        public RadioStation[] stations;
        public int currentStationIndex;

        [Header("Audio")]
        public AudioSource radioSource;
        public AudioLowPassFilter lowPassFilter;
        public float openWindowCutoff = 22000f;
        public float closedWindowCutoff = 2500f;
        public bool windowsOpen = true;

        [Header("Custom Music")]
        public string customMusicFolder = "Music";

        private List<AudioClip> _customClips = new();
        private int _customIndex;
        private bool _playingCustom;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (radioSource == null)
            {
                radioSource = gameObject.AddComponent<AudioSource>();
                radioSource.spatialBlend = 0.65f;
                radioSource.loop = false;
                radioSource.playOnAwake = false;
            }

            if (lowPassFilter == null)
                lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
        }

        private void Start()
        {
            LoadCustomMusic();
            if (stations != null && stations.Length > 0)
                PlayStation(0);
        }

        private void Update()
        {
            // Hotkeys
            if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftShift))
                NextStation();

            if (Input.GetKeyDown(KeyCode.T) && Input.GetKey(KeyCode.LeftShift))
                ToggleWindows();

            // Auto-advance
            if (radioSource != null && !radioSource.isPlaying && radioSource.clip != null)
            {
                if (_playingCustom)
                    PlayNextCustom();
                else
                    PlayNextClipInStation();
            }

            UpdateLowPass();
        }

        private void UpdateLowPass()
        {
            if (lowPassFilter == null) return;
            float target = windowsOpen ? openWindowCutoff : closedWindowCutoff;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, target, Time.deltaTime * 3f);
        }

        public void PlayStation(int index)
        {
            if (stations == null || stations.Length == 0) return;
            currentStationIndex = Mathf.Clamp(index, 0, stations.Length - 1);
            _playingCustom = false;

            var station = stations[currentStationIndex];
            if (station.clips != null && station.clips.Length > 0)
            {
                radioSource.clip = station.clips[Random.Range(0, station.clips.Length)];
                radioSource.Play();
                Debug.Log("[Radio] Station: " + station.name);
            }
        }

        public void NextStation()
        {
            if (stations == null || stations.Length == 0) return;
            PlayStation((currentStationIndex + 1) % stations.Length);
        }

        private void PlayNextClipInStation()
        {
            if (stations == null || stations.Length == 0) return;
            var station = stations[currentStationIndex];
            if (station.clips == null || station.clips.Length == 0) return;
            radioSource.clip = station.clips[Random.Range(0, station.clips.Length)];
            radioSource.Play();
        }

        public void ToggleWindows()
        {
            windowsOpen = !windowsOpen;
            Debug.Log(windowsOpen ? "[Radio] Windows OPEN" : "[Radio] Windows CLOSED");
        }

        private void LoadCustomMusic()
        {
            string folder = Path.Combine(Application.persistentDataPath, customMusicFolder);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                return;
            }

            // Note: loading arbitrary audio at runtime requires UnityWebRequest or pre-imported clips.
            // Placeholder: users drop AudioClips into the stations array or StreamingAssets for builds.
            Debug.Log("[Radio] Custom music folder: " + folder);
        }

        public void PlayCustomPlaylist()
        {
            if (_customClips.Count == 0) return;
            _playingCustom = true;
            _customIndex = 0;
            radioSource.clip = _customClips[0];
            radioSource.Play();
        }

        private void PlayNextCustom()
        {
            if (_customClips.Count == 0) return;
            _customIndex = (_customIndex + 1) % _customClips.Count;
            radioSource.clip = _customClips[_customIndex];
            radioSource.Play();
        }

        public void SetVolume(float vol)
        {
            if (radioSource != null)
                radioSource.volume = Mathf.Clamp01(vol);
        }
    }
}
