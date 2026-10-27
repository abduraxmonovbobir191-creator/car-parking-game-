using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CarParking.World
{
    /// <summary>
    /// Multi-Biome Procedural Terrain Chunk Streamer.
    /// Generates and streams terrain chunks asynchronously using the Job System + Burst.
    /// Supports: City, Amazon Rainforest, Mud, Desert, Ocean and more.
    /// Designed for continental-scale worlds (1500km x 1000km+).
    /// </summary>
    public class ProceduralTerrainChunkStreamer : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 256;          // vertices per side
        [SerializeField] private float chunkWorldSize = 512f; // metres per chunk
        [SerializeField] private int viewRadiusInChunks = 6;
        [SerializeField] private Material terrainMaterial;

        [Header("Noise / Biome")]
        [SerializeField] private float biomeScale = 0.0008f;
        [SerializeField] private float heightScale = 180f;
        [SerializeField] private int seed = 42;

        [Header("References")]
        [SerializeField] private Transform viewer; // usually the car / camera

        private Dictionary<Vector2Int, TerrainChunk> _activeChunks = new();
        private Queue<Vector2Int> _chunksToCreate = new();
        private HashSet<Vector2Int> _pending = new();

        private Vector2Int _currentViewerChunk;
        private float _noiseOffsetX;
        private float _noiseOffsetZ;

        private void Start()
        {
            _noiseOffsetX = seed * 1.37f;
            _noiseOffsetZ = seed * 2.91f;

            if (FloatingOriginSystem.Instance != null)
            {
                FloatingOriginSystem.Instance.OnOriginShifted += OnOriginShifted;
            }

            UpdateViewerChunk();
            RequestChunksAroundViewer();
        }

        private void Update()
        {
            UpdateViewerChunk();
            RequestChunksAroundViewer();
            ProcessChunkQueue();
            UnloadDistantChunks();
        }

        private void UpdateViewerChunk()
        {
            if (viewer == null) return;

            Vector3 pos = viewer.position;
            _currentViewerChunk = new Vector2Int(
                Mathf.FloorToInt(pos.x / chunkWorldSize),
                Mathf.FloorToInt(pos.z / chunkWorldSize)
            );
        }

        private void RequestChunksAroundViewer()
        {
            for (int z = -viewRadiusInChunks; z <= viewRadiusInChunks; z++)
            {
                for (int x = -viewRadiusInChunks; x <= viewRadiusInChunks; x++)
                {
                    Vector2Int coord = new Vector2Int(_currentViewerChunk.x + x, _currentViewerChunk.y + z);

                    if (_activeChunks.ContainsKey(coord) || _pending.Contains(coord))
                        continue;

                    _chunksToCreate.Enqueue(coord);
                    _pending.Add(coord);
                }
            }
        }

        private void ProcessChunkQueue()
        {
            // Process a limited number per frame to keep FPS stable
            int maxPerFrame = 2;
            int processed = 0;

            while (_chunksToCreate.Count > 0 && processed < maxPerFrame)
            {
                Vector2Int coord = _chunksToCreate.Dequeue();
                _pending.Remove(coord);

                if (_activeChunks.ContainsKey(coord)) continue;

                CreateChunk(coord);
                processed++;
            }
        }

        private void CreateChunk(Vector2Int coord)
        {
            GameObject go = new GameObject();
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(coord.x * chunkWorldSize, 0f, coord.y * chunkWorldSize);

            BiomeType biome = DetermineDominantBiome(coord);

            TerrainChunk chunk = go.AddComponent<TerrainChunk>();
            chunk.Initialize(coord, biome, terrainMaterial);

            // Generate heightmap + mesh using Burst job
            GenerateChunkMesh(chunk, coord, biome);

            _activeChunks.Add(coord, chunk);
        }

        private BiomeType DetermineDominantBiome(Vector2Int coord)
        {
            float cx = (coord.x + 0.5f) * chunkWorldSize;
            float cz = (coord.y + 0.5f) * chunkWorldSize;

            float nx = (cx + _noiseOffsetX) * biomeScale;
            float nz = (cz + _noiseOffsetZ) * biomeScale;

            float moisture = Mathf.PerlinNoise(nx * 1.7f, nz * 1.7f);
            float temperature = Mathf.PerlinNoise(nx * 0.9f + 100f, nz * 0.9f + 100f);
            float continental = Mathf.PerlinNoise(nx * 0.4f, nz * 0.4f);

            // Simple biome rules (expandable)
            if (continental < 0.28f) return BiomeType.Ocean;
            if (continental < 0.34f) return BiomeType.Beach;

            if (temperature > 0.65f && moisture < 0.35f) return BiomeType.Desert;
            if (moisture > 0.7f && temperature > 0.45f) return BiomeType.Rainforest;
            if (moisture > 0.55f && temperature < 0.5f) return BiomeType.Mud;
            if (continental > 0.75f && temperature < 0.4f) return BiomeType.Mountain;

            // City placement (rare, based on another noise)
            float cityNoise = Mathf.PerlinNoise(nx * 3.1f + 50f, nz * 3.1f + 50f);
            if (cityNoise > 0.82f && continental > 0.4f && continental < 0.7f)
                return BiomeType.City;

            return BiomeType.Grassland;
        }

        private void GenerateChunkMesh(TerrainChunk chunk, Vector2Int coord, BiomeType biome)
        {
            int res = chunkSize;
            var heights = new NativeArray<float>(res * res, Allocator.TempJob);

            var job = new GenerateHeightmapJob
            {
                Resolution = res,
                WorldSize = chunkWorldSize,
                ChunkCoord = new int2(coord.x, coord.y),
                HeightScale = heightScale,
                NoiseOffset = new float2(_noiseOffsetX, _noiseOffsetZ),
                Biome = (int)biome,
                Heights = heights
            };

            JobHandle handle = job.Schedule(res * res, 64);
            handle.Complete();

            // Build mesh on main thread (can be moved to another job later)
            Mesh mesh = BuildMeshFromHeights(heights, res, chunkWorldSize, biome);
            chunk.ApplyMesh(mesh);

            heights.Dispose();
        }

        private Mesh BuildMeshFromHeights(NativeArray<float> heights, int res, float worldSize, BiomeType biome)
        {
            Vector3[] vertices = new Vector3[res * res];
            Vector2[] uvs = new Vector2[res * res];
            int[] triangles = new int[(res - 1) * (res - 1) * 6];

            float step = worldSize / (res - 1);

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = z * res + x;
                    float h = heights[i];
                    vertices[i] = new Vector3(x * step, h, z * step);
                    uvs[i] = new Vector2(x / (float)(res - 1), z / (float)(res - 1));
                }
            }

            int t = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    int i = z * res + x;
                    triangles[t++] = i;
                    triangles[t++] = i + res;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + res;
                    triangles[t++] = i + res + 1;
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void UnloadDistantChunks()
        {
            List<Vector2Int> toRemove = new();

            foreach (var kvp in _activeChunks)
            {
                int dx = Mathf.Abs(kvp.Key.x - _currentViewerChunk.x);
                int dz = Mathf.Abs(kvp.Key.y - _currentViewerChunk.y);

                if (dx > viewRadiusInChunks + 1 || dz > viewRadiusInChunks + 1)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var coord in toRemove)
            {
                if (_activeChunks.TryGetValue(coord, out TerrainChunk chunk))
                {
                    chunk.Unload();
                    Destroy(chunk.gameObject);
                    _activeChunks.Remove(coord);
                }
            }
        }

        private void OnOriginShifted(Vector3 delta)
        {
            // Chunks are parented under this transform, so they move automatically
            // if this GameObject is a root. Otherwise we may need extra handling.
        }

        private void OnDestroy()
        {
            if (FloatingOriginSystem.Instance != null)
            {
                FloatingOriginSystem.Instance.OnOriginShifted -= OnOriginShifted;
            }
        }
    }

    [BurstCompile]
    public struct GenerateHeightmapJob : IJobParallelFor
    {
        public int Resolution;
        public float WorldSize;
        public int2 ChunkCoord;
        public float HeightScale;
        public float2 NoiseOffset;
        public int Biome;

        public NativeArray<float> Heights;

        public void Execute(int index)
        {
            int x = index % Resolution;
            int z = index / Resolution;

            float step = WorldSize / (Resolution - 1);
            float worldX = ChunkCoord.x * WorldSize + x * step;
            float worldZ = ChunkCoord.y * WorldSize + z * step;

            float nx = (worldX + NoiseOffset.x) * 0.003f;
            float nz = (worldZ + NoiseOffset.y) * 0.003f;

            // Multi-octave noise
            float height = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            for (int o = 0; o < 5; o++)
            {
                height += noise.cnoise(new float2(nx * frequency, nz * frequency)) * amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            // Biome-specific height modification
            switch (Biome)
            {
                case 0: // Ocean
                    height = math.min(height * 0.3f - 25f, -2f);
                    break;
                case 1: // Beach
                    height = height * 0.4f + 1.5f;
                    break;
                case 2: // Desert
                    height = height * 0.7f + math.abs(noise.snoise(new float2(nx * 0.5f, nz * 0.5f))) * 12f;
                    break;
                case 3: // Mud
                    height = height * 0.5f + 3f;
                    break;
                case 4: // Rainforest
                    height = height * 0.8f + 8f;
                    break;
                case 6: // City
                    height = math.max(height * 0.3f + 2f, 1f); // flatter for cities
                    break;
                case 7: // Mountain
                    height = height * 1.8f + 40f;
                    break;
                default:
                    height = height * HeightScale * 0.15f + 5f;
                    break;
            }

            Heights[index] = height;
        }
    }
}
