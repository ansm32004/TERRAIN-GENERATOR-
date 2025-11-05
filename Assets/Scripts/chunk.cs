using UnityEngine;

namespace YOLOGRAM
{
    public class Chunk : MonoBehaviour
    {
        public int size = 32; // Units per side
        public float scale = 0.1f; // Noise scale
        public Material groundMat; // Assign green material
        public Material lodLowMat; // Grayscale for far LOD

        [Header("Style")]
        [Tooltip("When true, tiles are plain flat (y=0). When false, Perlin noise creates hills.")]
        public bool flatTiles = true;

        [HideInInspector] public TerrainManager manager; // Reference to avoid FindObjectOfType
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private int lodLevel = 0;
        private Mesh highDetailMesh; // Cache for quick LOD swaps

        // ---------------------------- MAIN GENERATE FUNCTION ----------------------------
        public void Generate(Vector2Int chunkCoord, TerrainManager terrainManager)
        {
            manager = terrainManager;
            name = $"Chunk_{chunkCoord.x}_{chunkCoord.y}";

            // Ensure chunk is placed in world space, not relative to TerrainManager
            transform.SetParent(null);
            transform.position = new Vector3(chunkCoord.x * size, 0, chunkCoord.y * size);

            // Ensure required components exist
            meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            meshCollider = gameObject.GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();

            // Generate the mesh
            highDetailMesh = GenerateHighDetailMesh(chunkCoord);
            meshFilter.sharedMesh = highDetailMesh;
            meshCollider.sharedMesh = highDetailMesh;

            // Apply initial material and LOD
            SetLOD(0);

            // If this is the origin chunk, position the player properly
            if (chunkCoord == Vector2Int.zero && manager != null)
            {
                Vector3 abovePos = transform.position + new Vector3(size / 2f, 50f, size / 2f);
                manager.SnapPlayerToTerrain(abovePos);
            }
        }

        // ---------------------------- MESH GENERATION ----------------------------
        private Mesh GenerateHighDetailMesh(Vector2Int chunkCoord)
        {
            int verticesPerSide = size + 1;
            Vector3[] vertices = new Vector3[verticesPerSide * verticesPerSide];
            int[] triangles = new int[(size * size) * 6];

            for (int z = 0; z < verticesPerSide; z++)
            {
                for (int x = 0; x < verticesPerSide; x++)
                {
                    float localX = x - (size / 2f);
                    float localZ = z - (size / 2f);
                    float height = 0f;

                    if (!flatTiles)
                    {
                        float worldX = chunkCoord.x * size + x;
                        float worldZ = chunkCoord.y * size + z;
                        height = Mathf.PerlinNoise(worldX * scale, worldZ * scale) * 10f;
                        height += Mathf.PerlinNoise(worldX * scale * 0.5f, worldZ * scale * 0.5f) * 3f;
                    }

                    vertices[z * verticesPerSide + x] = new Vector3(localX, height, localZ);
                }
            }

            int triIndex = 0;
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = z * verticesPerSide + x;
                    triangles[triIndex++] = i;
                    triangles[triIndex++] = i + verticesPerSide;
                    triangles[triIndex++] = i + 1;
                    triangles[triIndex++] = i + 1;
                    triangles[triIndex++] = i + verticesPerSide;
                    triangles[triIndex++] = i + verticesPerSide + 1;
                }
            }

            Mesh mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------- LOD MANAGEMENT ----------------------------
        public void SetLOD(int level)
        {
            // Safety checks
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

            if (lodLevel == level || manager == null) return;
            lodLevel = level;

            Material mat = lodLevel == 0 ? groundMat : lodLowMat;
            if (mat == null)
            {
                Debug.LogWarning("Chunk.SetLOD: Missing material for LOD " + lodLevel + " on " + name);
                return;
            }

            if (lodLevel == 1) mat.color = Color.Lerp(Color.green, Color.gray, 0.5f);
            meshRenderer.material = mat;

            if (lodLevel == 1 && highDetailMesh != null)
            {
                Mesh lowMesh = SimplifyMesh(highDetailMesh);
                if (lowMesh != null) meshFilter.mesh = lowMesh;
            }
            else
            {
                meshFilter.mesh = highDetailMesh;
            }
        }

        private Mesh SimplifyMesh(Mesh original)
        {
            // Placeholder for future low-LOD simplification
            return original;
        }

        // ---------------------------- STATIC UTILITY ----------------------------
        public static Vector2Int WorldToChunkCoord(Vector3 pos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(pos.x / 32f),
                Mathf.FloorToInt(pos.z / 32f)
            );
        }
    }
}
