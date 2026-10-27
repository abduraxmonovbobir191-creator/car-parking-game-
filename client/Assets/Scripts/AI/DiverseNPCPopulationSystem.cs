using System.Collections.Generic;
using UnityEngine;

namespace CarParking.AI
{
    /// <summary>
    /// Procedural NPC variety: clothing colors, scale, idle behaviors
    /// (phone, carrying, sitting).
    /// </summary>
    public class DiverseNPCPopulationSystem : MonoBehaviour
    {
        public static DiverseNPCPopulationSystem Instance { get; private set; }

        public enum IdleBehavior { Walk, Phone, CarryBox, Sit, Talk }

        [Header("Prefabs")]
        public GameObject[] npcPrefabs;
        public int populationCount = 50;
        public float spawnRadius = 100f;

        [Header("Variety")]
        public Color[] skinTones;
        public Color[] clothingColors;

        private Transform _player;
        private List<NPCInstance> _npcs = new();

        private class NPCInstance
        {
            public GameObject go;
            public Transform transform;
            public IdleBehavior behavior;
            public Vector3 anchor;
            public float phase;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (skinTones == null || skinTones.Length == 0)
            {
                skinTones = new[] {
                    new Color(1f, 0.86f, 0.75f),
                    new Color(0.92f, 0.75f, 0.6f),
                    new Color(0.7f, 0.5f, 0.35f),
                    new Color(0.45f, 0.3f, 0.2f),
                    new Color(0.25f, 0.18f, 0.12f)
                };
            }
            if (clothingColors == null || clothingColors.Length == 0)
            {
                clothingColors = new[] {
                    Color.red, Color.blue, Color.green, Color.black,
                    Color.white, new Color(0.3f, 0.3f, 0.35f), new Color(0.8f, 0.6f, 0.2f)
                };
            }
        }

        private void Start()
        {
            var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
            if (v != null) _player = v.transform;
            SpawnPopulation();
        }

        private void Update()
        {
            if (_player == null)
            {
                var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
                if (v != null) _player = v.transform;
            }

            foreach (var npc in _npcs)
            {
                if (npc.transform == null) continue;
                UpdateBehavior(npc);

                // Respawn if too far
                if (_player != null && Vector3.Distance(npc.transform.position, _player.position) > spawnRadius * 1.6f)
                    Relocate(npc);
            }
        }

        private void SpawnPopulation()
        {
            for (int i = 0; i < populationCount; i++)
            {
                Vector3 pos = RandomPointNearPlayer();
                GameObject go;

                if (npcPrefabs != null && npcPrefabs.Length > 0)
                    go = Instantiate(npcPrefabs[Random.Range(0, npcPrefabs.Length)], pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.position = pos;
                    go.transform.localScale = new Vector3(0.4f, 0.9f + Random.Range(-0.1f, 0.15f), 0.4f);
                    go.name = "NPC_" + i;
                }

                ApplyVariety(go);

                _npcs.Add(new NPCInstance
                {
                    go = go,
                    transform = go.transform,
                    behavior = (IdleBehavior)Random.Range(0, 5),
                    anchor = pos,
                    phase = Random.Range(0f, 100f)
                });
            }
        }

        private void ApplyVariety(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.material == null) continue;
                // Simple color variation
                if (Random.value > 0.5f)
                    r.material.color = clothingColors[Random.Range(0, clothingColors.Length)];
                else
                    r.material.color = skinTones[Random.Range(0, skinTones.Length)];
            }
        }

        private void UpdateBehavior(NPCInstance npc)
        {
            npc.phase += Time.deltaTime;
            switch (npc.behavior)
            {
                case IdleBehavior.Walk:
                    Vector3 walkTarget = npc.anchor + new Vector3(Mathf.Sin(npc.phase * 0.3f), 0f, Mathf.Cos(npc.phase * 0.25f)) * 6f;
                    npc.transform.position = Vector3.MoveTowards(npc.transform.position, walkTarget, 1.2f * Time.deltaTime);
                    break;
                case IdleBehavior.Phone:
                    // Stay near anchor, slight idle sway
                    npc.transform.position = npc.anchor + Vector3.up * Mathf.Sin(npc.phase * 2f) * 0.02f;
                    break;
                case IdleBehavior.CarryBox:
                    Vector3 carryTarget = npc.anchor + new Vector3(Mathf.Sin(npc.phase * 0.2f), 0f, Mathf.Cos(npc.phase * 0.2f)) * 4f;
                    npc.transform.position = Vector3.MoveTowards(npc.transform.position, carryTarget, 0.9f * Time.deltaTime);
                    break;
                case IdleBehavior.Sit:
                    npc.transform.position = npc.anchor;
                    break;
                case IdleBehavior.Talk:
                    npc.transform.position = npc.anchor + Vector3.right * Mathf.Sin(npc.phase) * 0.1f;
                    break;
            }
        }

        private Vector3 RandomPointNearPlayer()
        {
            Vector3 center = _player != null ? _player.position : Vector3.zero;
            Vector2 o = Random.insideUnitCircle * spawnRadius;
            return new Vector3(center.x + o.x, 0.1f, center.z + o.y);
        }

        private void Relocate(NPCInstance npc)
        {
            Vector3 p = RandomPointNearPlayer();
            npc.transform.position = p;
            npc.anchor = p;
            npc.behavior = (IdleBehavior)Random.Range(0, 5);
            ApplyVariety(npc.go);
        }
    }
}
