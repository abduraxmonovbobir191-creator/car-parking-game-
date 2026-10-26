using UnityEngine;

namespace NextGenDrive.Graphics
{
    public class PhotorealismLODManager : MonoBehaviour
    {
        [Header("Target Camera")]
        public Transform activeCamera;

        [Header("Skin & Car Micro-Detail Settings")]
        public Renderer characterSkinRenderer;
        public Renderer carBodyRenderer;

        public float ultraDetailDistance = 3.5f;   // 4K Skin pores & Subsurface Scattering
        public float mediumDetailDistance = 15.0f; // Standard PBR

        private static readonly int MicroPoreStrengthID = Shader.PropertyToID("_MicroPoreDetailStrength");
        private static readonly int SubsurfaceScatteringID = Shader.PropertyToID("_SubsurfaceRadius");
        private static readonly int ClearCoatSmoothnessID = Shader.PropertyToID("_ClearCoatSmoothness");

        private MaterialPropertyBlock skinPropBlock;
        private MaterialPropertyBlock carPropBlock;

        private void Awake()
        {
            skinPropBlock = new MaterialPropertyBlock();
            carPropBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (activeCamera == null) return;

            float distance = Vector3.Distance(activeCamera.position, transform.position);
            UpdateSkinFidelity(distance);
            UpdateCarPaintFidelity(distance);
        }

        private void UpdateSkinFidelity(float dist)
        {
            if (characterSkinRenderer == null) return;

            // Smoothly blend micro-pores when camera zooms close to character face/skin
            float poreStrength = Mathf.InverseLerp(mediumDetailDistance, ultraDetailDistance, dist);
            float sssRadius = Mathf.Lerp(0.2f, 1.0f, poreStrength);

            characterSkinRenderer.GetPropertyBlock(skinPropBlock);
            skinPropBlock.SetFloat(MicroPoreStrengthID, poreStrength);
            skinPropBlock.SetFloat(SubsurfaceScatteringID, sssRadius);
            characterSkinRenderer.SetPropertyBlock(skinPropBlock);
        }

        private void UpdateCarPaintFidelity(float dist)
        {
            if (carBodyRenderer == null) return;

            float flakeDetail = Mathf.InverseLerp(mediumDetailDistance * 1.5f, ultraDetailDistance, dist);
            carBodyRenderer.GetPropertyBlock(carPropBlock);
            carPropBlock.SetFloat(ClearCoatSmoothnessID, Mathf.Lerp(0.85f, 0.99f, flakeDetail));
            carBodyRenderer.SetPropertyBlock(carPropBlock);
        }
    }
}
