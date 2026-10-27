using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using CarParking.Vehicle;
using CarParking.Core;
using CarParking.AI;

namespace CarParking.UI
{
    /// <summary>
    /// Lightweight embedded HTTP server streaming vehicle telemetry
    /// to a companion mobile web dashboard.
    /// </summary>
    public class CompanionAppBridge : MonoBehaviour
    {
        public static CompanionAppBridge Instance { get; private set; }

        [Header("Server")]
        public int httpPort = 8080;
        public bool startOnAwake = true;

        private HttpListener _listener;
        private Thread _thread;
        private bool _running;
        private string _latestJson = "{}";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (startOnAwake) StartServer();
        }

        private void Update()
        {
            _latestJson = BuildTelemetryJson();
        }

        public void StartServer()
        {
            if (_running) return;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{httpPort}/");
                _listener.Start();
                _running = true;
                _thread = new Thread(ListenLoop) { IsBackground = true };
                _thread.Start();
                Debug.Log($"[Companion] Dashboard at http://127.0.0.1:{httpPort}/");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Companion] Server failed: " + e.Message + " — try running as admin or change port");
            }
        }

        public void StopServer()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        private void ListenLoop()
        {
            while (_running && _listener != null)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath.ToLowerInvariant();
            byte[] buffer;
            string contentType = "application/json";

            if (path == "/" || path == "/index.html")
            {
                contentType = "text/html";
                buffer = Encoding.UTF8.GetBytes(GetDashboardHtml());
            }
            else if (path == "/telemetry" || path == "/api/telemetry")
            {
                buffer = Encoding.UTF8.GetBytes(_latestJson);
            }
            else
            {
                ctx.Response.StatusCode = 404;
                buffer = Encoding.UTF8.GetBytes("{\"error\":\"not found\"}");
            }

            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            try
            {
                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                ctx.Response.OutputStream.Close();
            }
            catch { }
        }

        private string BuildTelemetryJson()
        {
            var vehicle = FindFirstObjectByType<RealisticVehicleController>();
            float speed = 0f, fuel = 0f, rpm = 0f;
            int gear = 0, wanted = 0;
            float x = 0f, z = 0f;
            float health = 1f;

            if (vehicle != null)
            {
                var rb = vehicle.GetComponent<Rigidbody>();
                if (rb != null) speed = rb.linearVelocity.magnitude * 3.6f;
                x = vehicle.transform.position.x;
                z = vehicle.transform.position.z;

                var dt = vehicle.GetComponent<DrivetrainSystem>();
                if (dt != null) { rpm = dt.currentRpm; gear = dt.currentGear; }

                var dmg = vehicle.GetComponent<VehicleDamageAndDeformation>();
                if (dmg != null) health = (dmg.engineHealth + dmg.radiatorHealth) * 0.5f;
            }

            if (WorldServicesAndEconomySystem.Instance != null)
                fuel = WorldServicesAndEconomySystem.Instance.currentFuel;

            if (PolicePursuitAndRadarSystem.Instance != null)
                wanted = PolicePursuitAndRadarSystem.Instance.WantedLevel;

            return "{" +
                $"\"speed\":{speed:F1}," +
                $"\"rpm\":{rpm:F0}," +
                $"\"gear\":{gear}," +
                $"\"fuel\":{fuel:F2}," +
                $"\"health\":{health:F2}," +
                $"\"wanted\":{wanted}," +
                $"\"x\":{x:F1}," +
                $"\"z\":{z:F1}," +
                $"\"time\":{Time.time:F1}" +
                "}";
        }

        private string GetDashboardHtml()
        {
            return @"<!DOCTYPE html><html><head><meta name=viewport content=width=device-width,initial-scale=1>
<title>Ultra Car Companion</title>
<style>
body{font-family:system-ui;background:#0d1117;color:#e6edf3;margin:0;padding:16px}
.card{background:#161b22;border-radius:12px;padding:16px;margin-bottom:12px}
.h{font-size:14px;opacity:.7}.v{font-size:28px;font-weight:700}
grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}
</style></head><body>
<h2>Ultra Car Dashboard</h2>
<div class=grid>
<div class=card><div class=h>Speed</div><div class=v id=speed>—</div></div>
<div class=card><div class=h>RPM</div><div class=v id=rpm>—</div></div>
<div class=card><div class=h>Gear</div><div class=v id=gear>—</div></div>
<div class=card><div class=h>Fuel</div><div class=v id=fuel>—</div></div>
<div class=card><div class=h>Health</div><div class=v id=health>—</div></div>
<div class=card><div class=h>Wanted</div><div class=v id=wanted>—</div></div>
</div>
<div class=card><div class=h>GPS</div><div class=v id=gps style=font-size:16px>—</div></div>
<script>
async function tick(){
 try{
  const r=await fetch('/telemetry');const j=await r.json();
  document.getElementById('speed').textContent=j.speed.toFixed(0)+' km/h';
  document.getElementById('rpm').textContent=Math.round(j.rpm);
  document.getElementById('gear').textContent=j.gear;
  document.getElementById('fuel').textContent=(j.fuel*100).toFixed(0)+'%';
  document.getElementById('health').textContent=(j.health*100).toFixed(0)+'%';
  document.getElementById('wanted').textContent='★'.repeat(j.wanted)||'0';
  document.getElementById('gps').textContent=j.x.toFixed(0)+', '+j.z.toFixed(0);
 }catch(e){}
 setTimeout(tick,500);
}
tick();
</script></body></html>";
        }

        private void OnDestroy() => StopServer();
    }
}
