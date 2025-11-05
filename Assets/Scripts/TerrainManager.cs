using UnityEngine;
using System.Collections.Generic;

namespace YOLOGRAM
{
    public class TerrainManager : MonoBehaviour
    {
        [Header("Chunk Setup")]
        public GameObject chunkPrefab;
        public Transform player;

        [Tooltip("How many chunks ahead of the player to generate.")]
        public int lookahead = 4; // how far ahead to make chunks
        [Tooltip("Half-width (in chunks) of the strip generated at each lookahead step. 1 => 3-wide strip.")]
        public int stripHalfWidth = 1;

        [Header("Performance")]
        [Tooltip("Max chunks to instantiate per frame")]
        public int maxChunksPerFrame = 6;

        [Header("Colors")]
        public Color centerColor = Color.red;
        public Color innerColor = Color.yellow;
        public Color outerColor = Color.blue;
        public Color playerTileColor = Color.white;

        // Persistent storage of generated chunks
        private Dictionary<Vector2Int, Chunk> loadedChunks = new();
        private Queue<Vector2Int> generationQueue = new();
        private HashSet<Vector2Int> queuedChunks = new();

        // Tracking
        private Vector2Int currentChunkCoord = Vector2Int.zero;
        private Vector2Int lastChunkCoord = Vector2Int.zero;
        private Vector3 lastPlayerPosition;

        void Start()
        {
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
                else
                {
                    Debug.LogError("TerrainManager: No Player with 'Player' tag found. Assign in inspector.");
                    enabled = false;
                    return;
                }
            }

            if (chunkPrefab == null)
            {
                Debug.LogError("TerrainManager: chunkPrefab not assigned.");
                enabled = false;
                return;
            }

            // initial coords
            currentChunkCoord = Chunk.WorldToChunkCoord(player.position);
            lastChunkCoord = currentChunkCoord;
            lastPlayerPosition = player.position;

            // Ensure a small starting area exists
            EnqueueAreaAround(currentChunkCoord, 1);
            ProcessGenerationQueue(); // generate a few immediately
            ProcessGenerationQueue();
            ProcessGenerationQueue();
        }

        void Update()
        {
            if (player == null) return;

            Vector2Int playerChunk = Chunk.WorldToChunkCoord(player.position);
            currentChunkCoord = playerChunk;

            // If player moved chunk, update last positions
            if (playerChunk != lastChunkCoord)
            {
                lastChunkCoord = playerChunk;
            }

            // Determine forward direction to generate ahead:
            // Prefer actual movement (delta position). If nearly zero, fallback to player.forward.
            Vector3 movementDelta = player.position - lastPlayerPosition;
            Vector2 dir2;
            if (movementDelta.magnitude > 0.01f)
            {
                dir2 = new Vector2(movementDelta.x, movementDelta.z).normalized;
            }
            else
            {
                Vector3 f = player.forward;
                dir2 = new Vector2(f.x, f.z).normalized;
                if (dir2.magnitude < 0.01f) dir2 = Vector2.up; // fallback north
            }

            lastPlayerPosition = player.position;

            // Convert continuous direction into discrete chunk direction (dx,dz in {-1,0,1})
            int dx = Mathf.Clamp(Mathf.RoundToInt(dir2.x), -1, 1);
            int dz = Mathf.Clamp(Mathf.RoundToInt(dir2.y), -1, 1);

            // If both are zero (rare), force forward to north
            if (dx == 0 && dz == 0)
            {
                dz = 1;
            }

            // Enqueue the forward strip based on lookahead & stripHalfWidth.
            EnqueueForwardStrip(currentChunkCoord, dx, dz, lookahead, stripHalfWidth);

            // Always keep the immediate surrounding ring so player isn't standing on missing neighbors
            EnqueueAreaAround(currentChunkCoord, 1);

            // Generate at most maxChunksPerFrame this frame
            ProcessGenerationQueue();

            // Update colors; player tile -> white
            UpdateChunkColors();
        }

        // Enqueue a rectangular strip in front of the player.
        // For steps from 1..lookahead, at each step place a cross-section perpendicular to (dx,dz) with given half-width.
        private void EnqueueForwardStrip(Vector2Int origin, int dx, int dz, int lookaheadSteps, int halfWidth)
        {
            // Perpendicular vector in chunk-space: perp = (-dz, dx) (rotated 90deg)
            int px = -dz;
            int pz = dx;

            for (int step = 1; step <= lookaheadSteps; step++)
            {
                Vector2Int center = new Vector2Int(origin.x + dx * step, origin.y + dz * step);

                for (int w = -halfWidth; w <= halfWidth; w++)
                {
                    Vector2Int coord = new Vector2Int(center.x + px * w, center.y + pz * w);
                    EnqueueIfMissing(coord);
                }
            }
        }

        // Enqueue a small square area (radius) around a center
        private void EnqueueAreaAround(Vector2Int center, int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    EnqueueIfMissing(new Vector2Int(center.x + x, center.y + z));
                }
            }
        }

        private void EnqueueIfMissing(Vector2Int coord)
        {
            if (loadedChunks.ContainsKey(coord)) return;
            if (queuedChunks.Contains(coord)) return;
            queuedChunks.Add(coord);
            generationQueue.Enqueue(coord);
        }

        private void ProcessGenerationQueue()
        {
            int generated = 0;
            while (generationQueue.Count > 0 && generated < maxChunksPerFrame)
            {
                Vector2Int coord = generationQueue.Dequeue();
                queuedChunks.Remove(coord);

                if (loadedChunks.ContainsKey(coord)) continue;

                GenerateChunk(coord);
                generated++;
            }
        }

        private void GenerateChunk(Vector2Int coord)
        {
            GameObject chunkObj = Instantiate(chunkPrefab, null); // world-space
            chunkObj.name = $"Chunk_{coord.x}_{coord.y}";

            Chunk chunk = chunkObj.GetComponent<Chunk>();
            if (chunk == null) chunk = chunkObj.AddComponent<Chunk>();

            chunk.Generate(coord, this);
            loadedChunks[coord] = chunk;
        }

        private void UpdateChunkColors()
        {
            Vector2Int playerChunk = Chunk.WorldToChunkCoord(player.position);
            foreach (var kv in loadedChunks)
            {
                if (kv.Value == null) continue;

                float dist = Vector2.Distance(kv.Key, currentChunkCoord);
                Color color;
                if (kv.Key == playerChunk)
                    color = playerTileColor;
                else if (dist <= 0.5f)
                    color = centerColor;
                else if (dist <= 1.5f)
                    color = innerColor;
                else
                    color = outerColor;

                var renderer = kv.Value.GetComponent<Renderer>();
                if (renderer == null) continue;

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", color);
                mpb.SetColor("_Color", color);
                renderer.SetPropertyBlock(mpb);
            }
        }

        // Optional: snap player to terrain surface
        public void SnapPlayerToTerrain(Vector3 abovePos)
        {
            if (player == null) return;
            if (Physics.Raycast(abovePos, Vector3.down, out RaycastHit hit, 200f))
            {
                player.position = hit.point + Vector3.up;
            }
        }

        // Optional debug helper
        public bool IsChunkLoaded(Vector2Int coord) => loadedChunks.ContainsKey(coord);
    }
}
