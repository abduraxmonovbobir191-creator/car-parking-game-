using UnityEngine;

namespace CarParking.Vehicle
{
    /// <summary>
    /// Shatterable windshield/windows with spider-web crack stages,
    /// physical glass shards and debris (plastic, metal) on heavy impacts.
    /// </summary>
    public class ShatterableGlassAndDebrisSystem : MonoBehaviour
    {
        [System.Serializable]
        public class GlassPanel
        {
            public string name;
            public GameObject glassObject;
            public MeshRenderer renderer;
            public float health = 1f;
            public bool isShattered;
            public Material crackedMaterial;
            public Material shatteredMaterial;
        }

        [Header("Glass Panels")]
        public GlassPanel[] panels;

        [Header("Impact")]
        public float crackThreshold = 4000f;
        public float shatterThreshold = 12000f;

        [Header("Debris")]
        public GameObject[] debrisPrefabs;
        public int debrisCount = 8;
        public float debrisForce = 6f;
        public GameObject sparkPrefab;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (panels != null)
            {
                foreach (var p in panels)
                {
                    if (p.glassObject != null && p.renderer == null)
                        p.renderer = p.glassObject.GetComponent<MeshRenderer>();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;
            float impulse = collision.impulse.magnitude;
            if (impulse < crackThreshold) return;

            ContactPoint cp = collision.GetContact(0);
            ProcessGlassImpact(cp.point, impulse);

            if (impulse > shatterThreshold * 0.7f)
                SpawnDebris(cp.point, collision.relativeVelocity);
        }

        private void ProcessGlassImpact(Vector3 worldPoint, float impulse)
        {
            if (panels == null) return;

            GlassPanel closest = null;
            float bestDist = 2.5f;

            foreach (var p in panels)
            {
                if (p.isShattered || p.glassObject == null) continue;
                float d = Vector3.Distance(worldPoint, p.glassObject.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    closest = p;
                }
            }

            if (closest == null) return;

            if (impulse >= shatterThreshold)
            {
                ShatterPanel(closest, worldPoint);
            }
            else
            {
                // Crack stage
                closest.health = Mathf.Max(0f, closest.health - impulse / shatterThreshold);
                if (closest.renderer != null && closest.crackedMaterial != null)
                    closest.renderer.material = closest.crackedMaterial;

                if (closest.health <= 0.15f)
                    ShatterPanel(closest, worldPoint);
            }
        }

        private void ShatterPanel(GlassPanel panel, Vector3 impactPoint)
        {
            panel.isShattered = true;
            panel.health = 0f;

            if (panel.renderer != null)
            {
                if (panel.shatteredMaterial != null)
                    panel.renderer.material = panel.shatteredMaterial;
                else
                    panel.renderer.enabled = false;
            }

            // Spawn glass shards
            for (int i = 0; i < 6; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "GlassShard";
                shard.transform.position = impactPoint + Random.insideUnitSphere * 0.2f;
                shard.transform.localScale = Vector3.one * Random.Range(0.04f, 0.12f);
                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.AddForce(Random.insideUnitSphere * debrisForce + Vector3.up * 2f, ForceMode.Impulse);
                Destroy(shard, 5f);
            }

            Debug.Log("[Glass] Shattered: " + panel.name);
        }

        private void SpawnDebris(Vector3 point, Vector3 relativeVel)
        {
            if (sparkPrefab != null)
            {
                var spark = Instantiate(sparkPrefab, point, Quaternion.identity);
                Destroy(spark, 2f);
            }

            int count = debrisPrefabs != null && debrisPrefabs.Length > 0 ? debrisCount : 4;
            for (int i = 0; i < count; i++)
            {
                GameObject debris;
                if (debrisPrefabs != null && debrisPrefabs.Length > 0)
                    debris = Instantiate(debrisPrefabs[Random.Range(0, debrisPrefabs.Length)], point, Random.rotation);
                else
                {
                    debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    debris.transform.position = point;
                    debris.transform.localScale = Vector3.one * Random.Range(0.08f, 0.2f);
                }

                var rb = debris.GetComponent<Rigidbody>();
                if (rb == null) rb = debris.AddComponent<Rigidbody>();
                rb.mass = Random.Range(0.1f, 0.5f);
                Vector3 force = relativeVel.normalized * debrisForce + Random.insideUnitSphere * debrisForce * 0.5f;
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
                Destroy(debris, 8f);
            }
        }

        public void RepairAllGlass()
        {
            if (panels == null) return;
            foreach (var p in panels)
            {
                p.health = 1f;
                p.isShattered = false;
                if (p.renderer != null)
                {
                    p.renderer.enabled = true;
                    // Original material should be restored by vehicle tuning/visual system if needed
                }
            }
        }
    }
}
