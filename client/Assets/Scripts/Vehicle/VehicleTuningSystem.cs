using UnityEngine;

namespace CarParking.Vehicle
{
    public enum EngineType { I4, I6_2JZ, V8_Biturbo, V12 }
    public enum PaintType { Metallic, Matte, Chrome, CarbonFiber, Chameleon }

    /// <summary>
    /// Full pro tuning system: suspension geometry, engine swaps, ECU, paint and visual customization.
    /// </summary>
    public class VehicleTuningSystem : MonoBehaviour
    {
        [Header("Suspension Geometry")]
        [Range(-8f, 4f)] public float frontCamber = -1.5f;
        [Range(-8f, 4f)] public float rearCamber = -1.2f;
        [Range(0f, 8f)] public float frontCaster = 4.5f;
        [Range(-1f, 1f)] public float frontToe = 0.05f;
        [Range(-1f, 1f)] public float rearToe = 0.1f;
        [Range(0f, 0.05f)] public float wheelOffset = 0.02f;

        [Header("Air Suspension Heights (metres)")]
        public float[] airHeightPresets = { 0.08f, 0.16f, 0.28f, 0.38f };
        public int currentAirPreset = 1;

        [Header("Engine & Performance")]
        public EngineType currentEngine = EngineType.V8_Biturbo;
        public int ecuStage = 1;               // 1-3
        public float turboBoostPSI = 12f;
        public float[] customGearRatios = { 3.4f, 2.1f, 1.5f, 1.15f, 0.9f, 0.75f };

        [Header("Visual")]
        public PaintType paintType = PaintType.Metallic;
        public Color primaryColor = Color.red;
        public Color secondaryColor = Color.black;
        [Range(0f, 1f)] public float windowTint = 0.35f;
        public Material bodyMaterial;
        public Material windowMaterial;

        private RealisticVehicleController _vehicle;
        private DrivetrainSystem _drivetrain;

        private void Awake()
        {
            _vehicle = GetComponent<RealisticVehicleController>();
            _drivetrain = GetComponent<DrivetrainSystem>();
            ApplyAllTuning();
        }

        public void ApplyAllTuning()
        {
            ApplySuspensionGeometry();
            ApplyEngineAndECU();
            ApplyVisuals();
        }

        public void ApplySuspensionGeometry()
        {
            if (_vehicle == null || _vehicle.wheels == null) return;

            for (int i = 0; i < _vehicle.wheels.Length; i++)
            {
                var w = _vehicle.wheels[i];
                if (w.wheelTransform == null) continue;

                bool isFront = i < 2;
                float camber = isFront ? frontCamber : rearCamber;
                float toe = isFront ? frontToe : rearToe;

                Vector3 euler = w.wheelTransform.localEulerAngles;
                euler.z = camber;
                euler.y = toe * 10f;
                w.wheelTransform.localEulerAngles = euler;

                // Simple offset
                Vector3 pos = w.wheelTransform.localPosition;
                pos.x += (pos.x > 0 ? wheelOffset : -wheelOffset);
                w.wheelTransform.localPosition = pos;
            }

            // Air suspension height
            if (currentAirPreset >= 0 && currentAirPreset < airHeightPresets.Length)
            {
                _vehicle.suspensionRestLength = airHeightPresets[currentAirPreset] + 0.2f;
            }
        }

        public void ApplyEngineAndECU()
        {
            if (_drivetrain == null) return;

            switch (currentEngine)
            {
                case EngineType.I4:
                    _drivetrain.maxTorque = 280f;
                    _drivetrain.maxRpm = 6800f;
                    break;
                case EngineType.I6_2JZ:
                    _drivetrain.maxTorque = 420f;
                    _drivetrain.maxRpm = 7500f;
                    break;
                case EngineType.V8_Biturbo:
                    _drivetrain.maxTorque = 680f;
                    _drivetrain.maxRpm = 7200f;
                    break;
                case EngineType.V12:
                    _drivetrain.maxTorque = 820f;
                    _drivetrain.maxRpm = 8500f;
                    break;
            }

            // ECU stage multiplier
            float stageMult = 1f + (ecuStage - 1) * 0.12f;
            _drivetrain.maxTorque *= stageMult;

            // Turbo boost
            _drivetrain.turboMaxBoost = 1f + (turboBoostPSI / 14.7f) * 0.6f;

            // Custom gears
            if (customGearRatios != null && customGearRatios.Length >= 5)
                _drivetrain.gearRatios = customGearRatios;
        }

        public void ApplyVisuals()
        {
            if (bodyMaterial != null)
            {
                bodyMaterial.SetColor("_BaseColor", primaryColor);

                switch (paintType)
                {
                    case PaintType.Metallic:
                        bodyMaterial.SetFloat("_Metallic", 0.85f);
                        bodyMaterial.SetFloat("_Smoothness", 0.9f);
                        break;
                    case PaintType.Matte:
                        bodyMaterial.SetFloat("_Metallic", 0.1f);
                        bodyMaterial.SetFloat("_Smoothness", 0.25f);
                        break;
                    case PaintType.Chrome:
                        bodyMaterial.SetFloat("_Metallic", 1f);
                        bodyMaterial.SetFloat("_Smoothness", 1f);
                        break;
                    case PaintType.CarbonFiber:
                        bodyMaterial.SetFloat("_Metallic", 0.4f);
                        bodyMaterial.SetFloat("_Smoothness", 0.6f);
                        break;
                    case PaintType.Chameleon:
                        bodyMaterial.SetFloat("_Metallic", 0.7f);
                        bodyMaterial.SetFloat("_Smoothness", 0.85f);
                        // Color shift would be handled by a special shader
                        break;
                }
            }

            if (windowMaterial != null)
            {
                Color glass = windowMaterial.GetColor("_BaseColor");
                glass.a = 1f - windowTint * 0.85f;
                windowMaterial.SetColor("_BaseColor", glass);
            }
        }

        public void SetAirPreset(int index)
        {
            currentAirPreset = Mathf.Clamp(index, 0, airHeightPresets.Length - 1);
            ApplySuspensionGeometry();
        }

        public void CycleEngine()
        {
            currentEngine = (EngineType)(((int)currentEngine + 1) % System.Enum.GetValues(typeof(EngineType)).Length);
            ApplyEngineAndECU();
        }
    }
}
