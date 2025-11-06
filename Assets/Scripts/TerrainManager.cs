using UnityEngine;
using System.Collections.Generic;

namespace YOLOGRAM
{
    public class TerrainManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        public GameObject chunkPrefab;
        public Transform player;
        public float chunkSize = 32f;   // match your prefab size
        public int viewRadius = 2;      // how far around player to keep generating

        [Header("Tree Prefabs")]
        public GameObject cylinderTreePrefab; // LOD 0
        public GameObject cubeTreePrefab;     // LOD 1

        [Header("Colors")]
        public Color playerTileColor = Color.white;
        public Color nearTileColor = Color.yellow;
        public Color farTileColor = Color.blue;

        private readonly Dictionary<Vector2Int, Chunk> spawnedChunks = new();
        private Vector2Int currentChunkCoord;
        private Vector2Int lastChunkCoord;

        void Start()
        {
            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null) player = found.transform;
                else
                {
                    Debug.LogError("TerrainManager: No Player found!");
                    enabled = false;
                    return;
                }
            }

            if (chunkPrefab == null)
            {
                Debug.LogError("TerrainManager: Chunk prefab not assigned!");
                enabled = false;
                return;
            }

            currentChunkCoord = WorldToChunkCoord(player.position);
            lastChunkCoord = currentChunkCoord;

            // Generate initial grid
            GenerateChunksAround(currentChunkCoord);
            SnapPlayerToTerrain(player.position + Vector3.up * 50f);
        }

        void Update()
        {
            if (player == null) return;

            currentChunkCoord = WorldToChunkCoord(player.position);

            // Detect chunk transition
            if (currentChunkCoord != lastChunkCoord)
            {
                lastChunkCoord = currentChunkCoord;
                GenerateChunksAround(currentChunkCoord);
            }

            UpdateChunkColors();
        }

        // --------------------------------------------------------------------
        // Generate all missing chunks in a square around the player
        // --------------------------------------------------------------------
        private void GenerateChunksAround(Vector2Int center)
        {
            for (int x = -viewRadius; x <= viewRadius; x++)
            {
                for (int z = -viewRadius; z <= viewRadius; z++)
                {
                    Vector2Int coord = new Vector2Int(center.x + x, center.y + z);

                    if (!spawnedChunks.ContainsKey(coord))
                    {
                        SpawnChunk(coord);
                    }
                }
            }
        }

        private void SpawnChunk(Vector2Int coord)
        {
            Vector3 worldPos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
            GameObject chunkObj = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
            chunkObj.name = $"Chunk_{coord.x}_{coord.y}";

            Chunk chunk = chunkObj.GetComponent<Chunk>();
            if (chunk == null)
                chunk = chunkObj.AddComponent<Chunk>();

            // Pass tree prefabs into chunk before generation
            chunk.treeHighPrefab = cylinderTreePrefab;
            chunk.treeLowPrefab = cubeTreePrefab;

            chunk.Generate(coord, this);
            spawnedChunks[coord] = chunk;
        }

        // --------------------------------------------------------------------
        // Color the chunks dynamically
        // --------------------------------------------------------------------
        private void UpdateChunkColors()
        {
            foreach (var kvp in spawnedChunks)
            {
                Vector2Int coord = kvp.Key;
                Chunk chunk = kvp.Value;
                if (chunk == null) continue;

                float dist = Vector2.Distance(coord, currentChunkCoord);
                Color color = farTileColor;

                if (coord == currentChunkCoord)
                    color = playerTileColor;
                else if (dist <= 1.5f)
                    color = nearTileColor;

                Renderer rend = chunk.GetComponent<Renderer>();
                if (rend == null) continue;

                MaterialPropertyBlock mpb = new();
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", color);
                mpb.SetColor("_Color", color);
                rend.SetPropertyBlock(mpb);

                // LOD switching to drive tree LOD and chunk materials/mesh
                int lod = (coord == currentChunkCoord || dist <= 1.5f) ? 0 : 1;
                chunk.SetLOD(lod);
            }
        }

        // --------------------------------------------------------------------
        // Convert world position → chunk coordinates
        // --------------------------------------------------------------------
        public Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt(worldPos.x / chunkSize);
            int z = Mathf.FloorToInt(worldPos.z / chunkSize);
            return new Vector2Int(x, z);
        }

        // --------------------------------------------------------------------
        // Keeps player aligned to ground (optional)
        // --------------------------------------------------------------------
        public void SnapPlayerToTerrain(Vector3 abovePos)
        {
            if (player == null) return;
            if (Physics.Raycast(abovePos, Vector3.down, out RaycastHit hit, 200f))
            {
                player.position = hit.point + Vector3.up;
            }
        }
    }
}
