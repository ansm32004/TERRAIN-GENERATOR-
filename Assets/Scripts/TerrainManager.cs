using UnityEngine;
using System.Collections.Generic;

namespace YOLOGRAM
{
    public class TerrainManager : MonoBehaviour
    {
        [Header("Chunk Setup")]
        public GameObject chunkPrefab;
        public Transform player;
        public float chunkSize = 32f;  // Must match Chunk.size in your prefab
        public int rowWidth = 3;       // Always 3 chunks wide (center + left + right)
        public float spawnDistance = 50f; // How far ahead to spawn next row

        [Header("Colors")]
        public Color playerTileColor = Color.white;
        public Color newRowColor = Color.yellow;
        public Color oldRowColor = Color.blue;

        private int nextRowIndexZ = 0;
        private int lastGeneratedRowZ = int.MinValue;

        // Keep track of all spawned chunks
        private readonly Dictionary<Vector2Int, Chunk> spawnedChunks = new();

        void Start()
        {
            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null) player = found.transform;
                else
                {
                    Debug.LogError("No player assigned and no GameObject with 'Player' tag found!");
                    enabled = false;
                    return;
                }
            }

            if (chunkPrefab == null)
            {
                Debug.LogError("Chunk prefab is missing!");
                enabled = false;
                return;
            }

            // Initialize grid index based on player's Z position
            nextRowIndexZ = Mathf.FloorToInt(player.position.z / chunkSize);

            // Generate initial safe area (two rows)
            GenerateRow();
            GenerateRow();

            // Snap player on top of terrain to prevent falling
            SnapPlayerToTerrain(player.position + Vector3.up * 50f);
        }

        void Update()
        {
            Vector2Int playerGrid = new Vector2Int(
                Mathf.RoundToInt(player.position.x / chunkSize),
                Mathf.RoundToInt(player.position.z / chunkSize)
            );

            // Spawn next row if player gets close to the edge
            float nextRowWorldZ = nextRowIndexZ * chunkSize;
            if (player.position.z + spawnDistance > nextRowWorldZ)
            {
                GenerateRow();
            }

            UpdateChunkColors(playerGrid);
        }

        // --------------------------------------------------------------------
        // Generates a new 3×1 row of chunks ahead of the player
        // --------------------------------------------------------------------
        private void GenerateRow()
        {
            int centerX = Mathf.RoundToInt(player.position.x / chunkSize);
            int halfWidth = rowWidth / 2; // =1 for 3-wide

            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                int tileX = centerX + dx;
                int tileZ = nextRowIndexZ;

                Vector2Int coord = new Vector2Int(tileX, tileZ);
                if (!spawnedChunks.ContainsKey(coord))
                {
                    // Instantiate new chunk
                    GameObject chunkObj = Instantiate(chunkPrefab, null);
                    chunkObj.name = $"Chunk_{coord.x}_{coord.y}";

                    Chunk chunk = chunkObj.GetComponent<Chunk>();
                    if (chunk == null)
                        chunk = chunkObj.AddComponent<Chunk>();

                    chunk.Generate(coord, this);
                    spawnedChunks[coord] = chunk;
                }
            }

            lastGeneratedRowZ = nextRowIndexZ;
            nextRowIndexZ += 1; // Move forward one row
        }

        // --------------------------------------------------------------------
        // Update colors of chunks dynamically
        // --------------------------------------------------------------------
        private void UpdateChunkColors(Vector2Int playerChunk)
        {
            foreach (var kvp in spawnedChunks)
            {
                Vector2Int coord = kvp.Key;
                Chunk chunk = kvp.Value;
                if (chunk == null) continue;

                Color targetColor = oldRowColor;
                if (coord == playerChunk)
                    targetColor = playerTileColor;
                else if (coord.y == lastGeneratedRowZ)
                    targetColor = newRowColor;

                Renderer rend = chunk.GetComponent<Renderer>();
                if (rend == null) continue;

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", targetColor);
                mpb.SetColor("_Color", targetColor);
                rend.SetPropertyBlock(mpb);
            }
        }

        // --------------------------------------------------------------------
        // Keeps player positioned on top of terrain
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
