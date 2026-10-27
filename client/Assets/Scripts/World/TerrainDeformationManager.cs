using System.Collections.Generic;
using UnityEngine;

namespace CarParking.World
{
    /// <summary>
    /// Manages real-time deformable mud, sand and beach surfaces.
    /// Uses Compute Shader + RenderTexture heightmaps for persistent ruts.
    /// Integrates with FloatingOriginSystem and TerrainChunk.
    /// </summary>
    public class TerrainDeformationManager : MonoBehaviour
    {
        public static TerrainDeformationManager Instance { get; private set; }

        [Header("Compute")]
        [SerializeField] private ComputeShader deformationCompute;
        [SerializeField] private int textureResolution = 512;

        [Header("Deformation Settings")]
        [SerializeField] private float maxSinkDepth = 0.45f;
        [SerializeField] private float mudViscosity = 2.8f;
        [SerializeField] private float sandResistance = 1.6f;
        [SerializeField] private float recoverySpeed = 0.015f;

        private Dictionary<Vector2Int, RenderTexture> _deformMaps = new();
        private int _kernelDeform;
        private int _kernelRecover;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (deformationCompute != null)
            {
                _kernelDeform = deformationCompute.FindKernel("CSDeform");
                _kernelRecover = deformationCompute.FindKernel("CSRecover");
            }

            if (FloatingOriginSystem.Instance != null)
                FloatingOriginSystem.Instance.OnOriginShifted += OnOriginShifted;
        }

        public void RegisterChunk(TerrainChunk chunk)
        {
            if (chunk.DominantBiome != BiomeType.Mud &&
                chunk.DominantBiome != BiomeType.Desert &&
                chunk.DominantBiome != BiomeType.Beach)
                return;

            if (_deformMaps.ContainsKey(chunk.Coord)) return;

            var rt = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();

            // Clear to zero displacement
            ClearRenderTexture(rt);
            _deformMaps.Add(chunk.Coord, rt);
        }

        public void UnregisterChunk(Vector2Int coord)
        {
            if (_deformMaps.TryGetValue(coord, out var rt))
            {
                rt.Release();
                _deformMaps.Remove(coord);
            }
        }

        /// <summary>
        /// Called by vehicle wheels every FixedUpdate when on deformable surface.
        /// </summary>
        public void ApplyWheelDeformation(Vector3 worldPos, float wheelRadius, float vehicleMass, float wheelSpin, BiomeType biome)
        {
            Vector2Int chunkCoord = WorldToChunkCoord(worldPos);
            if (!_deformMaps.TryGetValue(chunkCoord, out var rt)) return;

            float strength = biome switch
            {
                BiomeType.Mud => mudViscosity * (vehicleMass / 1500f) * (0.4f + Mathf.Abs(wheelSpin) * 0.6f),
                BiomeType.Desert => sandResistance * (vehicleMass / 1800f),
                BiomeType.Beach => 1.1f * (vehicleMass / 1600f),
                _ => 1f
            };

            strength = Mathf.Clamp(strength, 0.05f, maxSinkDepth);

            if (deformationCompute == null) return;

            // Convert world pos to UV inside the chunk
            Vector2 uv = WorldToChunkUV(worldPos, chunkCoord);

            deformationCompute.SetTexture(_kernelDeform, "Result", rt);
            deformationCompute.SetFloats("WheelUV", uv.x, uv.y);
            deformationCompute.SetFloat("Strength", strength);
            deformationCompute.SetFloat("Radius", wheelRadius / 512f); // approximate
            deformationCompute.SetFloat("DeltaTime", Time.fixedDeltaTime);

            int groups = Mathf.CeilToInt(textureResolution / 8f);
            deformationCompute.Dispatch(_kernelDeform, groups, groups, 1);
        }

        public float GetSinkDepth(Vector3 worldPos, BiomeType biome)
        {
            // Simplified query - in full implementation sample the RenderTexture
            return biome switch
            {
                BiomeType.Mud => 0.22f,
                BiomeType.Desert => 0.12f,
                BiomeType.Beach => 0.08f,
                _ => 0f
            };
        }

        private void Update()
        {
            // Slow recovery of ruts
            if (deformationCompute == null) return;

            foreach (var kvp in _deformMaps)
            {
                deformationCompute.SetTexture(_kernelRecover, "Result", kvp.Value);
                deformationCompute.SetFloat("Recovery", recoverySpeed * Time.deltaTime);
                int groups = Mathf.CeilToInt(textureResolution / 8f);
                deformationCompute.Dispatch(_kernelRecover, groups, groups, 1);
            }
        }

        private void OnOriginShifted(Vector3 delta)
        {
            // Deformation maps are local to chunks; chunks themselves move with origin.
            // No extra work needed if chunks are parented correctly.
        }

        private Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            // Assumes same chunkWorldSize as streamer (512)
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / 512f),
                Mathf.FloorToInt(worldPos.z / 512f)
            );
        }

        private Vector2 WorldToChunkUV(Vector3 worldPos, Vector2Int chunkCoord)
        {
            float localX = worldPos.x - chunkCoord.x * 512f;
            float localZ = worldPos.z - chunkCoord.y * 512f;
            return new Vector2(localX / 512f, localZ / 512f);
        }

        private void ClearRenderTexture(RenderTexture rt)
        {
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = null;
        }

        private void OnDestroy()
        {
            foreach (var rt in _deformMaps.Values)
                rt.Release();
            _deformMaps.Clear();

            if (FloatingOriginSystem.Instance != null)
                FloatingOriginSystem.Instance.OnOriginShifted -= OnOriginShifted;
        }
    }
}
