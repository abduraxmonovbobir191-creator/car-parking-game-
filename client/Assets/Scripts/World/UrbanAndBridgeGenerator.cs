using System.Collections.Generic;
using UnityEngine;

namespace CarParking.World
{
    /// <summary>
    /// Procedural megacity buildings, multi-level parking garages and suspension bridges.
    /// Activated for BiomeType.City and across Ocean biomes.
    /// </summary>
    public class UrbanAndBridgeGenerator : MonoBehaviour
    {
        [Header("City")]
        public GameObject[] skyscraperPrefabs;
        public GameObject parkingGaragePrefab;
        public GameObject roadSegmentPrefab;
        public int buildingsPerCityChunk = 18;

        [Header("Bridges")]
        public GameObject bridgePylonPrefab;
        public GameObject bridgeDeckPrefab;
        public float bridgeMaxSpan = 1200f;

        private Dictionary<Vector2Int, List<GameObject>> _spawned = new();

        public void GenerateForChunk(TerrainChunk chunk)
        {
            if (chunk.DominantBiome == BiomeType.City)
            {
                GenerateCity(chunk);
            }
            else if (chunk.DominantBiome == BiomeType.Ocean)
            {
                // Opportunity for bridges is decided at a higher level (between land chunks)
            }
        }

        private void GenerateCity(TerrainChunk chunk)
        {
            if (_spawned.ContainsKey(chunk.Coord)) return;

            List<GameObject> objects = new();
            Vector3 chunkOrigin = chunk.transform.position;
            float size = 512f;

            // Simple road grid
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 0.5f) / 4f;
                // Horizontal roads
                SpawnRoad(chunkOrigin + new Vector3(size * 0.5f, 0.5f, t * size), true, objects);
                // Vertical roads
                SpawnRoad(chunkOrigin + new Vector3(t * size, 0.5f, size * 0.5f), false, objects);
            }

            // Skyscrapers + parking garages
            for (int i = 0; i < buildingsPerCityChunk; i++)
            {
                Vector3 pos = chunkOrigin + new Vector3(
                    Random.Range(40f, size - 40f),
                    0f,
                    Random.Range(40f, size - 40f)
                );

                // Avoid placing directly on roads (simple)
                if (IsNearRoad(pos, chunkOrigin, size)) continue;

                GameObject prefab = (i % 7 == 0 && parkingGaragePrefab != null)
                    ? parkingGaragePrefab
                    : skyscraperPrefabs[Random.Range(0, skyscraperPrefabs.Length)];

                if (prefab == null) continue;

                var go = Instantiate(prefab, pos, Quaternion.identity, chunk.transform);
                go.transform.localScale = new Vector3(1f, Random.Range(0.7f, 1.8f), 1f);
                objects.Add(go);
            }

            _spawned[chunk.Coord] = objects;
        }

        private void SpawnRoad(Vector3 pos, bool horizontal, List<GameObject> list)
        {
            if (roadSegmentPrefab == null) return;
            var go = Instantiate(roadSegmentPrefab, pos, horizontal ? Quaternion.identity : Quaternion.Euler(0, 90, 0), transform);
            list.Add(go);
        }

        private bool IsNearRoad(Vector3 pos, Vector3 origin, float size)
        {
            float localX = pos.x - origin.x;
            float localZ = pos.z - origin.z;
            float cell = size / 4f;
            return (localX % cell < 18f || localZ % cell < 18f);
        }

        public void GenerateSuspensionBridge(Vector3 start, Vector3 end)
        {
            if (bridgePylonPrefab == null || bridgeDeckPrefab == null) return;

            Vector3 dir = (end - start).normalized;
            float length = Vector3.Distance(start, end);
            if (length > bridgeMaxSpan) return;

            // Pylons
            Vector3 pylon1 = start + dir * (length * 0.25f);
            Vector3 pylon2 = start + dir * (length * 0.75f);
            Instantiate(bridgePylonPrefab, pylon1, Quaternion.LookRotation(dir));
            Instantiate(bridgePylonPrefab, pylon2, Quaternion.LookRotation(dir));

            // Deck segments
            int segments = Mathf.CeilToInt(length / 25f);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 pos = Vector3.Lerp(start, end, t);
                pos.y += 18f; // deck height
                Instantiate(bridgeDeckPrefab, pos, Quaternion.LookRotation(dir));
            }
        }

        public void ClearChunk(Vector2Int coord)
        {
            if (_spawned.TryGetValue(coord, out var list))
            {
                foreach (var go in list)
                    if (go) Destroy(go);
                _spawned.Remove(coord);
            }
        }
    }
}
