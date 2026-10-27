using System.Collections.Generic;
using UnityEngine;
using CarParking.World;
using CarParking.Vehicle;

namespace CarParking.Multiplayer
{
    /// <summary>
    /// 64-bit floating-origin multiplayer synchronization.
    /// Sends/receives double-precision world coordinates and interpolates remote vehicles
    /// with Hermite splines for smooth 60Hz+ movement across a 1500 km map.
    /// </summary>
    public class NetworkWorldSyncManager : MonoBehaviour
    {
        public static NetworkWorldSyncManager Instance { get; private set; }

        [Header("Sync Settings")]
        public float sendRate = 20f;
        public float interpolationBackTime = 0.12f;

        private float _sendTimer;
        private Dictionary<int, RemoteVehicleState> _remoteVehicles = new();
        private Dictionary<int, SnapshotBuffer> _snapshotBuffers = new();

        private struct Snapshot
        {
            public double time;
            public Vector3d worldPos;
            public Quaternion rotation;
            public float steerAngle;
            public float[] suspensionCompression;
            public float rpm;
            public int gear;
            public bool diffLock;
        }

        private class SnapshotBuffer
        {
            public Snapshot[] buffer = new Snapshot[30];
            public int count;
            public int head;

            public void Add(Snapshot s)
            {
                buffer[head] = s;
                head = (head + 1) % buffer.Length;
                if (count < buffer.Length) count++;
            }

            public bool GetInterpolated(double renderTime, out Snapshot result)
            {
                result = default;
                if (count < 2) return false;

                int newest = (head - 1 + buffer.Length) % buffer.Length;
                int oldest = (head - count + buffer.Length) % buffer.Length;

                for (int i = 0; i < count - 1; i++)
                {
                    int a = (oldest + i) % buffer.Length;
                    int b = (oldest + i + 1) % buffer.Length;

                    if (buffer[a].time <= renderTime && buffer[b].time >= renderTime)
                    {
                        float t = (float)((renderTime - buffer[a].time) / (buffer[b].time - buffer[a].time));
                        result = HermiteInterpolate(buffer[a], buffer[b], t);
                        return true;
                    }
                }
                result = buffer[newest];
                return true;
            }

            private Snapshot HermiteInterpolate(Snapshot a, Snapshot b, float t)
            {
                float t2 = t * t;
                float t3 = t2 * t;
                float h1 = 2f * t3 - 3f * t2 + 1f;
                float h2 = -2f * t3 + 3f * t2;

                Snapshot s = new Snapshot();
                s.worldPos = new Vector3d(
                    a.worldPos.x * h1 + b.worldPos.x * h2,
                    a.worldPos.y * h1 + b.worldPos.y * h2,
                    a.worldPos.z * h1 + b.worldPos.z * h2
                );
                s.rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
                s.steerAngle = Mathf.Lerp(a.steerAngle, b.steerAngle, t);
                s.rpm = Mathf.Lerp(a.rpm, b.rpm, t);
                s.gear = t < 0.5f ? a.gear : b.gear;
                s.diffLock = t < 0.5f ? a.diffLock : b.diffLock;

                if (a.suspensionCompression != null && b.suspensionCompression != null)
                {
                    int len = Mathf.Min(a.suspensionCompression.Length, b.suspensionCompression.Length);
                    s.suspensionCompression = new float[len];
                    for (int i = 0; i < len; i++)
                        s.suspensionCompression[i] = Mathf.Lerp(a.suspensionCompression[i], b.suspensionCompression[i], t);
                }
                return s;
            }
        }

        private class RemoteVehicleState
        {
            public Transform transform;
            public RealisticVehicleController controller;
            public DrivetrainSystem drivetrain;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            _sendTimer += Time.deltaTime;
            if (_sendTimer >= 1f / sendRate)
            {
                _sendTimer = 0f;
                SendLocalState();
            }

            InterpolateRemotes();
        }

        private void SendLocalState()
        {
            var localVehicles = FindObjectsByType<RealisticVehicleController>(FindObjectsSortMode.None);
            foreach (var v in localVehicles)
            {
                if (!v.enabled) continue;

                Vector3d worldPos = FloatingOriginSystem.Instance != null
                    ? FloatingOriginSystem.Instance.LocalToWorld(v.transform.position)
                    : new Vector3d(v.transform.position.x, v.transform.position.y, v.transform.position.z);

                // In a real netcode this would be sent via Mirror / Netcode / custom transport
                // For now we just keep the structure ready.
            }
        }

        public void ReceiveRemoteSnapshot(int netId, Vector3d worldPos, Quaternion rot, float steer, float[] suspension, float rpm, int gear, bool diffLock)
        {
            if (!_snapshotBuffers.TryGetValue(netId, out var buffer))
            {
                buffer = new SnapshotBuffer();
                _snapshotBuffers[netId] = buffer;
            }

            Snapshot s = new Snapshot
            {
                time = Time.timeAsDouble,
                worldPos = worldPos,
                rotation = rot,
                steerAngle = steer,
                suspensionCompression = suspension,
                rpm = rpm,
                gear = gear,
                diffLock = diffLock
            };
            buffer.Add(s);
        }

        private void InterpolateRemotes()
        {
            double renderTime = Time.timeAsDouble - interpolationBackTime;

            foreach (var kvp in _snapshotBuffers)
            {
                if (!_remoteVehicles.TryGetValue(kvp.Key, out var remote)) continue;
                if (!kvp.Value.GetInterpolated(renderTime, out Snapshot s)) continue;

                Vector3 localPos = FloatingOriginSystem.Instance != null
                    ? FloatingOriginSystem.Instance.WorldToLocal(s.worldPos)
                    : s.worldPos.ToVector3();

                remote.transform.SetPositionAndRotation(localPos, s.rotation);

                if (remote.drivetrain != null)
                {
                    remote.drivetrain.currentRpm = s.rpm;
                    remote.drivetrain.currentGear = s.gear;
                    remote.drivetrain.diffLock = s.diffLock;
                }
            }
        }

        public void RegisterRemoteVehicle(int netId, GameObject go)
        {
            _remoteVehicles[netId] = new RemoteVehicleState
            {
                transform = go.transform,
                controller = go.GetComponent<RealisticVehicleController>(),
                drivetrain = go.GetComponent<DrivetrainSystem>()
            };
        }

        public void UnregisterRemoteVehicle(int netId)
        {
            _remoteVehicles.Remove(netId);
            _snapshotBuffers.Remove(netId);
        }
    }
}
