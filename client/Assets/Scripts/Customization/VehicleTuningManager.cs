using System;
using UnityEngine;
using NextGenDrive.Physics;

namespace NextGenDrive.Customization
{
    public enum PaintFinishType
    {
        Gloss,
        Metallic,
        Matte,
        Chameleon,
        ForgedCarbon
    }

    [Serializable]
    public class EnginePreset
    {
        public string engineCode;      // e.g., "I6_2JZ_GTE", "V8_4.0_BITURBO", "V10_5.2_NA"
        public float baseTorqueNm;
        public float redlineRPM;
        public float engineWeightKg;
    }

    [Serializable]
    public class StanceSetup
    {
        [Range(-12f, 2f)] public float frontCamberAngle = -1.5f;
        [Range(-15f, 2f)] public float rearCamberAngle = -2.0f;
        [Range(0.08f, 0.35f)] public float rideHeightMeters = 0.16f;
        [Range(15000f, 65000f)] public float springStiffness = 35000f;
    }

    public class VehicleTuningManager : MonoBehaviour
    {
        [Header("References")]
        public RealisticCarController carController;
        public Renderer[] bodyRenderers;
        public GameObject[] frontBumpers;
        public GameObject[] rearBumpers;
        public GameObject[] spoilers;

        [Header("Performance Upgrades")]
        public EnginePreset currentEngine;
        [Range(0, 3)] public int ecuStage = 0;
        [Range(0f, 2.5f)] public float turboBoostBar = 0f;

        [Header("Stance & Air Suspension")]
        public StanceSetup stance = new StanceSetup();

        [Header("Paint & Visuals")]
        public PaintFinishType paintType = PaintFinishType.Metallic;
        public Color primaryColor = Color.black;
        public Color secondaryChameleonColor = new Color(0.2f, 0.0f, 0.5f);

        private MaterialPropertyBlock paintBlock;

        private void Awake()
        {
            paintBlock = new MaterialPropertyBlock();
        }

        public void ApplyAllTuning()
        {
            if (currentEngine == null)
            {
                Debug.LogWarning("VehicleTuningManager: currentEngine is not set.");
                return;
            }

            ApplyPerformanceToPhysics();
            ApplyStanceAndSuspension();
            ApplyPaintShader();
        }

        public float CalculateVerifiedHorsepower()
        {
            if (currentEngine == null) return 0f;

            float stageMultiplier = 1f + (ecuStage * 0.12f);
            float boostMultiplier = 1f + (turboBoostBar * 0.35f);
            float peakTorque = currentEngine.baseTorqueNm * stageMultiplier * boostMultiplier;
            return Mathf.Round((peakTorque * currentEngine.redlineRPM) / 7127f);
        }

        private void ApplyPerformanceToPhysics()
        {
            if (carController == null) return;

            float stageMultiplier = 1f + (ecuStage * 0.12f);
            float boostMultiplier = 1f + (turboBoostBar * 0.35f);
            float finalPeakTorque = currentEngine.baseTorqueNm * stageMultiplier * boostMultiplier;

            carController.maxRPM = currentEngine.redlineRPM;
            carController.torqueCurve = new AnimationCurve(
                new Keyframe(900f, finalPeakTorque * 0.45f),
                new Keyframe(currentEngine.redlineRPM * 0.55f, finalPeakTorque),
                new Keyframe(currentEngine.redlineRPM * 0.88f, finalPeakTorque * 0.90f),
                new Keyframe(currentEngine.redlineRPM + 300f, 0f)
            );
        }

        private void ApplyStanceAndSuspension()
        {
            if (carController == null || carController.wheels == null) return;

            foreach (var w in carController.wheels)
            {
                if (w.collider == null) continue;

                w.collider.suspensionDistance = stance.rideHeightMeters;
                JointSpring spring = w.collider.suspensionSpring;
                spring.spring = stance.springStiffness;
                spring.damper = stance.springStiffness * 0.14f;
                w.collider.suspensionSpring = spring;

                // Apply visual camber angle (Razval)
                if (w.visualMesh != null)
                {
                    bool isFront = w.collider.transform.localPosition.z > 0;
                    float camber = isFront ? stance.frontCamberAngle : stance.rearCamberAngle;
                    float sideSign = w.collider.transform.localPosition.x > 0 ? 1f : -1f;
                    w.visualMesh.localRotation = Quaternion.Euler(0f, 0f, camber * sideSign);
                }
            }
        }

        private void ApplyPaintShader()
        {
            float smoothness = 0.92f;
            float metallic = 0.85f;
            float chameleonBlend = 0f;

            switch (paintType)
            {
                case PaintFinishType.Gloss:
                    smoothness = 0.95f; metallic = 0.1f; break;
                case PaintFinishType.Metallic:
                    smoothness = 0.92f; metallic = 0.88f; break;
                case PaintFinishType.Matte:
                    smoothness = 0.22f; metallic = 0.35f; break;
                case PaintFinishType.Chameleon:
                    smoothness = 0.96f; metallic = 0.92f; chameleonBlend = 1.0f; break;
                case PaintFinishType.ForgedCarbon:
                    smoothness = 0.80f; metallic = 0.60f; break;
            }

            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(paintBlock);
                paintBlock.SetColor("_BaseColor", primaryColor);
                paintBlock.SetColor("_SecondaryColor", secondaryChameleonColor);
                paintBlock.SetFloat("_Smoothness", smoothness);
                paintBlock.SetFloat("_Metallic", metallic);
                paintBlock.SetFloat("_ChameleonFactor", chameleonBlend);
                r.SetPropertyBlock(paintBlock);
            }
        }

        public void EquipBodyPart(GameObject[] partGroup, int selectedIndex)
        {
            for (int i = 0; i < partGroup.Length; i++)
            {
                if (partGroup[i] != null)
                    partGroup[i].SetActive(i == selectedIndex);
            }
        }
    }
}
