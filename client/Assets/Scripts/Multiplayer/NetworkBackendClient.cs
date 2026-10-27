using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using CarParking.World;
using CarParking.Vehicle;

namespace CarParking.Multiplayer
{
    public class NetworkBackendClient : MonoBehaviour
    {
        public static NetworkBackendClient Instance { get; private set; }

        [Header("Server")]
        public string serverUrl = "ws://127.0.0.1:8765";
        public string playerName = "Driver";
        public string roomId = "world_1";

        [Header("Sync")]
        public float stateSendRate = 15f;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private float _sendTimer;
        private bool _connected;
        private string _netId;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private async void Start() { await ConnectAsync(); }

        private void Update()
        {
            if (!_connected) return;
            _sendTimer += Time.deltaTime;
            if (_sendTimer >= 1f / stateSendRate) { _sendTimer = 0f; SendLocalState(); }
        }

        public async Task ConnectAsync()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);
                _connected = true;
                Debug.Log("[NetworkBackend] Connected to " + serverUrl);
                _ = ReceiveLoop();
                await SendJson(new Dictionary<string, object> { { "type", "join" }, { "name", playerName }, { "room", roomId } });
            }
            catch (Exception e) { Debug.LogWarning("[NetworkBackend] Connection failed: " + e.Message); _connected = false; }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[16384];
            while (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    HandleMessage(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                catch { break; }
            }
            _connected = false;
        }

        private void HandleMessage(string json)
        {
            if (json.Contains("\"type\":\"joined\""))
            {
                _netId = ExtractString(json, "netId");
                Debug.Log("[NetworkBackend] Joined as " + _netId);
            }
            else if (json.Contains("\"type\":\"voice\""))
            {
                string netId = ExtractString(json, "netId");
                string pcm = ExtractString(json, "pcm");
                if (netId != null && pcm != null && VoiceChatManager.Instance != null)
                    VoiceChatManager.Instance.ReceiveVoicePacket(netId, pcm, Vector3.zero);
            }
            else if (json.Contains("\"type\":\"race_start\""))
                Debug.Log("[NetworkBackend] Race start received");
        }

        private void SendLocalState()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            if (vehicle == null) return;
            var t = vehicle.transform;
            var drivetrain = vehicle.GetComponent<DrivetrainSystem>();
            Vector3d worldPos = FloatingOriginSystem.Instance != null ? FloatingOriginSystem.Instance.LocalToWorld(t.position) : new Vector3d(t.position.x, t.position.y, t.position.z);
            var offset = FloatingOriginSystem.Instance != null ? FloatingOriginSystem.Instance.TotalOriginOffset : Vector3d.zero;
            _ = SendJson(new Dictionary<string, object> {
                { "type", "state" },
                { "pos", new Dictionary<string, double> { { "x", worldPos.x }, { "y", worldPos.y }, { "z", worldPos.z } } },
                { "rot", new Dictionary<string, float> { { "x", t.rotation.x }, { "y", t.rotation.y }, { "z", t.rotation.z }, { "w", t.rotation.w } } },
                { "originOffset", new Dictionary<string, double> { { "x", offset.x }, { "y", offset.y }, { "z", offset.z } } },
                { "rpm", drivetrain != null ? drivetrain.currentRpm : 0f },
                { "gear", drivetrain != null ? drivetrain.currentGear : 1 },
                { "steer", Input.GetAxis("Horizontal") }
            });
        }

        public async Task SendVoicePacket(string base64Pcm)
        {
            await SendJson(new Dictionary<string, object> { { "type", "voice" }, { "pcm", base64Pcm } });
        }

        public async Task CreateMarketListing(string vehicleId, float price, Dictionary<string, object> specs)
        {
            await SendJson(new Dictionary<string, object> { { "type", "market_create" }, { "vehicleId", vehicleId }, { "price", price }, { "specs", specs } });
        }

        public async Task BuyMarketListing(string listingId)
        {
            await SendJson(new Dictionary<string, object> { { "type", "market_buy" }, { "listingId", listingId } });
        }

        public async Task ChallengeRace(string opponentNetId, string raceType)
        {
            await SendJson(new Dictionary<string, object> { { "type", "race_challenge" }, { "opponentNetId", opponentNetId }, { "raceType", raceType } });
        }

        private async Task SendJson(Dictionary<string, object> data)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            byte[] bytes = Encoding.UTF8.GetBytes(SimpleJsonSerialize(data));
            try { await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token); }
            catch (Exception e) { Debug.LogWarning("[NetworkBackend] Send failed: " + e.Message); }
        }

        private string SimpleJsonSerialize(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(","); first = false;
                sb.Append("\"").Append(kv.Key).Append("\":" ).Append(SerializeValue(kv.Value));
            }
            return sb.Append("}").ToString();
        }

        private string SerializeValue(object val)
        {
            if (val == null) return "null";
            if (val is string s) return "\"" + s.Replace("\"", "\\\"") + "\"";
            if (val is bool b) return b ? "true" : "false";
            if (val is int || val is long || val is float || val is double) return Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture);
            if (val is Dictionary<string, object> d) return SimpleJsonSerialize(d);
            if (val is Dictionary<string, double> dd) { var sb = new StringBuilder("{"); bool f = true; foreach (var kv in dd) { if (!f) sb.Append(","); f = false; sb.Append("\"").Append(kv.Key).Append("\":" ).Append(kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); } return sb.Append("}").ToString(); }
            if (val is Dictionary<string, float> df) { var sb = new StringBuilder("{"); bool f = true; foreach (var kv in df) { if (!f) sb.Append(","); f = false; sb.Append("\"").Append(kv.Key).Append("\":" ).Append(kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); } return sb.Append("}").ToString(); }
            return "\"" + val.ToString() + "\"";
        }

        private string ExtractString(string json, string key)
        {
            string pattern = "\"" + key + "\":\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += pattern.Length;
            int end = json.IndexOf('"', idx);
            return end < 0 ? null : json.Substring(idx, end - idx);
        }

        private async void OnDestroy()
        {
            _connected = false;
            if (_cts != null) _cts.Cancel();
            if (_ws != null && _ws.State == WebSocketState.Open)
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
        }
    }
}
