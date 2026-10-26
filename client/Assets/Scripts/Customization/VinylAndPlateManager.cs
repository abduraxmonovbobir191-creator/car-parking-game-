using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace NextGenDrive.Customization
{
    [Serializable]
    public class VinylDecalLayer
    {
        public string decalTextureId;
        public Vector2 uvPosition;
        public Vector2 uvScale = Vector2.one;
        public float rotationAngle;
        public Color decalColor = Color.white;
        public bool isMirrorSymmetrical;
    }

    public class VinylAndPlateManager : MonoBehaviour
    {
        [Header("License Plate UI / 3D Text")]
        public string regionCode = "01";
        public string plateNumber = "A 777 AA";
        public string countryTag = "UZ";
        public bool isVipGoldPlate = false;

        public TextMeshPro frontPlateText;
        public TextMeshPro rearPlateText;

        [Header("Custom Livery Layers (Max 64)")]
        public List<VinylDecalLayer> vinylLayers = new List<VinylDecalLayer>();
        public const int MaxVinylLayers = 64;

        private void Start()
        {
            UpdateLicensePlateVisuals();
        }

        public void SetCustomLicensePlate(string region, string number, bool vipStatus)
        {
            regionCode = region.ToUpper();
            plateNumber = number.ToUpper();
            isVipGoldPlate = vipStatus;
            UpdateLicensePlateVisuals();
        }

        public void UpdateLicensePlateVisuals()
        {
            string formatted = $"{regionCode} | {plateNumber}";
            if (frontPlateText != null) frontPlateText.text = formatted;
            if (rearPlateText != null) rearPlateText.text = formatted;
        }

        public bool AddVinylLayer(VinylDecalLayer newLayer)
        {
            if (vinylLayers.Count >= MaxVinylLayers) return false;
            vinylLayers.Add(newLayer);
            return true;
        }

        public string ExportLiveryJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
