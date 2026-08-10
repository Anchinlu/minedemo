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

            for (int x = 0; x < Chunk.Width; x++)
            {
                for (int z = 0; z < Chunk.Depth; z++)
                {
                    int worldX = chunk.chunkX * Chunk.Width + x;
                    int worldZ = chunk.chunkZ * Chunk.Depth + z;
                    
                    BiomeType biome = TerrainGenerator.GetBiome(worldX, worldZ);
                    if (biome == BiomeType.RiverLake) continue; // Không sinh cây ở sông/hồ

                    int hash = (worldX * 3129871 ^ worldZ * 631453 ^ seed) % 100;
                    if (hash < 0) hash = -hash;

                    // Xác định tỉ lệ mọc cây theo Biome
                    int treeChance = 0;
                    if (biome == BiomeType.Forest) treeChance = 5; // Dày
                    else if (biome == BiomeType.Plains) treeChance = 1; // Thưa
                    else if (biome == BiomeType.Hills) treeChance = 2; // Vừa
                    else if (biome == BiomeType.Mountains) treeChance = 1; // Rất thưa

                    if (hash < treeChance)
                    {
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
                                
                                GenerateOakTree(chunk.worldManager, worldX, rootWorldY, worldZ, seed, profile, chunk.chunkX, chunk.chunkZ);
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
            int trunkHeight = 4;
            int canopyRadius = 2;
            int canopyHeight = 3;

            // Tính toán biến thể phụ để cây có độ cao chênh lệch trong cùng một Profile
            int variationHash = (rootX * 12345 ^ rootZ * 67890 ^ seed) % 10;
            if (variationHash < 0) variationHash = -variationHash;

            if (profile == TreeProfile.Small)
            {
                trunkHeight = 4 + (variationHash % 2); // 4-5
                canopyRadius = 2;
                canopyHeight = 3;
            }
            else if (profile == TreeProfile.Medium)
            {
                trunkHeight = 5 + (variationHash % 3); // 5-7
                canopyRadius = 2 + (variationHash % 2); // 2-3
                canopyHeight = 4;
            }
            else if (profile == TreeProfile.Large)
            {
                trunkHeight = 7 + (variationHash % 4); // 7-10
                canopyRadius = 3 + (variationHash % 2); // 3-4
                canopyHeight = 5;
            }

            int canopyCenterY = rootY + trunkHeight - 1;

            int canopyStartY = canopyCenterY - (canopyHeight / 2);
            int canopyEndY = canopyStartY + canopyHeight - 1;

            // Xác định đỉnh của thân cây, đảm bảo thân luôn thấp hơn đỉnh tán lá ít nhất 1 block
            int trunkTopY = canopyCenterY + 1;
            if (trunkTopY >= canopyEndY) 
            {
                trunkTopY = canopyEndY - 1;
            }

            // 1. Kiểm tra không gian thân cây (Trunk Space check)
            if (!CheckTrunkSpace(manager, rootX, rootY, rootZ, trunkHeight))
                return; // Huỷ lệnh sinh nếu thân bị vướng (vào đá, thân cây khác, v.v.)

            // 2. Ghi Thân (Từ mặt đất đâm xuyên lên ngang giữa tán)
            for (int y = rootY; y <= trunkTopY; y++)
            {
                manager.SetProceduralBlock(rootX, y, rootZ, BlockType.OakLog, ownerChunkX, ownerChunkZ);
            }

            // 3. Ghi Tán lá
            for (int y = canopyStartY; y <= canopyEndY; y++)
            {
                int currentRadius = GetCanopyRadius(y, canopyCenterY, canopyRadius, profile);

                for (int x = -currentRadius; x <= currentRadius; x++)
                {
                    for (int z = -currentRadius; z <= currentRadius; z++)
                    {
                        // Xoá các khối góc để tán bo tròn hơn
                        if (Mathf.Abs(x) == currentRadius && Mathf.Abs(z) == currentRadius)
                        {
                            // Tỉa ngẫu nhiên một số lá ở mép để tán trông tự nhiên, lồi lõm hơn
                            int leafHash = ((rootX + x) * 11 ^ (rootZ + z) * 13 ^ y * 17) % 100;
                            if (leafHash < 0) leafHash = -leafHash;
                            if (leafHash < 50) continue; 
                        }

                        // Không ghi đè lá lên thân cây
                        if (x == 0 && z == 0 && y <= trunkTopY)
                            continue;

                        // Tán lá bị vướng núi hoặc block khác -> bỏ qua ô đó, không ghi đè, không huỷ cả cây
                        BlockType currentType = manager.GetExpectedBlock(rootX + x, y, rootZ + z);
                        if (currentType != BlockType.Air && currentType != BlockType.OakLeaves)
                            continue;

                        // Ghi dữ liệu tán lá 
                        manager.SetProceduralBlock(rootX + x, y, rootZ + z, BlockType.OakLeaves, ownerChunkX, ownerChunkZ);
                    }
                }
            }
        }

        private static int GetCanopyRadius(int y, int centerY, int maxRadius, TreeProfile profile)
        {
            if (y == centerY) return maxRadius;
            if (y < centerY) return Mathf.Max(1, maxRadius - 1); // Lớp đáy
            if (y > centerY) return Mathf.Max(1, maxRadius - (y - centerY)); // Các lớp chóp hóp lại dần
            return 1;
        }

        private static bool CheckTrunkSpace(WorldManager manager, int rootX, int rootY, int rootZ, int trunkHeight)
        {
            for (int y = rootY; y < rootY + trunkHeight; y++)
            {
                BlockType type = manager.GetExpectedBlock(rootX, y, rootZ);
                // Thân cây chỉ mọc xuyên qua Không khí hoặc Lá cây (lá sẽ bị đè)
                if (type != BlockType.Air && type != BlockType.OakLeaves)
                    return false;
            }
            return true;
        }
    }
}
