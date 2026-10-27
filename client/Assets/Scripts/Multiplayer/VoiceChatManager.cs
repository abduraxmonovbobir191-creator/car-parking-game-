using System;
using System.Collections.Generic;
using UnityEngine;
using CarParking.Multiplayer;

namespace CarParking.Multiplayer
{
    /// <summary>
    /// Real-time multiplayer voice chat (walkie-talkie).
    /// Push-to-talk (V), microphone capture, spatial attenuation, server relay.
    /// </summary>
    public class VoiceChatManager : MonoBehaviour
    {
        public static VoiceChatManager Instance { get; private set; }

        [Header("Settings")]
        public KeyCode pushToTalkKey = KeyCode.V;
        public int sampleRate = 16000;
        public int chunkSamples = 640;
        public float maxVoiceDistance = 120f;
        public float minVolume = 0.05f;

        [Header("UI")]
        public GameObject micIndicator;

        private AudioClip _micClip;
        private string _micDevice;
        private bool _isTalking;
        private float[] _sampleBuffer;
        private Dictionary<string, AudioSource> _remoteSources = new();
        private Transform _listener;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _sampleBuffer = new float[chunkSamples];
        }

        private void Start()
        {
            if (Microphone.devices.Length > 0)
            {
                _micDevice = Microphone.devices[0];
                _micClip = Microphone.Start(_micDevice, true, 1, sampleRate);
            }

            var cam = Camera.main;
            if (cam != null) _listener = cam.transform;
        }

        private void Update()
        {
            bool talk = Input.GetKey(pushToTalkKey);
            if (talk != _isTalking)
            {
                _isTalking = talk;
                if (micIndicator != null) micIndicator.SetActive(_isTalking);
            }

            if (_isTalking && _micClip != null)
                CaptureAndSend();

            UpdateSpatialVolumes();
        }

        private void CaptureAndSend()
        {
            int pos = Microphone.GetPosition(_micDevice);
            if (pos < chunkSamples) return;

            _micClip.GetData(_sampleBuffer, pos - chunkSamples);

            // Simple peak normalization
            float peak = 0f;
            for (int i = 0; i < _sampleBuffer.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(_sampleBuffer[i]));
            if (peak < 0.01f) return;

            // Convert to 16-bit PCM bytes
            byte[] pcm = new byte[_sampleBuffer.Length * 2];
            for (int i = 0; i < _sampleBuffer.Length; i++)
            {
                short s = (short)(Mathf.Clamp(_sampleBuffer[i], -1f, 1f) * 32000f);
                pcm[i * 2] = (byte)(s & 0xff);
                pcm[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }

            string b64 = Convert.ToBase64String(pcm);

            if (NetworkBackendClient.Instance != null)
            {
                // Send via backend (server relays to room)
                _ = NetworkBackendClient.Instance.SendVoicePacket(b64);
            }
        }

        public void ReceiveVoicePacket(string netId, string base64Pcm, Vector3 speakerWorldPos)
        {
            if (string.IsNullOrEmpty(base64Pcm)) return;

            byte[] pcm = Convert.FromBase64String(base64Pcm);
            float[] samples = new float[pcm.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                samples[i] = s / 32000f;
            }

            if (!_remoteSources.TryGetValue(netId, out AudioSource src))
            {
                var go = new GameObject("Voice_" + netId);
                go.transform.SetParent(transform);
                src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = maxVoiceDistance;
                src.minDistance = 3f;
                src.playOnAwake = false;
                _remoteSources[netId] = src;
            }

            src.transform.position = speakerWorldPos;

            AudioClip clip = AudioClip.Create("RemoteVoice", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            src.clip = clip;
            src.Play();
        }

        private void UpdateSpatialVolumes()
        {
            if (_listener == null) return;

            foreach (var kvp in _remoteSources)
            {
                if (kvp.Value == null) continue;
                float dist = Vector3.Distance(_listener.position, kvp.Value.transform.position);
                float vol = dist >= maxVoiceDistance ? 0f : Mathf.Lerp(1f, minVolume, dist / maxVoiceDistance);
                kvp.Value.volume = vol;
            }
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(_micDevice))
                Microphone.End(_micDevice);
        }
    }
}
