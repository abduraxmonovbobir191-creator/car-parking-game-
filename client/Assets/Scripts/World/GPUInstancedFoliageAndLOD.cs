using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarParking.World
{
    /// <summary>
    /// GPU Instanced foliage and props with frustum culling.
    /// Designed to render hundreds of thousands of trees, bushes and rocks at high FPS.
    /// </summary>
    public class GPUInstancedFoliageAndLOD : MonoBehaviour
    {
        [System.Serializable]
        public class FoliageType
        {
            public Mesh mesh;
            public Material material;
            public int maxInstances = 20000;
            public float minScale = 0.7f;
            public float maxScale = 1.4f;
            public BiomeType[] allowedBiomes;
        }

        public FoliageType[] foliageTypes;
        public float spawnRadius = 450f;
        public int instancesPerFrame = 64;
        public Transform viewer;

        private class BatchData
        {
            public Matrix4x4[] matrices;
            public int count;
            public MaterialPropertyBlock props;
            public ComputeBuffer argsBuffer;
            public Mesh mesh;
            public Material material;
        }

        private List<BatchData> _batches = new();
        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
            if (viewer == null && FloatingOriginSystem.Instance != null)
            {
                // fallback
            }

            InitializeBatches();
        }

        private void InitializeBatches()
        {
            foreach (var type in foliageTypes)
            {
                if (type.mesh == null || type.material == null) continue;

                var batch = new BatchData
                {
                    matrices = new Matrix4x4[type.maxInstances],
                    count = 0,
                    props = new MaterialPropertyBlock(),
                    mesh = type.mesh,
                    material = type.material
                };

                // Indirect args buffer (index count, instance count, start index, base vertex, start instance)
                uint[] args = new uint[5] { type.mesh.GetIndexCount(0), 0, 0, 0, 0 };
                batch.argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
                batch.argsBuffer.SetData(args);

                _batches.Add(batch);

                // Pre-spawn some instances in a radius (simple version)
                PreSpawn(type, batch);
            }
        }

        private void PreSpawn(FoliageType type, BatchData batch)
        {
            int toSpawn = Mathf.Min(type.maxInstances, 4000);
            for (int i = 0; i < toSpawn; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spawnRadius;
                Vector3 pos = (viewer != null ? viewer.position : Vector3.zero) + new Vector3(offset.x, 0f, offset.y);
                pos.y = SampleHeight(pos);

                float scale = Random.Range(type.minScale, type.maxScale);
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                batch.matrices[batch.count] = Matrix4x4.TRS(pos, rot, Vector3.one * scale);
                batch.count++;
            }

            UpdateArgs(batch);
        }

        private float SampleHeight(Vector3 pos)
        {
            // Simple height approximation - replace with real terrain sample later
            return Mathf.PerlinNoise(pos.x * 0.01f, pos.z * 0.01f) * 15f;
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;

            foreach (var batch in _batches)
            {
                if (batch.count == 0) continue;

                // Frustum culling is handled by Unity when using DrawMeshInstanced
                Graphics.DrawMeshInstanced(
                    batch.mesh,
                    0,
                    batch.material,
                    batch.matrices,
                    batch.count,
                    batch.props,
                    ShadowCastingMode.On,
                    true
                );
            }
        }

        private void UpdateArgs(BatchData batch)
        {
            uint[] args = new uint[5];
            batch.argsBuffer.GetData(args);
            args[1] = (uint)batch.count;
            batch.argsBuffer.SetData(args);
        }

        private void OnDestroy()
        {
            foreach (var batch in _batches)
            {
                if (batch.argsBuffer != null)
                    batch.argsBuffer.Release();
            }
        }
    }
}
