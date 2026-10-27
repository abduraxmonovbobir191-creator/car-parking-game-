using System.Collections.Generic;
using UnityEngine;
using CarParking.World;

namespace CarParking.World
{
    /// <summary>
    /// Runtime procedural megacity: skyscrapers, multi-level parking,
    /// tunnels, neon signage across streamed chunks.
    /// </summary>
    public class ProceduralMegacityGenerator : MonoBehaviour
    {
        public static ProceduralMegacityGenerator Instance { get; private set; }

        [Header("Prefabs")]
        public GameObject[] skyscraperPrefabs;
        public GameObject parkingStructurePrefab;
        public GameObject tunnelSegmentPrefab;
        public GameObject neonSignPrefab;

        [Header("Density")]
        public int buildingsPerCityChunk = 24;
        public int neonPerChunk = 8;
        public float minBuildingSpacing = 28f;
        public Vector2 heightScaleRange = new Vector2(0.6f, 2.4f);

        private Dictionary<Vector2Int, List<GameObject>> _chunkObjects = new();
        private Transform _player;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
            if (v != null) _player = v.transform;
        }

        private void Update()
        {
            if (_player == null)
            {
                var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
                if (v != null) _player = v.transform;
                return;
            }

            // Generate around player in city-like zones
            Vector2Int chunk = new Vector2Int(
                Mathf.FloorToInt(_player.position.x / 512f),
                Mathf.FloorToInt(_player.position.z / 512f)
            );

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                Vector2Int c = new Vector2Int(chunk.x + dx, chunk.y + dz);
                if (!_chunkObjects.ContainsKey(c) && IsCityChunk(c))
                    GenerateChunk(c);
            }

            // Unload far chunks
            List<Vector2Int> remove = new();
            foreach (var kvp in _chunkObjects)
            {
                if (Mathf.Abs(kvp.Key.x - chunk.x) > 2 || Mathf.Abs(kvp.Key.y - chunk.y) > 2)
                    remove.Add(kvp.Key);
            }
            foreach (var c in remove) UnloadChunk(c);
        }

        private bool IsCityChunk(Vector2Int coord)
        {
            float nx = (coord.x + 0.5f) * 512f * 0.0008f;
            float nz = (coord.y + 0.5f) * 512f * 0.0008f;
            float city = Mathf.PerlinNoise(nx * 3.1f + 50f, nz * 3.1f + 50f);
            float continental = Mathf.PerlinNoise(nx * 0.4f, nz * 0.4f);
            return city > 0.72f && continental > 0.35f && continental < 0.75f;
        }

        private void GenerateChunk(Vector2Int coord)
        {
            List<GameObject> objs = new();
            Vector3 origin = new Vector3(coord.x * 512f, 0f, coord.y * 512f);
            System.Random rng = new System.Random(coord.x * 73856093 ^ coord.y * 19349663);

            // Skyscrapers
            int placed = 0;
            int attempts = 0;
            while (placed < buildingsPerCityChunk && attempts < buildingsPerCityChunk * 4)
            {
                attempts++;
                float px = (float)rng.NextDouble() * 480f + 16f;
                float pz = (float)rng.NextDouble() * 480f + 16f;
                Vector3 pos = origin + new Vector3(px, 0f, pz);

                bool ok = true;
                foreach (var go in objs)
                {
                    if (go != null && Vector3.Distance(go.transform.position, pos) < minBuildingSpacing)
                    { ok = false; break; }
                }
                if (!ok) continue;

                GameObject prefab = null;
                if (skyscraperPrefabs != null && skyscraperPrefabs.Length > 0)
                    prefab = skyscraperPrefabs[rng.Next(skyscraperPrefabs.Length)];

                GameObject b;
                if (prefab != null)
                    b = Instantiate(prefab, pos, Quaternion.Euler(0f, rng.Next(0, 4) * 90f, 0f), transform);
                else
                {
                    b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.transform.position = pos + Vector3.up * 20f;
                    b.transform.localScale = new Vector3(12f, 40f * Mathf.Lerp(heightScaleRange.x, heightScaleRange.y, (float)rng.NextDouble()), 12f);
                    b.transform.SetParent(transform);
                }

                float hScale = Mathf.Lerp(heightScaleRange.x, heightScaleRange.y, (float)rng.NextDouble());
                b.transform.localScale = new Vector3(b.transform.localScale.x, b.transform.localScale.y * hScale, b.transform.localScale.z);
                objs.Add(b);
                placed++;
            }

            // Multi-level parking
            if (parkingStructurePrefab != null && rng.NextDouble() > 0.4)
            {
                Vector3 ppos = origin + new Vector3(256f, 0f, 256f);
                var p = Instantiate(parkingStructurePrefab, ppos, Quaternion.identity, transform);
                objs.Add(p);
            }

            // Tunnel entrance marker
            if (tunnelSegmentPrefab != null && rng.NextDouble() > 0.7)
            {
                Vector3 tpos = origin + new Vector3(100f, -2f, 400f);
                var t = Instantiate(tunnelSegmentPrefab, tpos, Quaternion.identity, transform);
                objs.Add(t);
            }

            // Neon signs
            if (neonSignPrefab != null)
            {
                for (int i = 0; i < neonPerChunk; i++)
                {
                    Vector3 npos = origin + new Vector3(
                        (float)rng.NextDouble() * 500f,
                        15f + (float)rng.NextDouble() * 30f,
                        (float)rng.NextDouble() * 500f);
                    var n = Instantiate(neonSignPrefab, npos, Quaternion.Euler(0f, rng.Next(0, 360), 0f), transform);
                    objs.Add(n);
                }
            }

            _chunkObjects[coord] = objs;
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (!_chunkObjects.TryGetValue(coord, out var list)) return;
            foreach (var go in list)
                if (go != null) Destroy(go);
            _chunkObjects.Remove(coord);
        }
    }
}
