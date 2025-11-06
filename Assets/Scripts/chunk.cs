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

        [Header("Trees")]
        [Tooltip("High detail tree prefab (cylinder)")]
        public GameObject treeHighPrefab;
        [Tooltip("Low detail tree prefab (cube)")]
        public GameObject treeLowPrefab;
        [Range(0, 100)] public int minTrees = 5;
        [Range(0, 100)] public int maxTrees = 15;
        [Tooltip("Padding from chunk edges to avoid overlap")] public float edgePadding = 1f;
        [Header("Density")]
        [Range(0f, 3f)] public float treeDensity = 1f; // 1 = default, <1 fewer, >1 more
        private System.Collections.Generic.List<Tree> spawnedTrees = new System.Collections.Generic.List<Tree>();

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

            // Spawn trees after mesh and collider are ready
            SpawnTrees(chunkCoord);
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

            // Propagate LOD to trees
            UpdateTreeLOD(lodLevel);
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

        // ---------------------------- TREE SPAWNING ----------------------------
        private void SpawnTrees(Vector2Int chunkCoord)
        {
            // Clear any previous trees (e.g., regen path)
            if (spawnedTrees != null && spawnedTrees.Count > 0)
            {
                for (int i = 0; i < spawnedTrees.Count; i++)
                {
                    if (spawnedTrees[i] != null)
                    {
                        Destroy(spawnedTrees[i].gameObject);
                    }
                }
                spawnedTrees.Clear();
            }

            if (treeHighPrefab == null || treeLowPrefab == null) return;

            // Deterministic seed from chunk coordinates
            int seed = (chunkCoord.x * 73856093) ^ (chunkCoord.y * 19349663);
            Random.InitState(seed);

            int baseCount = Random.Range(minTrees, maxTrees + 1);
            int count = Mathf.Max(0, Mathf.RoundToInt(baseCount * treeDensity));

            float halfSize = size * 0.5f;
            float minXZ = -halfSize + edgePadding;
            float maxXZ = halfSize - edgePadding;

            for (int i = 0; i < count; i++)
            {
                float localX = Random.Range(minXZ, maxXZ);
                float localZ = Random.Range(minXZ, maxXZ);

                // Raycast from above to terrain to get height
                Vector3 rayOrigin = transform.TransformPoint(new Vector3(localX, 100f, localZ));
                Vector3 spawnPos = transform.TransformPoint(new Vector3(localX, 0f, localZ));
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 500f))
                {
                    spawnPos = hit.point;
                }

                float scaleJitter = Random.Range(0.8f, 1.2f);
                float yRot = Random.Range(0f, 360f);

                GameObject treeObj = new GameObject($"Tree_{i}");
                treeObj.transform.SetParent(transform, false);
                treeObj.transform.position = spawnPos;

                Tree tree = treeObj.AddComponent<Tree>();
                tree.Initialize(treeHighPrefab, treeLowPrefab, lodLevel, scaleJitter, yRot);

                spawnedTrees.Add(tree);
            }
        }

        private void UpdateTreeLOD(int level)
        {
            if (spawnedTrees == null) return;
            for (int i = 0; i < spawnedTrees.Count; i++)
            {
                if (spawnedTrees[i] != null)
                {
                    spawnedTrees[i].SetLOD(level);
                }
            }
        }
    }
}
