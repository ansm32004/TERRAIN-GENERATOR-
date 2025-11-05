using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

public class PlaneGenerator : MonoBehaviour  // Updated class name
{
    public GameObject tilePrefab; // Assign Plane prefab here
    public Transform player;
    public int tileSize = 10;

    [Header("Height Settings")]  // New fields for heights
    public int heightmapResolution = 33;
    public float heightMultiplier = 0.1f;
    public float tileKmSize = 0.01f;

    [Header("Blending Settings")]  // New for seams
    [Range(0f, 0.5f)] public float overlapPercent = 0.1f; // 10% border overlap
    [Range(0f, 1f)] public float blendDistance = 0.5f; // Lerp factor (0-1)

    private HashSet<Vector3> spawnedTiles = new HashSet<Vector3>();
    private Dictionary<Vector3, GameObject> tileObjects = new Dictionary<Vector3, GameObject>();
    private Dictionary<Vector3, float[,]> cachedHeightmaps = new Dictionary<Vector3, float[,]>(); // Cache for blending consistency

    void Start()
    {
        StartCoroutine(GenerateTile(Vector3.zero)); // Async start
    }

    void Update()
    {
        if (player == null) return;
        Vector3 playerTilePos = GetTilePosition(player.position);
        StartCoroutine(GenerateSurroundingTiles(playerTilePos));
    }

    Vector3 GetTilePosition(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.x / tileSize) * tileSize;
        int z = Mathf.RoundToInt(pos.z / tileSize) * tileSize;
        return new Vector3(x, 0, z);
    }

    IEnumerator GenerateTile(Vector3 center)
    {
        if (spawnedTiles.Contains(center)) yield break;

        GameObject tile = Instantiate(tilePrefab, center, Quaternion.identity);
        var rend = tile.GetComponent<Renderer>();
        if (rend != null) rend.material.color = Random.ColorHSV();
        spawnedTiles.Add(center);
        tileObjects[center] = tile;

        yield return StartCoroutine(FetchAndApplyHeights(tile, center)); // Height enhancement

        MeshCollider collider = tile.GetComponent<MeshCollider>();
        if (collider != null) collider.sharedMesh = tile.GetComponent<MeshFilter>().mesh; // Update collision
    }

    IEnumerator FetchAndApplyHeights(GameObject tileObj, Vector3 center)
    {
        Vector3 cacheKey = center;
        if (cachedHeightmaps.ContainsKey(cacheKey))
        {
            ApplyHeightmapToMesh(tileObj, cachedHeightmaps[cacheKey]);
            yield break;
        }

        // Lat/lng calc (keep your origin; e.g., update to local coords)
        const double originLat = 28.6139; // Customize!
        const double originLng = 77.2090;
        const double metersPerDegLat = 111000.0;
        double tileCenterLat = originLat + (center.z / metersPerDegLat);
        double tileCenterLng = originLng + (center.x / metersPerDegLat / Mathf.Cos((float)(originLat * Mathf.Deg2Rad)));

        // Overlap-extended deltas
        double latDelta = (tileKmSize * (1 + 2 * overlapPercent)) / 111.0;
        double lngDelta = latDelta / Mathf.Cos((float)tileCenterLat * Mathf.Deg2Rad);

        // Build larger locations
        string locations = "";
        int fullRes = heightmapResolution + Mathf.RoundToInt(heightmapResolution * 2 * overlapPercent); // Approx overlap
        if (fullRes < 2) fullRes = 2;
        for (int y = 0; y < fullRes; y++)
        {
            for (int x = 0; x < fullRes; x++)
            {
                double lat = tileCenterLat - latDelta + (2 * latDelta * y / (fullRes - 1f));
                double lng = tileCenterLng - lngDelta + (2 * lngDelta * x / (fullRes - 1f));
                locations += string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}|", lat, lng);
            }
        }
        locations = locations.TrimEnd('|');

    // OpenTopoData fetch (batch if >100 points; for now, assume low res)
    string url = $"https://api.opentopodata.org/v1/srtm30m?locations={locations}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Use Unity JsonUtility to parse
                string json = request.downloadHandler.text;
                OpenTopoResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<OpenTopoResponse>(json);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Failed to parse OpenTopoData response: " + ex.Message + "\nJSON:\n" + json);
                }

                if (response != null && response.results != null && response.results.Length == fullRes * fullRes)
                {
                    float[,] fullHeightmap = new float[fullRes, fullRes];
                    float minHeight = float.MaxValue;
                    for (int idx = 0; idx < response.results.Length; idx++)
                    {
                        int x = idx % fullRes;
                        int y = idx / fullRes;
                        fullHeightmap[x, y] = response.results[idx].elevation;
                        if (fullHeightmap[x, y] < minHeight) minHeight = fullHeightmap[x, y];
                    }

                    // Normalize full
                    for (int y = 0; y < fullRes; y++)
                        for (int x = 0; x < fullRes; x++)
                            fullHeightmap[x, y] -= minHeight;

                    // Crop to core + blend
                    float[,] coreHeightmap = new float[heightmapResolution, heightmapResolution];
                    int overlapOffset = Mathf.RoundToInt(fullRes * overlapPercent);
                    if (overlapOffset < 0) overlapOffset = 0;
                    // Ensure offsets fit
                    int maxStart = fullRes - overlapOffset;
                    int coreXIndex = 0;
                    for (int y = overlapOffset; y < fullRes - overlapOffset; y++)
                    {
                        int coreYIndex = 0;
                        for (int x = overlapOffset; x < fullRes - overlapOffset; x++)
                        {
                            if (coreXIndex < heightmapResolution && coreYIndex < heightmapResolution)
                            {
                                coreHeightmap[coreXIndex, coreYIndex] = fullHeightmap[x, y] * heightMultiplier;
                            }
                            coreYIndex++;
                        }
                        coreXIndex++;
                    }

                    // Blend seams with neighbors
                    BlendWithNeighbors(ref coreHeightmap, center);

                    cachedHeightmaps[cacheKey] = coreHeightmap;
                    ApplyHeightmapToMesh(tileObj, coreHeightmap);
                }
                else
                {
                    Debug.LogWarning("OpenTopoData: Incomplete results; using flat. JSON:\n" + request.downloadHandler.text);
                }
            }
            else
            {
                Debug.LogError("Fetch Error: " + request.error);
            }
        }
    }

    private void BlendWithNeighbors(ref float[,] heightmap, Vector3 center)
    {
        // Blend right edge (expand to left/forward/back as needed)
        Vector3 rightCenter = center + Vector3.right * tileSize;
        if (tileObjects.ContainsKey(rightCenter) && cachedHeightmaps.ContainsKey(rightCenter))
        {
            float[,] rightMap = cachedHeightmaps[rightCenter];
            int edgeWidth = Mathf.Max(1, Mathf.RoundToInt(heightmap.GetLength(0) * blendDistance));
            for (int y = 0; y < heightmap.GetLength(1); y++)
            {
                float rightEdgeHeight = rightMap[0, y % rightMap.GetLength(1)]; // Left of neighbor
                for (int x = heightmap.GetLength(0) - edgeWidth; x < heightmap.GetLength(0); x++)
                {
                    float t = (float)(x - (heightmap.GetLength(0) - edgeWidth)) / edgeWidth;
                    heightmap[x, y] = Mathf.Lerp(heightmap[x, y], rightEdgeHeight, t);
                }
            }
        }
        // Add similar blocks for other directions (left, forward, back)
    }

    private void ApplyHeightmapToMesh(GameObject tileObj, float[,] heightmap)
    {
        MeshFilter meshFilter = tileObj.GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        Mesh mesh = new Mesh(); // create fresh mesh to avoid mismatched topology issues

        int resolution = heightmap.GetLength(0);
        Vector3[] vertices = new Vector3[resolution * resolution];
        float scale = tileSize / (resolution - 1f);
        int vertIndex = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // Offset from center
                float localX = x * scale - tileSize / 2f;
                float localZ = y * scale - tileSize / 2f;
                vertices[vertIndex] = new Vector3(localX, heightmap[x, y], localZ);
                vertIndex++;
            }
        }

        // Rebuild triangles for grid
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int triIndex = 0;
        for (int y = 0; y < resolution - 1; y++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int current = x + y * resolution;
                int nextX = current + 1;
                int nextY = current + resolution;
                int nextXY = nextY + 1;

                triangles[triIndex++] = current; triangles[triIndex++] = nextY; triangles[triIndex++] = nextX;
                triangles[triIndex++] = nextX; triangles[triIndex++] = nextY; triangles[triIndex++] = nextXY;
            }
        }

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2((i % resolution) / (float)(resolution - 1), (i / resolution) / (float)(resolution - 1));
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.mesh = mesh;
    }

    IEnumerator GenerateSurroundingTiles(Vector3 center)
    {
        yield return GenerateTile(center); // Center first

        Vector3[] directions = new Vector3[]
        {
            Vector3.forward * tileSize,
            Vector3.back * tileSize,
            Vector3.left * tileSize,
            Vector3.right * tileSize
        };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= 3; i++)
            {
                Vector3 pos = center + dir * i;
                yield return GenerateTile(pos); // Yield for async
            }
        }
    }

    [System.Serializable]
    public class OpenTopoResponse
    {
        public OpenTopoResult[] results;
        public string status;
    }

    [System.Serializable]
    public class OpenTopoResult
    {
        public float elevation;
    }
}