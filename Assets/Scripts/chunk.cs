using UnityEngine;

namespace YOLOGRAM
{
    public class Chunk : MonoBehaviour
    {
    public int size = 32; // Units per side.
    public float scale = 0.1f; // Noise scale.
    public Material groundMat; // Assign green material.
    public Material lodLowMat; // Grayscale for far LOD.
    [Header("Style")]
    [Tooltip("When true, tiles are plain flat (y=0). When false, Perlin noise creates hills.")]
    public bool flatTiles = true;

    [HideInInspector] public TerrainManager manager; // Reference to avoid FindObjectOfType.
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private int lodLevel = 0;
    private Mesh highDetailMesh; // Cache for quick LOD swaps.

    public void Generate(Vector2Int chunkCoord, TerrainManager terrainManager)
    {
        if (manager == null) manager = terrainManager; // Set reference once.

        name = $"Chunk_{chunkCoord.x}_{chunkCoord.y}";
        transform.position = new Vector3(chunkCoord.x * size, 0, chunkCoord.y * size);
        transform.SetParent(manager.transform); // Ensure hierarchy.

        // Add components if missing.
        meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();

        // Generate high-detail mesh once.
        highDetailMesh = GenerateHighDetailMesh(chunkCoord);
        meshFilter.mesh = highDetailMesh;
        meshCollider.sharedMesh = highDetailMesh;

        // Apply material with initial LOD=0.
        SetLOD(0);

        // Snap player if starting chunk.
        if (chunkCoord == Vector2Int.zero && manager != null) manager.SnapPlayerToTerrain(transform.position + new Vector3(size / 2f, 50f, size / 2f));
    }

    private Mesh GenerateHighDetailMesh(Vector2Int chunkCoord)
    {
        int verticesPerSide = size + 1;
        Vector3[] vertices = new Vector3[verticesPerSide * verticesPerSide];
        int[] triangles = new int[(size * size) * 6];

        // Seamless heights with global coords.
        for (int z = 0; z < verticesPerSide; z++)
        {
            for (int x = 0; x < verticesPerSide; x++)
            {
                // Local coordinates centered on chunk so the mesh is centered on its GameObject
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

        // Triangles.
        int triIndex = 0;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = z * verticesPerSide + x;
                triangles[triIndex++] = i; triangles[triIndex++] = i + verticesPerSide; triangles[triIndex++] = i + 1;
                triangles[triIndex++] = i + 1; triangles[triIndex++] = i + verticesPerSide; triangles[triIndex++] = i + verticesPerSide + 1;
            }
        }

        Mesh mesh = new Mesh { vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public void SetLOD(int level)
    {
        // Ensure required components are present before changing LOD
        if (meshRenderer == null) meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshFilter == null) meshFilter = gameObject.GetComponent<MeshFilter>();

        if (lodLevel == level || manager == null) return;
        lodLevel = level;

        // Quick swap: Just material (for color tint). For true low-res, generate simplified mesh here.
        Material mat = lodLevel == 0 ? groundMat : lodLowMat;
        if (mat == null)
        {
            Debug.LogWarning("Chunk.SetLOD: material is missing for LOD " + lodLevel + " on " + name);
        }
        else
        {
            if (lodLevel == 1) mat.color = Color.Lerp(Color.green, Color.gray, 0.5f);
            if (meshRenderer != null) meshRenderer.material = mat;
        }

        // Optional: Simplify mesh for LOD 1 (half verts, but keep collider full for player).
        if (lodLevel == 1 && highDetailMesh != null)
        {
            Mesh lowMesh = SimplifyMesh(highDetailMesh); // Implement if needed; placeholder.
            if (lowMesh != null) meshFilter.mesh = lowMesh;
        }
        else
        {
            meshFilter.mesh = highDetailMesh;
        }
    }

    private Mesh SimplifyMesh(Mesh original) // Placeholder for low-LOD mesh.
    {
        // Basic: Subsample vertices (e.g., every other). Expand for full impl.
        return original; // Temp: No change until needed.
    }

    public static Vector2Int WorldToChunkCoord(Vector3 pos) => new Vector2Int(Mathf.FloorToInt(pos.x / 32f), Mathf.FloorToInt(pos.z / 32f));
    }
}