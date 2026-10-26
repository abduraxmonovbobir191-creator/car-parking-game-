using System;
using UnityEngine;

namespace NextGenDrive.Physics
{
    public class VehicleDamageDeformer : MonoBehaviour
    {
        [Header("Deformable Body Meshes")]
        public MeshFilter[] deformableMeshes;
        public float deformationRadius = 0.65f;
        public float maxVertexDisplacement = 0.35f;
        public float damageSensitivity = 0.025f;
        public float minImpactSpeedKmh = 18f;

        [Header("Mechanical Health (0 - 100%)")]
        [Range(0f, 100f)] public float bodyIntegrity = 100f;
        [Range(0f, 100f)] public float engineHealth = 100f;

        public event Action<string, float> OnSevereAccidentRecorded; // VIN, ImpactForce

        private Vector3[][] originalVertices;
        private Vector3[][] currentVertices;
        private RealisticCarController carController;

        private void Awake()
        {
            carController = GetComponent<RealisticCarController>();
            CacheOriginalMeshes();
        }

        private void CacheOriginalMeshes()
        {
            originalVertices = new Vector3[deformableMeshes.Length][];
            currentVertices = new Vector3[deformableMeshes.Length][];

            for (int i = 0; i < deformableMeshes.Length; i++)
            {
                if (deformableMeshes[i] == null) continue;
                originalVertices[i] = deformableMeshes[i].mesh.vertices;
                currentVertices[i] = (Vector3[])originalVertices[i].Clone();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactVelocityKmh = collision.relativeVelocity.magnitude * 3.6f;
            if (impactVelocityKmh < minImpactSpeedKmh) return;

            ContactPoint contact = collision.contacts[0];
            Vector3 impactVelocity = collision.relativeVelocity * damageSensitivity;

            DeformMeshesAtPoint(contact.point, impactVelocity);
            ApplyMechanicalDamage(impactVelocityKmh);
        }

        private void DeformMeshesAtPoint(Vector3 worldContactPoint, Vector3 impactVector)
        {
            for (int i = 0; i < deformableMeshes.Length; i++)
            {
                MeshFilter mf = deformableMeshes[i];
                if (mf == null) continue;

                Vector3 localContact = mf.transform.InverseTransformPoint(worldContactPoint);
                Vector3 localImpact = mf.transform.InverseTransformDirection(impactVector);
                bool meshModified = false;

                for (int v = 0; v < currentVertices[i].Length; v++)
                {
                    float dist = Vector3.Distance(currentVertices[i][v], localContact);
                    if (dist < deformationRadius)
                    {
                        float falloff = 1f - (dist / deformationRadius);
                        Vector3 deformDelta = localImpact * (falloff * falloff);
                        currentVertices[i][v] += deformDelta;

                        // Clamp maximum dent depth so mesh doesn't invert
                        Vector3 totalDisp = currentVertices[i][v] - originalVertices[i][v];
                        if (totalDisp.magnitude > maxVertexDisplacement)
                        {
                            currentVertices[i][v] = originalVertices[i][v] + totalDisp.normalized * maxVertexDisplacement;
                        }
                        meshModified = true;
                    }
                }

                if (meshModified)
                {
                    mf.mesh.vertices = currentVertices[i];
                    mf.mesh.RecalculateNormals();
                    mf.mesh.RecalculateBounds();
                }
            }
        }

        private void ApplyMechanicalDamage(float impactSpeedKmh)
        {
            float damageAmount = Mathf.Clamp((impactSpeedKmh - minImpactSpeedKmh) * 0.22f, 1f, 45f);
            bodyIntegrity = Mathf.Max(0f, bodyIntegrity - damageAmount);
            engineHealth = Mathf.Max(15f, engineHealth - (damageAmount * 0.6f));

            // Record severe crash on Carfax VIN History if impact > 65 km/h
            if (impactSpeedKmh > 65f && carController != null)
            {
                OnSevereAccidentRecorded?.Invoke(carController.vehicleVIN, impactSpeedKmh);
            }
        }

        public void RepairVehicleCompletely()
        {
            for (int i = 0; i < deformableMeshes.Length; i++)
            {
                if (deformableMeshes[i] == null) continue;
                currentVertices[i] = (Vector3[])originalVertices[i].Clone();
                deformableMeshes[i].mesh.vertices = currentVertices[i];
                deformableMeshes[i].mesh.RecalculateNormals();
            }
            bodyIntegrity = 100f;
            engineHealth = 100f;
        }
    }
}
