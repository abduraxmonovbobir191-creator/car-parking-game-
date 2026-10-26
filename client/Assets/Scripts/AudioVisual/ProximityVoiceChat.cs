using System;
using UnityEngine;

namespace NextGenDrive.AudioVisual
{
    [RequireComponent(typeof(AudioSource))]
    public class ProximityVoiceChat : MonoBehaviour
    {
        [Header("3D Spatial Voice Settings")]
        public float maxHearingDistance = 22f;
        public int sampleRate = 16000;
        public KeyCode pushToTalkKey = KeyCode.V;

        public event Action<byte[]> OnVoicePacketCaptured;

        private AudioSource spatialAudioSource;
        private AudioClip micClip;
        private string micDeviceName;
        private bool isRecording = false;
        private int lastSamplePosition = 0;

        private void Awake()
        {
            spatialAudioSource = GetComponent<AudioSource>();
            spatialAudioSource.spatialBlend = 1.0f; // 100% 3D Spatial Sound
            spatialAudioSource.rolloffMode = AudioRolloffMode.Linear;
            spatialAudioSource.minDistance = 2.0f;
            spatialAudioSource.maxDistance = maxHearingDistance;
        }

        private void Start()
        {
            if (Microphone.devices.Length > 0)
            {
                micDeviceName = Microphone.devices[0];
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(pushToTalkKey)) StartTalking();
            if (Input.GetKeyUp(pushToTalkKey)) StopTalking();

            if (isRecording) CaptureMicStream();
        }

        public void StartTalking()
        {
            if (string.IsNullOrEmpty(micDeviceName) || isRecording) return;
            micClip = Microphone.Start(micDeviceName, true, 1, sampleRate);
            lastSamplePosition = 0;
            isRecording = true;
        }

        public void StopTalking()
        {
            if (!isRecording) return;
            Microphone.End(micDeviceName);
            isRecording = false;
        }

        private void CaptureMicStream()
        {
            int currentPos = Microphone.GetPosition(micDeviceName);
            if (currentPos < lastSamplePosition) lastSamplePosition = 0;

            int sampleCount = currentPos - lastSamplePosition;
            if (sampleCount >= 1600) // Send every 100ms chunk
            {
                float[] samples = new float[sampleCount];
                micClip.GetData(samples, lastSamplePosition);
                lastSamplePosition = currentPos;

                byte[] pcmBytes = ConvertFloatToPCM16(samples);
                OnVoicePacketCaptured?.Invoke(pcmBytes);
            }
        }

        public void PlayReceivedRemoteVoice(byte[] pcmBytes)
        {
            float[] samples = ConvertPCM16ToFloat(pcmBytes);
            AudioClip remoteClip = AudioClip.Create("RemoteVoice", samples.Length, 1, sampleRate, false);
            remoteClip.SetData(samples, 0);
            spatialAudioSource.PlayOneShot(remoteClip);
        }

        private static byte[] ConvertFloatToPCM16(float[] samples)
        {
            byte[] bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
                bytes[i * 2] = (byte)(val & 0xFF);
                bytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            return bytes;
        }

        private static float[] ConvertPCM16ToFloat(byte[] bytes)
        {
            float[] samples = new float[bytes.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                samples[i] = val / 32767f;
            }
            return samples;
        }
    }
}
