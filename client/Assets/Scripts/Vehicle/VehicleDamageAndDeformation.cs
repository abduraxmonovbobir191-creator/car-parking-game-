using System.Collections.Generic;
using UnityEngine;
using CarParking.Vehicle;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Soft-body mesh deformation, detachable parts, mechanical failures and repair system.
    /// Integrates with DrivetrainSystem and RealisticVehicleController.
    /// </summary>
    public class VehicleDamageAndDeformation : MonoBehaviour
    {
        [System.Serializable]
        public class DetachablePart
        {
            public string partName;
            public Transform partTransform;
            public float breakForce = 25000f;
            public bool isDetached;
            public bool isGlass;
        }

        [Header("Deformation")]
        public float deformationRadius = 0.6f;
        public float deformationStrength = 0.35f;
        public float maxVertexDisplacement = 0.45f;
        public float structuralStiffness = 0.7f;

        [Header("Detachable Parts")]
        public DetachablePart[] detachableParts;

        [Header("Mechanical")]
        public float radiatorHealth = 1f;
        public float engineHealth = 1f;
        public float[] tireHealth = { 1f, 1f, 1f, 1f };
        public float suspensionHealth = 1f;

        [Header("References")]
        public MeshFilter[] bodyMeshFilters;

        private Dictionary<MeshFilter, Vector3[]> _originalVertices = new();
        private Dictionary<MeshFilter, Vector3[]> _workingVertices = new();
        private DrivetrainSystem _drivetrain;
        private RealisticVehicleController _vehicle;
        private Rigidbody _rb;

        private void Awake()
        {
            _drivetrain = GetComponent<DrivetrainSystem>();
            _vehicle = GetComponent<RealisticVehicleController>();
            _rb = GetComponent<Rigidbody>();

            if (bodyMeshFilters != null)
            {
                foreach (var mf in bodyMeshFilters)
                {
                    if (mf == null || mf.mesh == null) continue;
                    Mesh mesh = Instantiate(mf.mesh);
                    mf.mesh = mesh;
                    _originalVertices[mf] = mesh.vertices;
                    _workingVertices[mf] = (Vector3[])mesh.vertices.Clone();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;

            float impulse = collision.impulse.magnitude;
            if (impulse < 800f) return;

            ContactPoint contact = collision.GetContact(0);
            ApplyDeformation(contact.point, contact.normal, impulse);

            TryDetachParts(contact.point, impulse);
            ApplyMechanicalDamage(impulse, contact.point);
        }

        private void ApplyDeformation(Vector3 worldPoint, Vector3 normal, float impulse)
        {
            float force = Mathf.Clamp01(impulse / 15000f) * deformationStrength * (1f - structuralStiffness);

            foreach (var kvp in _workingVertices)
            {
                MeshFilter mf = kvp.Key;
                Vector3[] verts = kvp.Value;
                Vector3 localPoint = mf.transform.InverseTransformPoint(worldPoint);
                Vector3 localNormal = mf.transform.InverseTransformDirection(normal);

                bool modified = false;
                for (int i = 0; i < verts.Length; i++)
                {
                    float dist = Vector3.Distance(verts[i], localPoint);
                    if (dist < deformationRadius)
                    {
                        float falloff = 1f - (dist / deformationRadius);
                        falloff *= falloff;
                        Vector3 displacement = -localNormal * force * falloff * maxVertexDisplacement;
                        verts[i] += displacement;
                        modified = true;
                    }
                }

                if (modified)
                {
                    Mesh mesh = mf.mesh;
                    mesh.vertices = verts;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                }
            }
        }

        private void TryDetachParts(Vector3 impactPoint, float impulse)
        {
            if (detachableParts == null) return;

            foreach (var part in detachableParts)
            {
                if (part.isDetached || part.partTransform == null) continue;

                float dist = Vector3.Distance(impactPoint, part.partTransform.position);
                if (dist > 1.8f) continue;

                if (impulse > part.breakForce)
                {
                    part.isDetached = true;
                    DetachPart(part);
                }
            }
        }

        private void DetachPart(DetachablePart part)
        {
            part.partTransform.SetParent(null);

            var rb = part.partTransform.gameObject.AddComponent<Rigidbody>();
            rb.mass = part.isGlass ? 2f : 12f;
            rb.AddForce(Random.insideUnitSphere * 4f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);

            if (part.isGlass)
            {
                // Simple glass shatter effect - disable renderer after short delay
                var rend = part.partTransform.GetComponent<Renderer>();
                if (rend != null) rend.enabled = false;
            }
        }

        private void ApplyMechanicalDamage(float impulse, Vector3 point)
        {
            float damage = Mathf.Clamp01(impulse / 20000f);

            // Radiator (front impact)
            if (transform.InverseTransformPoint(point).z > 0.8f)
            {
                radiatorHealth = Mathf.Max(0f, radiatorHealth - damage * 0.6f);
            }

            // Engine
            engineHealth = Mathf.Max(0f, engineHealth - damage * 0.35f);

            // Suspension
            suspensionHealth = Mathf.Max(0.3f, suspensionHealth - damage * 0.25f);

            // Tires (nearest wheel)
            if (_vehicle != null && _vehicle.wheels != null)
            {
                int nearest = 0;
                float minDist = float.MaxValue;
                for (int i = 0; i < _vehicle.wheels.Length; i++)
                {
                    float d = Vector3.Distance(point, _vehicle.wheels[i].wheelTransform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        nearest = i;
                    }
                }
                if (minDist < 1.2f && nearest < tireHealth.Length)
                    tireHealth[nearest] = Mathf.Max(0f, tireHealth[nearest] - damage * 0.8f);
            }

            ApplyMechanicalEffects();
        }

        private void ApplyMechanicalEffects()
        {
            if (_drivetrain != null)
            {
                // Power loss from engine damage
                float powerMult = 0.4f + engineHealth * 0.6f;
                _drivetrain.maxTorque *= powerMult;

                // Overheating from radiator leak
                if (radiatorHealth < 0.4f)
                {
                    // Gradually reduce max RPM
                    _drivetrain.maxRpm = Mathf.Lerp(_drivetrain.maxRpm, 4500f, Time.deltaTime * 0.1f);
                }
            }

            if (_vehicle != null)
            {
                // Bent suspension affects handling
                _vehicle.lateralStiffness *= (0.5f + suspensionHealth * 0.5f);
            }
        }

        public void FullRepair()
        {
            radiatorHealth = 1f;
            engineHealth = 1f;
            suspensionHealth = 1f;
            for (int i = 0; i < tireHealth.Length; i++)
                tireHealth[i] = 1f;

            // Restore meshes
            foreach (var kvp in _originalVertices)
            {
                MeshFilter mf = kvp.Key;
                if (mf == null || mf.mesh == null) continue;

                Vector3[] restored = (Vector3[])kvp.Value.Clone();
                _workingVertices[mf] = restored;
                mf.mesh.vertices = restored;
                mf.mesh.RecalculateNormals();
                mf.mesh.RecalculateBounds();
            }

            // Re-attach parts is complex; for simplicity we just reset flags
            if (detachableParts != null)
            {
                foreach (var part in detachableParts)
                    part.isDetached = false;
            }

            if (_drivetrain != null)
            {
                // Re-apply original torque from tuning if available
                var tuning = GetComponent<VehicleTuningSystem>();
                if (tuning != null) tuning.ApplyEngineAndECU();
            }

            Debug.Log("[Damage] Vehicle fully repaired.");
        }

        private void Update()
        {
            // Continuous overheating effect
            if (radiatorHealth < 0.5f && _drivetrain != null && _drivetrain.throttleInput > 0.3f)
            {
                engineHealth = Mathf.Max(0.1f, engineHealth - Time.deltaTime * 0.01f);
            }
        }
    }
}
