using UnityEngine;

namespace CarParking.World
{
    /// <summary>
    /// Represents a single streamed terrain chunk.
    /// Holds heightmap, splatmap, biome data and the generated mesh/collider.
    /// </summary>
    public class TerrainChunk : MonoBehaviour
    {
        public Vector2Int Coord { get; private set; }
        public BiomeType DominantBiome { get; private set; }
        public bool IsReady { get; private set; }

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private Mesh _mesh;

        public void Initialize(Vector2Int coord, BiomeType biome, Material terrainMaterial)
        {
            Coord = coord;
            DominantBiome = biome;
            IsReady = false;

            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshCollider = gameObject.AddComponent<MeshCollider>();

            _meshRenderer.sharedMaterial = terrainMaterial;
            gameObject.name = $"Chunk_{coord.x}_{coord.y}_{biome}";
        }

        public void ApplyMesh(Mesh mesh)
        {
            _mesh = mesh;
            _meshFilter.sharedMesh = mesh;
            _meshCollider.sharedMesh = mesh;
            IsReady = true;
        }

        public void Unload()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
            IsReady = false;
        }

        private void OnDestroy()
        {
            Unload();
        }
    }
}
