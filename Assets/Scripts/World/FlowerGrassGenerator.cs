using UnityEngine;
using MineDemo.Blocks;
using System.Collections.Generic;

namespace MineDemo.World
{
    public static class FlowerGrassGenerator
    {
        private static readonly BlockType[] HighStates = { BlockType.Poppy, BlockType.AzureBluet, BlockType.OxeyeDaisy, BlockType.Cornflower };
        private static readonly BlockType[] LowStates = { BlockType.OrangeTulip, BlockType.RedTulip, BlockType.PinkTulip, BlockType.WhiteTulip };

        public static void PlaceChunkFlowersAndGrass(Chunk chunk, int seed)
        {
            for (int x = 0; x < Chunk.Width; x++)
            {
                for (int z = 0; z < Chunk.Depth; z++)
                {
                    int worldX = chunk.chunkX * Chunk.Width + x;
                    int worldZ = chunk.chunkZ * Chunk.Depth + z;
                    
                    BiomeType biome = TerrainGenerator.GetBiome(worldX, worldZ);
                    
                    // Bỏ qua các Biome không có thực vật trên cạn
                    if (biome == BiomeType.RiverLake || biome == BiomeType.FrozenRiverLake) 
                        continue;

                    // Tìm mặt đất để trồng
                    for (int y = Chunk.Height - 1; y >= 0; y--)
                    {
                        BlockType type = chunk.GetBlockLocal(x, y, z);
                        if (type != BlockType.Air && type != BlockType.OakLeaves && type != BlockType.OakLog)
                        {
                            // Đã tìm thấy mặt đất
                            if (type == BlockType.Grass || type == BlockType.GrassSnow || type == BlockType.Sand)
                            {
                                int plantWorldY = y + WorldBounds.MinBuildY + 1;
                                if (plantWorldY < WorldBounds.MinBuildY || plantWorldY >= WorldBounds.MaxBuildY) break;
                                
                                // Kiểm tra xem ô phía trên có trống không
                                if (chunk.GetBlockLocal(x, y + 1, z) != BlockType.Air) break;

                                TryPlaceFlora(chunk, x, y + 1, z, worldX, plantWorldY, worldZ, biome, type, seed);
                            }
                            break;
                        }
                    }
                }
            }
        }

        private static void TryPlaceFlora(Chunk chunk, int localX, int localY, int localZ, int worldX, int worldY, int worldZ, BiomeType biome, BlockType groundBlock, int seed)
        {
            // Các Hash để random
            int rollHash = (worldX * 73856093 ^ worldZ * 19349663 ^ seed) % 1000;
            if (rollHash < 0) rollHash = -rollHash;

            float flowerNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.005f, seed, 555);
            float grassNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, seed, 666);
            bool placed = false;
            _ = placed; // Loại bỏ cảnh báo unused variable

            switch (biome)
            {
                case BiomeType.Plains:
                case BiomeType.Hills:
                case BiomeType.Mountains:
                    if (groundBlock != BlockType.Grass) break;
                    float densityMul = (biome == BiomeType.Hills) ? 0.5f : ((biome == BiomeType.Mountains) ? 0.15f : 1.0f);
                    
                    // 1. Hoa (Tỉ lệ rải rác)
                    if (rollHash < 20 * densityMul)
                    {
                        PlacePlainFlower(chunk, localX, localY, localZ, flowerNoise, rollHash);
                        placed = true;
                    }
                    // 2. Cỏ cao (Hiếm)
                    else if (rollHash < 25 * densityMul) // ~0.5%
                    {
                        if (localY + 1 < Chunk.Height && chunk.GetBlockLocal(localX, localY + 1, localZ) == BlockType.Air)
                        {
                            chunk.SetBlockLocal(localX, localY, localZ, BlockType.TallGrassLower);
                            chunk.SetBlockLocal(localX, localY + 1, localZ, BlockType.TallGrassUpper);
                            placed = true;
                        }
                    }
                    // 3. Cỏ ngắn (Dày)
                    else 
                    {
                        float grassThreshold = (grassNoise < 0.2f) ? 200f : 400f; // Vùng noise thấp thưa cỏ hơn, cao rậm hơn
                        grassThreshold *= densityMul;
                        
                        if (rollHash < grassThreshold)
                        {
                            chunk.SetBlockLocal(localX, localY, localZ, BlockType.ShortGrassPlant);
                            placed = true;
                        }
                    }
                    break;

                case BiomeType.Forest:
                    // Rừng ít cỏ hơn và chỉ có hoa Dandelion
                    if (rollHash < 5) 
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.Dandelion);
                        placed = true;
                    }
                    else if (rollHash < 150)
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.ShortGrassPlant);
                        placed = true;
                    }
                    else if (rollHash < 160) // Fern hiếm trong rừng
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.Fern);
                        placed = true;
                    }
                    break;

                case BiomeType.BirchForest:
                    if (groundBlock != BlockType.Grass) break;
                    // Hạn chế hoa (rất hiếm hoa, chủ yếu là Lily of the valley nếu có, nhưng hiện chưa có nên sẽ dùng hoa cúc trắng hoặc trắng/xanh)
                    if (rollHash < 2) 
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.AzureBluet); // Hoa màu nhạt hợp với Birch
                        placed = true;
                    }
                    else if (rollHash < 120) // Ít cỏ hơn Plains, tương tự Forest
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.ShortGrassPlant);
                        placed = true;
                    }
                    else if (rollHash < 125) // Tall grass
                    {
                        if (localY + 1 < Chunk.Height && chunk.GetBlockLocal(localX, localY + 1, localZ) == BlockType.Air)
                        {
                            chunk.SetBlockLocal(localX, localY, localZ, BlockType.TallGrassLower);
                            chunk.SetBlockLocal(localX, localY + 1, localZ, BlockType.TallGrassUpper);
                            placed = true;
                        }
                    }
                    break;

                case BiomeType.Desert:
                    if (groundBlock != BlockType.Sand) break; // Sa mạc chỉ mọc trên cát
                    
                    if (rollHash < 2) 
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.Dandelion);
                        placed = true;
                    }
                    else if (rollHash < 50)
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.ShortDryGrass);
                        placed = true;
                    }
                    break;

                case BiomeType.SnowyPlains:
                case BiomeType.SnowyMountains:
                    if (groundBlock != BlockType.GrassSnow) break;
                    
                    if (rollHash < 2) // Cực hiếm Dandelion
                    {
                        chunk.SetBlockLocal(localX, localY, localZ, BlockType.Dandelion);
                        placed = true;
                    }
                    break;
            }
        }

        private static void PlacePlainFlower(Chunk chunk, int localX, int localY, int localZ, float flowerNoise, int rollHash)
        {
            // scale noise threshold theo tài liệu: < -0.8
            // noise2D trả về 0..1, -0.8 trong khoảng -1..1 tương đương khoảng 0.1 trong 0..1
            if (flowerNoise < 0.1f) 
            {
                BlockType type = LowStates[rollHash % LowStates.Length];
                chunk.SetBlockLocal(localX, localY, localZ, type);
            }
            else
            {
                if ((rollHash % 100) < 33) 
                {
                    BlockType type = HighStates[rollHash % HighStates.Length];
                    chunk.SetBlockLocal(localX, localY, localZ, type);
                }
                else
                {
                    chunk.SetBlockLocal(localX, localY, localZ, BlockType.Dandelion);
                }
            }
        }
    }
}
