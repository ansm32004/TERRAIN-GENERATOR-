using UnityEngine;
using System.Collections.Generic;

namespace YOLOGRAM
{
    public class TerrainManager : MonoBehaviour
    {
        public GameObject chunkPrefab;
        public int viewDistance = 2;
        public Transform player;
        
        [Header("Performance")]
        [Tooltip("Maximum number of chunks to generate per frame")]
        public int maxChunksPerFrame = 4;
        [Range(2, 5)]
        public int preloadDistance = 3;

        [Header("Colors")]
        public Color centerColor = Color.red;
        public Color innerColor = Color.yellow;
        public Color outerColor = Color.blue;

        private Dictionary<Vector2Int, Chunk> loadedChunks = new();
        private Vector2Int currentChunkCoord;
        private Queue<Vector2Int> generationQueue = new();
        private HashSet<Vector2Int> queuedChunks = new();
        private Dictionary<int, MaterialPropertyBlock> mpbCache = new();

        void Start()
        {
            if (player == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) player = playerObj.transform;
                else
                {
                    Debug.LogError("No Player with 'Player' tag found! Assign manually in Inspector.");
                    return;
                }
            }

            if (chunkPrefab == null)
            {
                Debug.LogError("Chunk prefab is missing! Please assign in Inspector.");
                return;
            }

            // Initialize at player position
            currentChunkCoord = Vector2Int.zero;
            Vector3 centerPos = new Vector3(16f, 50f, 16f); // Half chunk size
            player.position = centerPos;
            
            // Generate initial chunks
            EnqueueSurroundingChunks(currentChunkCoord);
            for (int i = 0; i < maxChunksPerFrame * 2; i++)
            {
                ProcessGenerationQueue();
            }

            // Snap player to ground
            SnapPlayerToTerrain(centerPos);
        }

        void Update()
        {
            if (player == null) return;

            Vector2Int playerChunk = Chunk.WorldToChunkCoord(player.position);
            if (playerChunk != currentChunkCoord)
            {
                currentChunkCoord = playerChunk;
                EnqueueSurroundingChunks(playerChunk);
            }

            ProcessGenerationQueue();
            CleanupDistantChunks();
            UpdateChunkColors();
        }

        private void EnqueueSurroundingChunks(Vector2Int center)
        {
            for (int x = -preloadDistance; x <= preloadDistance; x++)
            {
                for (int z = -preloadDistance; z <= preloadDistance; z++)
                {
                    Vector2Int coord = new Vector2Int(center.x + x, center.y + z);
                    if (!loadedChunks.ContainsKey(coord) && !queuedChunks.Contains(coord))
                    {
                        queuedChunks.Add(coord);
                        generationQueue.Enqueue(coord);
                    }
                }
            }
        }

        private void ProcessGenerationQueue()
        {
            int chunksGenerated = 0;
            while (generationQueue.Count > 0 && chunksGenerated < maxChunksPerFrame)
            {
                Vector2Int coord = generationQueue.Dequeue();
                if (!loadedChunks.ContainsKey(coord))
                {
                    GenerateChunk(coord);
                    chunksGenerated++;
                }
                queuedChunks.Remove(coord);
            }
        }

        private void CleanupDistantChunks()
        {
            List<Vector2Int> toRemove = new List<Vector2Int>();
            foreach (var chunk in loadedChunks)
            {
                if (Vector2.Distance(chunk.Key, currentChunkCoord) > preloadDistance + 2)
                {
                    toRemove.Add(chunk.Key);
                }
            }

            foreach (var coord in toRemove)
            {
                if (loadedChunks.TryGetValue(coord, out Chunk chunk))
                {
                    Destroy(chunk.gameObject);
                    loadedChunks.Remove(coord);
                }
            }
        }

        private void GenerateChunk(Vector2Int coord)
        {
            if (chunkPrefab == null) return;

            GameObject chunkObj = Instantiate(chunkPrefab, transform);
            chunkObj.name = $"Chunk_{coord.x}_{coord.y}";
            
            Chunk chunk = chunkObj.GetComponent<Chunk>();
            if (chunk == null)
            {
                chunk = chunkObj.AddComponent<Chunk>();
            }

            chunk.Generate(coord, this);
            loadedChunks[coord] = chunk;

            float chunkSize = chunk.size;
            chunkObj.transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
        }

        private void UpdateChunkColors()
        {
            foreach (var kv in loadedChunks)
            {
                if (kv.Value == null) continue;
                
                float dist = Vector2.Distance(kv.Key, currentChunkCoord);
                Color color = dist <= 1 ? centerColor : 
                            dist <= 2 ? innerColor : 
                            outerColor;

                var renderer = kv.Value.GetComponent<Renderer>();
                if (renderer != null)
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", color);
                    mpb.SetColor("_BaseColor", color);
                    renderer.SetPropertyBlock(mpb);
                }
            }
        }

        public void SnapPlayerToTerrain(Vector3 abovePos)
        {
            if (player == null) return;
            if (Physics.Raycast(abovePos, Vector3.down, out RaycastHit hit, 100f))
            {
                player.position = hit.point + Vector3.up;
            }
        }
    }
}