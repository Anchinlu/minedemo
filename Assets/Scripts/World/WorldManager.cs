using UnityEngine;
using MineDemo.Blocks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;

namespace MineDemo.World
{
    public class WorldManager : MonoBehaviour
    {
        public Material chunkMaterial;
        public AtlasData atlasData;
        public Transform player;
        public int viewDistance = 3;
        
        private ConcurrentDictionary<Vector2Int, Chunk> activeChunks = new ConcurrentDictionary<Vector2Int, Chunk>();
        private Queue<Vector2Int> chunksToGenerate = new Queue<Vector2Int>();
        private readonly HashSet<Vector2Int> queuedChunkSet = new HashSet<Vector2Int>();
        
        private Queue<Chunk> chunksToUpdateMesh = new Queue<Chunk>();
        private readonly HashSet<Chunk> queuedMeshSet = new HashSet<Chunk>();
        
        private bool isGenerating = false;
        private Vector2Int currentPlayerChunk;
        
        public ConcurrentDictionary<Vector3Int, BlockType> globalModifications = new ConcurrentDictionary<Vector3Int, BlockType>();
        public ConcurrentDictionary<Vector3Int, byte> globalWaterLevels = new ConcurrentDictionary<Vector3Int, byte>();
        
        // Debug flags
        public static bool EnableTrees = true;
        public static bool EnableShortGrass = false;
        public static bool EnableClouds = false;
        public static bool EnableWaterFlow = false;
        public static bool EnableWater = false;
        public static bool EnableWaterTerrainCarving = false;
        public static bool EnableCaves = false;
        public static bool EnableClimateBiomes = false;
        public static bool EnableWorldGenDiagnostics = false;

        public int worldSeed = -1; // -1 means random seed

        // Quản lý block sinh tự động (chủ yếu là Cây) theo Chunk sở hữu
        public Dictionary<Vector2Int, List<Vector3Int>> chunkProceduralBlocks = new Dictionary<Vector2Int, List<Vector3Int>>();
        // Tra cứu nhanh O(1) an toàn luồng
        public ConcurrentDictionary<Vector3Int, BlockType> globalProceduralBlocks = new ConcurrentDictionary<Vector3Int, BlockType>();

        void Awake()
        {
            if (worldSeed == -1)
            {
                TerrainGenerator.SetSeed(Random.Range(0, 99999999));
            }
            else
            {
                TerrainGenerator.SetSeed(worldSeed);
            }
            Debug.Log($"[WorldManager] World Seed: {TerrainGenerator.Seed}");
            Debug.Log($"[WorldManager] Debug Flags: EnableCaves={EnableCaves}, EnableWater={EnableWater}, EnableWaterCarving={EnableWaterTerrainCarving}, EnableWaterFlow={EnableWaterFlow}, EnableTrees={EnableTrees}, EnableClimateBiomes={EnableClimateBiomes}");
        }

        void Start()
        {
            if (FindFirstObjectByType<MineDemo.UI.WorldDebugOverlay>() == null)
            {
                GameObject debugObj = new GameObject("WorldDebugOverlay");
                debugObj.AddComponent<MineDemo.UI.WorldDebugOverlay>();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (gameObject.GetComponent<WorldGenDebugTools>() == null)
            {
                gameObject.AddComponent<WorldGenDebugTools>();
            }
#endif

            if (MineDemo.Utils.ProfilerLogger.Instance == null)
            {
                GameObject logger = new GameObject("ProfilerLogger");
                logger.AddComponent<MineDemo.Utils.ProfilerLogger>();
            }

            if (player == null)
            {
                var pc = FindFirstObjectByType<MineDemo.Player.PlayerController>();
                if (pc != null) player = pc.transform;
            }

            if (player != null)
            {
                currentPlayerChunk = new Vector2Int(Mathf.FloorToInt(player.position.x / Chunk.Width), Mathf.FloorToInt(player.position.z / Chunk.Depth));
            }
            else 
            {
                currentPlayerChunk = Vector2Int.zero;
            }
            
            UpdateChunks();

            // Đưa người chơi lên độ cao an toàn (70) để chờ Chunk sinh ra
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = new Vector3(Chunk.Width / 2f, 70f, Chunk.Depth / 2f);
                if (cc != null) cc.enabled = true;
            }
        }

        void Update()
        {
            if (player == null) return;
            
            Vector2Int currentChunk = new Vector2Int(Mathf.FloorToInt(player.position.x / Chunk.Width), Mathf.FloorToInt(player.position.z / Chunk.Depth));
            if (currentChunk != currentPlayerChunk)
            {
                currentPlayerChunk = currentChunk;
                UpdateChunks();
            }

            if (!isGenerating && (chunksToGenerate.Count > 0 || chunksToUpdateMesh.Count > 0))
            {
                StartCoroutine(GenerateChunksCoroutine());
            }

            // Lưới an toàn: Nếu người chơi rơi xuống vực (do Chunk chưa load kịp), cứu họ lên trời
            if (player.position.y < -10f)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.position = new Vector3(player.position.x, 150f, player.position.z);
                if (cc != null) cc.enabled = true;
            }
        }

        void UpdateChunks()
        {
            List<Vector2Int> chunksInView = new List<Vector2Int>();

            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector2Int chunkPos = new Vector2Int(currentPlayerChunk.x + x, currentPlayerChunk.y + z);
                    chunksInView.Add(chunkPos);

                    if (!activeChunks.ContainsKey(chunkPos) && queuedChunkSet.Add(chunkPos))
                    {
                        chunksToGenerate.Enqueue(chunkPos);
                    }
                }
            }

            // Unload chunks that are out of view
            List<Vector2Int> chunksToUnload = new List<Vector2Int>();
            foreach (var kvp in activeChunks)
            {
                if (!chunksInView.Contains(kvp.Key))
                {
                    chunksToUnload.Add(kvp.Key);
                }
            }

            foreach (var pos in chunksToUnload)
            {
                if (activeChunks.TryGetValue(pos, out Chunk chunkToDestroy))
                {
                    queuedMeshSet.Remove(chunkToDestroy);
                    Destroy(chunkToDestroy.gameObject);
                }
                activeChunks.TryRemove(pos, out _);
                
                // Cleanup procedural blocks (trees) generated by this chunk
                if (chunkProceduralBlocks.ContainsKey(pos))
                {
                    foreach (var blockPos in chunkProceduralBlocks[pos])
                    {
                        globalProceduralBlocks.TryRemove(blockPos, out _);
                        QueueChunkAndNeighbors(blockPos.x, blockPos.z);
                    }
                    chunkProceduralBlocks.Remove(pos);
                }
            }
        }

        IEnumerator GenerateChunksCoroutine()
        {
            isGenerating = true;
            while (chunksToGenerate.Count > 0 || chunksToUpdateMesh.Count > 0)
            {
                // Ưu tiên Rebuild Mesh cho các chunk cũ để xoá mất ranh giới
                if (chunksToUpdateMesh.Count > 0)
                {
                    Chunk chunkToUpdate = chunksToUpdateMesh.Dequeue();
                    queuedMeshSet.Remove(chunkToUpdate);
                    
                    if (chunkToUpdate != null)
                    {
                        chunkToUpdate.GenerateMesh();
                        yield return null; // Mỗi frame chỉ update 1 chunk
                    }
                    continue;
                }

                if (chunksToGenerate.Count > 0)
                {
                    Vector2Int pos = chunksToGenerate.Dequeue();
                    queuedChunkSet.Remove(pos);
                
                // Tránh tạo lại nếu đã có hoặc player đi quá xa
                if (activeChunks.ContainsKey(pos)) continue;
                if (Mathf.Abs(pos.x - currentPlayerChunk.x) > viewDistance || Mathf.Abs(pos.y - currentPlayerChunk.y) > viewDistance)
                    continue;

                GameObject chunkObj = new GameObject($"Chunk_{pos.x}_{pos.y}");
                chunkObj.transform.parent = transform;
                chunkObj.transform.position = new Vector3(pos.x * Chunk.Width, 0, pos.y * Chunk.Depth);

                Chunk chunk = chunkObj.AddComponent<Chunk>();
                MeshRenderer renderer = chunkObj.GetComponent<MeshRenderer>();

                if (chunkMaterial != null)
                {
                    renderer.material = chunkMaterial;
                }

                chunkObj.layer = LayerMask.NameToLayer("Default"); // Ensure collision
                activeChunks.TryAdd(pos, chunk);
                chunk.Initialize(pos.x, pos.y, this, atlasData);

                yield return new WaitUntil(() => chunk == null || chunk.IsInitialized);

                if (chunk == null) continue; // Chunk bị hủy trong lúc đang tạo

                // Thêm các chunk xung quanh vào hàng đợi update mesh để xoá mặt thừa
                Vector2Int[] neighbors = new Vector2Int[] {
                    new Vector2Int(pos.x + 1, pos.y),
                    new Vector2Int(pos.x - 1, pos.y),
                    new Vector2Int(pos.x, pos.y + 1),
                    new Vector2Int(pos.x, pos.y - 1)
                };

                foreach (var n in neighbors)
                {
                    if (activeChunks.TryGetValue(n, out Chunk neighborChunk))
                    {
                        if (queuedMeshSet.Add(neighborChunk))
                            chunksToUpdateMesh.Enqueue(neighborChunk);
                    }
                }

                yield return null; // Chờ frame tiếp theo để không bị lag (hàng đợi)
                }
            }
            isGenerating = false;
        }

        public void EditBlock(int worldX, int worldY, int worldZ, BlockType newType)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / Chunk.Width);
            int chunkZ = Mathf.FloorToInt((float)worldZ / Chunk.Depth);
            Vector2Int chunkPos = new Vector2Int(chunkX, chunkZ);

            if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                int localX = worldX - chunkX * Chunk.Width;
                int localZ = worldZ - chunkZ * Chunk.Depth;
                chunk.EditBlock(localX, worldY, localZ, newType);
            }
        }
        
        public int WorldToLocalY(int worldY)
        {
            return worldY - WorldBounds.MinBuildY;
        }
        
        public BlockType GetBlockFromWorld(int worldX, int worldY, int worldZ)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, worldZ);
            if (globalModifications.TryGetValue(pos, out BlockType modType))
                return modType;

            int chunkX = Mathf.FloorToInt((float)worldX / Chunk.Width);
            int chunkZ = Mathf.FloorToInt((float)worldZ / Chunk.Depth);
            Vector2Int chunkPos = new Vector2Int(chunkX, chunkZ);

            if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                int localX = worldX - chunkX * Chunk.Width;
                int localZ = worldZ - chunkZ * Chunk.Depth;
                int localY = WorldToLocalY(worldY);
                return chunk.GetBlockLocal(localX, localY, localZ);
            }
            
            // Fallback đồng nhất tuyệt đối khi chunk chưa load
            return TerrainGenerator.GetExpectedBlock(worldX, worldY, worldZ);
        }

        public BlockType GetBlockForPlayerCheck(int worldX, int worldY, int worldZ)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, worldZ);
            if (globalModifications.TryGetValue(pos, out BlockType modType))
                return modType;

            int chunkX = Mathf.FloorToInt((float)worldX / Chunk.Width);
            int chunkZ = Mathf.FloorToInt((float)worldZ / Chunk.Depth);
            Vector2Int chunkPos = new Vector2Int(chunkX, chunkZ);

            if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                int localX = worldX - chunkX * Chunk.Width;
                int localZ = worldZ - chunkZ * Chunk.Depth;
                int localY = WorldToLocalY(worldY);
                return chunk.GetBlockLocal(localX, localY, localZ);
            }
            
            // Fallback chỉ khi chunk chưa load
            return GetExpectedBlock(worldX, worldY, worldZ);
        }

        public BlockType GetExpectedBlock(int worldX, int worldY, int worldZ)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, worldZ);
            if (globalModifications.ContainsKey(pos))
            {
                return globalModifications[pos];
            }
            
            // Tra cứu nhanh trong O(1) thay vì duyệt O(N)
            if (globalProceduralBlocks.TryGetValue(pos, out BlockType procType))
            {
                return procType;
            }

            // Nếu Chunk cha load, dự đoán địa hình bằng TerrainGenerator
            return TerrainGenerator.GetExpectedBlock(worldX, worldY, worldZ);
        }

        public byte GetWaterLevelWorld(int worldX, int worldY, int worldZ)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, worldZ);
            if (globalWaterLevels.TryGetValue(pos, out byte level))
                return level;

            int chunkX = Mathf.FloorToInt((float)worldX / Chunk.Width);
            int chunkZ = Mathf.FloorToInt((float)worldZ / Chunk.Depth);
            Vector2Int chunkPos = new Vector2Int(chunkX, chunkZ);

            if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                int localX = worldX - chunkX * Chunk.Width;
                int localZ = worldZ - chunkZ * Chunk.Depth;
                int localY = WorldToLocalY(worldY);
                return chunk.GetWaterLevelLocal(localX, localY, localZ);
            }
            
            // Nếu là sông chưa load thì mức nước bằng 8 (đầy)
            BlockType expected = GetExpectedBlock(worldX, worldY, worldZ);
            if (expected == BlockType.WaterSource) return 8;

            return 0;
        }

        public void SetGlobalBlock(int worldX, int worldY, int worldZ, BlockType type, byte waterLevel = 0, bool isWaterUpdate = false)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, worldZ);
            globalModifications[pos] = type;
            if (type == BlockType.WaterSource || type == BlockType.WaterFlow)
            {
                globalWaterLevels[pos] = waterLevel;
                if (isWaterUpdate && EnableWaterFlow && WaterManager.Instance != null)
                {
                    WaterManager.Instance.EnqueueWaterUpdate(pos, waterLevel);
                }
            }
            else
            {
                globalWaterLevels.TryRemove(pos, out _); // Xoá level nếu block không còn là nước
            }
            
            int chunkX = Mathf.FloorToInt((float)worldX / Chunk.Width);
            int chunkZ = Mathf.FloorToInt((float)worldZ / Chunk.Depth);
            Vector2Int chunkPos = new Vector2Int(chunkX, chunkZ);

            if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                int localX = worldX - chunkX * Chunk.Width;
                int localZ = worldZ - chunkZ * Chunk.Depth;
                int localY = WorldToLocalY(worldY);
                chunk.SetBlockLocal(localX, localY, localZ, type);
                
                if (type == BlockType.WaterSource || type == BlockType.WaterFlow)
                {
                    chunk.SetWaterLevelLocal(localX, localY, localZ, waterLevel);
                }
                
                QueueChunkAndNeighbors(worldX, worldZ);
            }
        }

        public void SetProceduralBlock(int worldX, int worldY, int worldZ, BlockType type, int ownerChunkX, int ownerChunkZ)
        {
            Vector3Int pos = new Vector3Int(worldX, worldY, worldZ);
            Vector2Int ownerChunk = new Vector2Int(ownerChunkX, ownerChunkZ);

            if (!chunkProceduralBlocks.ContainsKey(ownerChunk))
            {
                chunkProceduralBlocks[ownerChunk] = new List<Vector3Int>();
            }

            chunkProceduralBlocks[ownerChunk].Add(pos);
            globalProceduralBlocks[pos] = type;
            
            int chunkX = Mathf.FloorToInt((float)worldX / Chunk.Width);
            int chunkZ = Mathf.FloorToInt((float)worldZ / Chunk.Depth);
            Vector2Int chunkPos = new Vector2Int(chunkX, chunkZ);

            if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                int localX = worldX - chunkX * Chunk.Width;
                int localZ = worldZ - chunkZ * Chunk.Depth;
                int localY = WorldToLocalY(worldY);
                chunk.SetBlockLocal(localX, localY, localZ, type);
                
                QueueChunkAndNeighbors(worldX, worldZ);
            }
        }

        private void QueueChunkAndNeighbors(int worldX, int worldZ)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / Chunk.Width);
            int chunkZ = Mathf.FloorToInt((float)worldZ / Chunk.Depth);
            
            int localX = worldX - chunkX * Chunk.Width;
            int localZ = worldZ - chunkZ * Chunk.Depth;
            
            QueueChunkIfActive(chunkX, chunkZ);
            
            if (localX == 0) QueueChunkIfActive(chunkX - 1, chunkZ);
            else if (localX == Chunk.Width - 1) QueueChunkIfActive(chunkX + 1, chunkZ);
            
            if (localZ == 0) QueueChunkIfActive(chunkX, chunkZ - 1);
            else if (localZ == Chunk.Depth - 1) QueueChunkIfActive(chunkX, chunkZ + 1);
        }
        
        private void QueueChunkIfActive(int cX, int cZ)
        {
            Vector2Int pos = new Vector2Int(cX, cZ);
            if (activeChunks.TryGetValue(pos, out Chunk chunk) && queuedMeshSet.Add(chunk))
            {
                chunksToUpdateMesh.Enqueue(chunk);
            }
        }
    }
}
