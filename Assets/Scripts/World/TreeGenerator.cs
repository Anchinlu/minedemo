using UnityEngine;
using MineDemo.Blocks;
using System.Collections.Generic;

namespace MineDemo.World
{
    public enum TreeProfile
    {
        Small,
        Medium,
        Large
    }

    public static class TreeGenerator
    {
        public static void GenerateChunkTrees(Chunk chunk, int seed)
        {
            if (!WorldManager.EnableTrees) return;
            
            // Lưu trữ vị trí cây đã sinh trong chunk để kiểm tra khoảng cách
            List<Vector2Int> spawnedTrees = new List<Vector2Int>();

            for (int x = 0; x < Chunk.Width; x++)
            {
                for (int z = 0; z < Chunk.Depth; z++)
                {
                    int worldX = chunk.chunkX * Chunk.Width + x;
                    int worldZ = chunk.chunkZ * Chunk.Depth + z;
                    
                    BiomeType biome = TerrainGenerator.GetBiome(worldX, worldZ);
                    if (biome == BiomeType.RiverLake || biome == BiomeType.FrozenRiverLake ||
                        biome == BiomeType.Desert || biome == BiomeType.SnowyPlains ||
                        biome == BiomeType.SnowyMountains) continue; // Chỉ sinh Oak ở Plains, Forest, Hills, Mountains

                    int hash = (worldX * 3129871 ^ worldZ * 631453 ^ seed) % 100;
                    if (hash < 0) hash = -hash;

                    // Tính khoảng cách đến sông để trồng rừng ven sông
                    float river1 = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, seed, 7);
                    float river2 = WorldGenNoise.Noise2D(worldX, worldZ, 0.0025f, seed, 9);
                    float distToRiver = Mathf.Min(Mathf.Abs(river1 - 0.5f), Mathf.Abs(river2 - 0.5f));
                    
                    // Rừng ven sông: Có khoảng cách an toàn với bờ sông (dist > 0.025) và kéo dài ra xung quanh (dist < 0.07)
                    bool isRiverside = (distToRiver > 0.025f && distToRiver < 0.07f);
                    
                    int treeChance = 0;
                    if (isRiverside)
                    {
                        // Mật độ cao nhưng phân bố tự nhiên theo cụm (dùng noise)
                        float treeCluster = WorldGenNoise.Noise2D(worldX, worldZ, 0.1f, seed, 100);
                        if (treeCluster > 0.35f) treeChance = 12; // Mật độ rất dày
                    }
                    else
                    {
                        // Xác định tỉ lệ mọc cây theo Biome ở các vùng bình thường
                        if (biome == BiomeType.Forest) treeChance = 5; // Dày
                        else if (biome == BiomeType.BirchForest) treeChance = 10; // Rất dày (Nhiều cây)
                        else if (biome == BiomeType.Plains) 
                        {
                            // Vài cây xuất hiện riêng biệt, rải rác thành các điểm nhỏ ở vùng đồng bằng
                            float scattered = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, seed, 101);
                            if (scattered > 0.85f) treeChance = 2; // Lâu lâu mới có 1 bãi cây thưa
                            else treeChance = 0;
                        }
                        else if (biome == BiomeType.Hills) treeChance = 2; // Vừa
                        else if (biome == BiomeType.Mountains) treeChance = 1; // Rất thưa
                    }

                    if (hash < treeChance)
                    {
                        // Khoảng cách tối thiểu 4 block (tức là cách nhau ít nhất 3 block trống)
                        bool tooClose = false;
                        foreach (Vector2Int pos in spawnedTrees)
                        {
                            if (Vector2Int.Distance(new Vector2Int(x, z), pos) < 4f)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) continue;

                        // Chọn ngẫu nhiên kích thước dựa vào hash
                        int profileHash = (worldX * 73856093 ^ worldZ * 19349663 ^ seed) % 100;
                        if (profileHash < 0) profileHash = -profileHash;
                        
                        TreeProfile profile = TreeProfile.Small; // 60%
                        if (profileHash >= 60 && profileHash < 90) profile = TreeProfile.Medium; // 30%
                        else if (profileHash >= 90) profile = TreeProfile.Large; // 10%

                        // Tìm mặt đất để trồng cây
                        for (int y = Chunk.Height - 1; y >= 0; y--)
                        {
                            BlockType type = chunk.GetBlockLocal(x, y, z);
                            if (type == BlockType.Grass)
                            {
                                int rootWorldY = y + WorldBounds.MinBuildY + 1;
                                if (rootWorldY < WorldBounds.MinBuildY || rootWorldY + 10 >= WorldBounds.MaxBuildY) break;
                                
                                if (biome == BiomeType.BirchForest)
                                {
                                    if (profileHash < 15) // 15% cây đổ
                                        GenerateFallenBirch(chunk.worldManager, worldX, rootWorldY, worldZ, seed, chunk.chunkX, chunk.chunkZ);
                                    else
                                        GenerateBirchTree(chunk.worldManager, worldX, rootWorldY, worldZ, seed, chunk.chunkX, chunk.chunkZ);
                                }
                                else
                                {
                                    GenerateOakTree(chunk.worldManager, worldX, rootWorldY, worldZ, seed, profile, chunk.chunkX, chunk.chunkZ);
                                }
                                spawnedTrees.Add(new Vector2Int(x, z)); // Ghi nhận vị trí đã trồng
                                break;
                            }
                            else if (type != BlockType.Air && type != BlockType.OakLeaves)
                            {
                                break; // Nếu chạm phải block cứng khác không phải Grass, hủy cột này.
                            }
                        }
                    }
                }
            }
        }

        private static void GenerateOakTree(WorldManager manager, int rootX, int rootY, int rootZ, int seed, TreeProfile profile, int ownerChunkX, int ownerChunkZ)
        {
            // Tạo biến thể ngẫu nhiên để cây đa dạng
            int variationHash = (rootX * 12345 ^ rootZ * 67890 ^ seed) % 100;
            if (variationHash < 0) variationHash = -variationHash;

            // Thân cây cao từ 6 đến 10 block
            int trunkHeight = 6 + (variationHash % 5); 
            
            // 1. Kiểm tra không gian thân cây (Trunk Space check)
            // Ngăn chặn việc mọc đè lên lá của cây khác hoặc mọc lơ lửng
            if (!CheckTrunkSpace(manager, rootX, rootY, rootZ, trunkHeight)) return;

            // 2. Ghi Thân cây
            for (int y = rootY; y < rootY + trunkHeight; y++)
            {
                manager.SetProceduralBlock(rootX, y, rootZ, BlockType.OakLog, ownerChunkX, ownerChunkZ);
            }

            // 3. Ghi Tán lá (Đa dạng và ngẫu nhiên hơn)
            int leafHeight = 5 + (variationHash % 3); // Tán lá cao 5-7 block
            int leafStartY = rootY + trunkHeight - (3 + (variationHash % 2)); // Bắt đầu cách đỉnh thân 3-4 block

            for (int y = leafStartY; y <= leafStartY + leafHeight; y++)
            {
                int dy = y - leafStartY; // 0 là lớp dưới cùng của tán lá
                
                // Bán kính tán lá phình to ở giữa và hóp lại ở hai đầu một cách ngẫu nhiên
                int radius = 2;
                if (dy == 0) radius = 1 + (variationHash % 2); // Lớp đáy
                else if (dy == leafHeight) radius = 1; // Lớp đỉnh cùng
                else if (dy == leafHeight - 1) radius = 1 + ((variationHash / 10) % 2); 
                else radius = 2 + ((variationHash + dy) % 2); // Lớp giữa có thể phình ra tới radius 3

                for (int x = -radius; x <= radius; x++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        // Cắt tỉa các góc để tạo hình tán lá ngẫu nhiên, không bị vuông vức
                        if (Mathf.Abs(x) == radius && Mathf.Abs(z) == radius)
                        {
                            int leafHash = ((rootX + x) * 11 ^ (rootZ + z) * 13 ^ y * 17) % 100;
                            if (leafHash < 0) leafHash = -leafHash;
                            
                            // Ngẫu nhiên tỉa lá nhiều hơn ở các mép, lớp càng cao hoặc bán kính lớn càng dễ rụng lá góc
                            int threshold = 30 + (dy * 5) + (radius * 10);
                            if (leafHash < threshold) continue; 
                        }

                        // Không ghi đè lá lên phần thân cây
                        if (x == 0 && z == 0 && y < rootY + trunkHeight)
                            continue;

                        BlockType currentType = manager.GetExpectedBlock(rootX + x, y, rootZ + z);
                        if (currentType != BlockType.Air && currentType != BlockType.OakLeaves)
                            continue;

                        manager.SetProceduralBlock(rootX + x, y, rootZ + z, BlockType.OakLeaves, ownerChunkX, ownerChunkZ);
                    }
                }
            }
        }

        private static bool CheckTrunkSpace(WorldManager manager, int rootX, int rootY, int rootZ, int trunkHeight)
        {
            // Kiểm tra xem có đúng là đang mọc trên mặt đất (Grass/Dirt) không
            BlockType ground = manager.GetExpectedBlock(rootX, rootY - 1, rootZ);
            if (ground != BlockType.Grass && ground != BlockType.Dirt) return false;

            // Quét khoảng không gian phía trên để đảm bảo không bị vướng
            for (int y = rootY; y < rootY + trunkHeight + 2; y++)
            {
                BlockType type = manager.GetExpectedBlock(rootX, y, rootZ);
                // Từ chối mọc nếu gặp Lá của cây khác hoặc vật cản (ngăn cây mọc đâm xuyên vào nhau)
                if (type != BlockType.Air)
                    return false;
            }
            return true;
        }

        private static void GenerateBirchTree(WorldManager manager, int rootX, int rootY, int rootZ, int seed, int ownerChunkX, int ownerChunkZ)
        {
            int variationHash = (rootX * 12345 ^ rootZ * 67890 ^ seed) % 100;
            if (variationHash < 0) variationHash = -variationHash;

            // Thân cây cao từ 8 đến 12 block (theo yêu cầu)
            int trunkHeight = 8 + (variationHash % 5); 
            
            if (!CheckTrunkSpace(manager, rootX, rootY, rootZ, trunkHeight)) return;

            // Ghi Thân cây Bạch dương
            for (int y = rootY; y < rootY + trunkHeight; y++)
            {
                manager.SetProceduralBlock(rootX, y, rootZ, BlockType.BirchLog, ownerChunkX, ownerChunkZ);
            }

            // Ghi Tán lá (Đặc trưng Birch: Blob foliage, gọn gàng, ít khuyết)
            int leafHeight = 3 + (variationHash % 2); // Tán lá cao 3-4 block
            int leafStartY = rootY + trunkHeight - (2 + (variationHash % 2));

            for (int y = leafStartY; y <= leafStartY + leafHeight; y++)
            {
                int dy = y - leafStartY;
                
                int radius = 2; // Radius tĩnh của Birch Blob Foliage
                if (dy == leafHeight) radius = 1; // Lớp chóp nhỏ lại

                for (int x = -radius; x <= radius; x++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        // Cắt nhẹ 4 góc để tạo hình Blob tròn
                        if (Mathf.Abs(x) == radius && Mathf.Abs(z) == radius && radius > 1)
                        {
                            int leafHash = ((rootX + x) * 11 ^ (rootZ + z) * 13 ^ y * 17) % 100;
                            if (leafHash < 0) leafHash = -leafHash;
                            if (leafHash < 50) continue; // Tỉa bớt góc 50%
                        }

                        if (x == 0 && z == 0 && y < rootY + trunkHeight)
                            continue;

                        BlockType currentType = manager.GetExpectedBlock(rootX + x, y, rootZ + z);
                        if (currentType != BlockType.Air && currentType != BlockType.BirchLeaves)
                            continue;

                        manager.SetProceduralBlock(rootX + x, y, rootZ + z, BlockType.BirchLeaves, ownerChunkX, ownerChunkZ);
                    }
                }
            }
        }

        private static void GenerateFallenBirch(WorldManager manager, int rootX, int rootY, int rootZ, int seed, int ownerChunkX, int ownerChunkZ)
        {
            int variationHash = (rootX * 98765 ^ rootZ * 43210 ^ seed) % 100;
            if (variationHash < 0) variationHash = -variationHash;

            int logLength = 3 + (variationHash % 4); // 3-6 block gỗ đổ
            int dx = (variationHash % 2 == 0) ? 1 : 0;
            int dz = (variationHash % 2 == 0) ? 0 : 1;
            int sign = (variationHash % 4 >= 2) ? 1 : -1;
            
            dx *= sign;
            dz *= sign;

            for (int i = 0; i < logLength; i++)
            {
                int px = rootX + dx * i;
                int pz = rootZ + dz * i;

                BlockType ground = manager.GetExpectedBlock(px, rootY - 1, pz);
                if (ground != BlockType.Grass && ground != BlockType.Dirt) break; // Nếu khúc cây gác lên chỗ lồi lõm thì dừng

                BlockType current = manager.GetExpectedBlock(px, rootY, pz);
                if (current != BlockType.Air && current != BlockType.TallGrassLower) break; // Kẹt

                manager.SetProceduralBlock(px, rootY, pz, BlockType.BirchLog, ownerChunkX, ownerChunkZ);
            }
        }
    }
}
