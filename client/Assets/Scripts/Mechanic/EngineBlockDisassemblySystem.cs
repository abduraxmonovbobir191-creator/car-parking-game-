using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarParking.Mechanic
{
    /// <summary>
    /// MODULE 36 — Bolt-by-Bolt Engine Block Disassembly System
    /// Car Mechanic Simulator style interactive inspection and part tracking.
    /// Expanded with torque, oil change, repair cost, and reset utilities.
    /// </summary>
    public class EngineBlockDisassemblySystem : MonoBehaviour
    {
        public static EngineBlockDisassemblySystem Instance { get; private set; }

        [Serializable]
        public class EnginePart
        {
            public string id;
            public string displayName;
            public float wearPercent;
            public float temperatureC;
            public float oilStarvationDamage;
            public float knockDamage;
            public float boltTorqueNm;
            public bool isRemoved;
            public bool isDamaged;
            public Vector3 localPosition;
            public string parentPartId;
        }

        public List<EnginePart> parts = new();
        public bool inspectionMode;
        public float globalOilLevel = 1f;
        public float globalCoolantLevel = 1f;

        private Dictionary<string, EnginePart> _lookup = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildDefaultParts();
        }

        private void BuildDefaultParts()
        {
            AddPart("cylinder_head", "Cylinder Head", 0.02f, 85f, 0f, 0f, 90f, Vector3.up * 0.35f, "");
            AddPart("head_gasket", "Head Gasket", 0.05f, 90f, 0.01f, 0f, 0f, Vector3.up * 0.32f, "cylinder_head");
            AddPart("camshaft_intake", "Intake Camshaft", 0.03f, 88f, 0f, 0f, 25f, new Vector3(0.1f, 0.38f, 0f), "cylinder_head");
            AddPart("camshaft_exhaust", "Exhaust Camshaft", 0.03f, 92f, 0f, 0f, 25f, new Vector3(-0.1f, 0.38f, 0f), "cylinder_head");
            AddPart("timing_chain", "Timing Chain", 0.08f, 75f, 0.02f, 0f, 0f, Vector3.forward * 0.15f, "");
            AddPart("intake_manifold", "Intake Manifold", 0.01f, 45f, 0f, 0f, 22f, new Vector3(0f, 0.25f, 0.3f), "");
            AddPart("throttle_body", "Throttle Body", 0.04f, 40f, 0f, 0f, 10f, new Vector3(0f, 0.28f, 0.45f), "intake_manifold");
            AddPart("fuel_rail", "Fuel Rail", 0.02f, 50f, 0f, 0f, 12f, new Vector3(0f, 0.22f, 0.1f), "");
            for (int i = 0; i < 6; i++)
            {
                AddPart($"injector_{i}", $"Fuel Injector {i+1}", 0.06f, 55f, 0f, 0f, 8f, new Vector3(-0.2f + i*0.08f, 0.2f, 0.05f), "fuel_rail");
                AddPart($"spark_{i}", $"Spark Plug {i+1}", 0.12f, 120f, 0f, 0.01f, 20f, new Vector3(-0.2f + i*0.08f, 0.3f, 0f), "cylinder_head");
                AddPart($"coil_{i}", $"Ignition Coil {i+1}", 0.04f, 70f, 0f, 0f, 6f, new Vector3(-0.2f + i*0.08f, 0.42f, 0f), "cylinder_head");
            }
            AddPart("oil_pan", "Oil Pan", 0.01f, 95f, 0f, 0f, 15f, Vector3.down * 0.2f, "");
            AddPart("oil_filter", "Oil Filter", 0.15f, 90f, 0.05f, 0f, 25f, new Vector3(0.3f, -0.1f, 0f), "");
            AddPart("water_pump", "Water Pump", 0.07f, 80f, 0f, 0f, 18f, new Vector3(-0.25f, 0.05f, 0.1f), "");
            AddPart("crankshaft", "Crankshaft", 0.02f, 100f, 0.01f, 0.005f, 0f, Vector3.zero, "");
            for (int i = 0; i < 6; i++)
            {
                AddPart($"piston_{i}", $"Piston {i+1}", 0.04f, 110f, 0f, 0.002f, 0f, new Vector3(-0.15f + i*0.06f, 0.1f, 0f), "crankshaft");
                AddPart($"rod_{i}", $"Connecting Rod {i+1}", 0.03f, 105f, 0f, 0f, 45f, new Vector3(-0.15f + i*0.06f, 0.05f, 0f), "crankshaft");
            }
            RebuildLookup();
        }

        private void AddPart(string id, string name, float wear, float temp, float oilDmg, float knock, float torque, Vector3 pos, string parent)
        {
            parts.Add(new EnginePart
            {
                id = id, displayName = name, wearPercent = wear, temperatureC = temp,
                oilStarvationDamage = oilDmg, knockDamage = knock, boltTorqueNm = torque,
                localPosition = pos, parentPartId = parent
            });
        }

        private void RebuildLookup()
        {
            _lookup.Clear();
            foreach (var p in parts) _lookup[p.id] = p;
        }

        public EnginePart GetPart(string id) => _lookup.TryGetValue(id, out var p) ? p : null;

        public bool RemovePart(string id)
        {
            var p = GetPart(id);
            if (p == null || p.isRemoved) return false;
            foreach (var c in parts)
                if (c.parentPartId == id && !c.isRemoved) return false;
            p.isRemoved = true;
            Debug.Log($"[Mechanic] Removed {p.displayName}");
            return true;
        }

        public void UpdateWear(float dt, float rpm, float load, float oilPressure)
        {
            float oilFactor = oilPressure < 0.3f ? 3f : 1f;
            float knockFactor = load > 0.9f && rpm > 6000f ? 1.5f : 1f;
            foreach (var p in parts)
            {
                if (p.isRemoved) continue;
                p.wearPercent = Mathf.Clamp01(p.wearPercent + 0.00002f * rpm * 0.001f * oilFactor * dt);
                p.temperatureC = Mathf.Lerp(p.temperatureC, 80f + load * 40f, dt * 0.1f);
                if (oilPressure < 0.25f)
                    p.oilStarvationDamage = Mathf.Clamp01(p.oilStarvationDamage + 0.001f * dt);
                if (knockFactor > 1f)
                    p.knockDamage = Mathf.Clamp01(p.knockDamage + 0.0005f * dt);
                p.isDamaged = p.wearPercent > 0.7f || p.oilStarvationDamage > 0.4f || p.knockDamage > 0.3f;
            }
        }

        public float GetOverallHealth()
        {
            float sum = 0f;
            int count = 0;
            foreach (var p in parts)
            {
                if (p.isRemoved) continue;
                sum += 1f - p.wearPercent;
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        public string GetDiagnosticReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ENGINE DIAGNOSTIC ===");
            sb.AppendLine($"Overall Health: {GetOverallHealth()*100f:F1}%");
            sb.AppendLine($"Oil Level: {globalOilLevel*100f:F0}%  Coolant: {globalCoolantLevel*100f:F0}%");
            foreach (var p in parts)
            {
                if (p.isDamaged || p.wearPercent > 0.3f)
                    sb.AppendLine($"! {p.displayName}: wear {p.wearPercent*100f:F0}% temp {p.temperatureC:F0}C oilDmg {p.oilStarvationDamage*100f:F0}%");
            }
            return sb.ToString();
        }

        public void TorqueBolt(string partId, float appliedNm)
        {
            var p = GetPart(partId);
            if (p == null || p.isRemoved) return;
            float diff = Mathf.Abs(appliedNm - p.boltTorqueNm);
            if (diff > p.boltTorqueNm * 0.25f)
            {
                p.wearPercent = Mathf.Clamp01(p.wearPercent + 0.02f);
                Debug.Log($"[Mechanic] Over/under torqued {p.displayName}");
            }
        }

        public void ApplyOilChange()
        {
            globalOilLevel = 1f;
            foreach (var p in parts)
                p.oilStarvationDamage = Mathf.Max(0f, p.oilStarvationDamage - 0.3f);
            Debug.Log("[Mechanic] Oil changed");
        }

        public void CoolDown(float amount)
        {
            foreach (var p in parts)
                p.temperatureC = Mathf.Max(20f, p.temperatureC - amount);
        }

        public List<EnginePart> GetDamagedParts()
        {
            var list = new List<EnginePart>();
            foreach (var p in parts)
                if (p.isDamaged || p.wearPercent > 0.4f) list.Add(p);
            return list;
        }

        public float EstimateRepairCost()
        {
            float cost = 0f;
            foreach (var p in GetDamagedParts())
                cost += 150f + p.wearPercent * 800f;
            return cost;
        }

        public void ResetEngine()
        {
            parts.Clear();
            BuildDefaultParts();
            globalOilLevel = 1f;
            globalCoolantLevel = 1f;
            Debug.Log("[Mechanic] Engine reset to factory");
        }
    }
}
