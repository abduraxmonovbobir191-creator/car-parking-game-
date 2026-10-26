using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;

namespace NextGenDrive.Network
{
    public class MultiplayerNetworkClient : MonoBehaviour
    {
        public string serverUrl = "ws://localhost:8080";
        public string playerId;
        public string username = "ProDriver";

        private ClientWebSocket ws;
        private CancellationTokenSource cts;

        public event Action<string> OnServerEventReceived;

        private async void Start()
        {
            await ConnectToServer();
        }

        public async Task ConnectToServer()
        {
            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();

            try
            {
                await ws.ConnectAsync(new Uri(serverUrl), cts.Token);
                await SendJsonAsync($"{{\"action\":\"AUTH\",\"playerId\":\"{playerId}\",\"username\":\"{username}\"}}");
                _ = ReceiveLoop();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] Connection failed: {e.Message}");
            }
        }

        public async Task SendVehicleTransform(string vin, Vector3 pos, Quaternion rot, float rpm, bool hoodOpen)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;

            string payload = $"{{\"action\":\"SYNC_TRANSFORM\",\"vin\":\"{vin}\"," +
                             $"\"px\":{pos.x:F2},\"py\":{pos.y:F2},\"pz\":{pos.z:F2}," +
                             $"\"ry\":{rot.eulerAngles.y:F1},\"rpm\":{Mathf.RoundToInt(rpm)},\"hood\":{(hoodOpen ? "true" : "false")}}}";
            await SendJsonAsync(payload);
        }

        public async Task PlaceAuctionBid(string vin, int bidAmount)
        {
            string payload = $"{{\"action\":\"PLACE_BID\",\"vin\":\"{vin}\",\"bidAmount\":{bidAmount}}}";
            await SendJsonAsync(payload);
        }

        private async Task SendJsonAsync(string json)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[4096];
            while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnServerEventReceived?.Invoke(message);
                }
            }
        }

        private void OnDestroy()
        {
            cts?.Cancel();
            ws?.Dispose();
        }
    }
}
