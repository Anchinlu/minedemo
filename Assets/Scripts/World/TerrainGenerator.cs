using UnityEngine;
using MineDemo.Blocks;

namespace MineDemo.World
{
    public enum BiomeType
    {
        Plains,
        Forest,
        Hills,
        Mountains,
        RiverLake
    }

    public static class TerrainGenerator
    {
        private const int Seed = 12345;
        private const int WaterLevel = WorldBounds.SeaLevel; // 62
        private const int MinBuildY = WorldBounds.MinBuildY;
        private const int MaxBuildY = WorldBounds.MaxBuildY;

        public static void GenerateChunkData(int chunkX, int chunkZ, int width, int height, int depth, out BlockType[] blocks, out byte[] waterLevels, out int minOccupiedLocalY, out int maxOccupiedLocalY)
        {
            blocks = new BlockType[width * height * depth];
            waterLevels = new byte[width * height * depth];
            minOccupiedLocalY = height - 1;
            maxOccupiedLocalY = 0;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int worldX = chunkX * width + x;
                    int worldZ = chunkZ * depth + z;
                    
                    GenerateColumn(worldX, worldZ, out int surfaceY, out bool isWater, out BiomeType biome);

                    // Cave carving variables
                    // float caveScale = 0.03f; // Tạm tắt cave

                    for (int worldY = MinBuildY; worldY < MaxBuildY; worldY++)
                    {
                        int yLocal = worldY - MinBuildY;
                        int index = x + width * (yLocal + height * z);

                        BlockType expectedBlock = CalculateBlock(worldX, worldY, worldZ, surfaceY, isWater, biome);
                        
                        // TẠM TẮT CAVE ĐỂ TEST ĐÁY SÔNG/HỒ
                        /*
                        if (expectedBlock == BlockType.Stone && worldY < surfaceY - 10) // Không đào 10 block sát mặt đất
                        {
                            float depthFactor = Mathf.Clamp01((float)(surfaceY - worldY) / 150f);
                            float dynThreshold = Mathf.Lerp(0.7f, 0.5f, depthFactor); // Giảm nhẹ threshold ở dưới sâu

                            float caveNoise = Noise3D.FBM3D(worldX * caveScale, worldY * caveScale, worldZ * caveScale, 3, 0.5f, 2f);
                            if (caveNoise > dynThreshold)
                            {
                                // Aquifer
                                if (worldY <= WaterLevel - 10) 
                                {
                                    expectedBlock = BlockType.WaterSource;
                                    waterLevels[index] = 8;
                                }
                                else
                                {
                                    expectedBlock = BlockType.Air;
                                }
                            }
                        }
                        */

                        blocks[index] = expectedBlock;
                        if (expectedBlock != BlockType.Air)
                        {
                            if (yLocal < minOccupiedLocalY) minOccupiedLocalY = yLocal;
                            if (yLocal > maxOccupiedLocalY) maxOccupiedLocalY = yLocal;
                        }

                        if (expectedBlock == BlockType.WaterSource && waterLevels[index] == 0)
                        {
                            waterLevels[index] = 8;
                        }
                    }
                }
            }

            // Bảo đảm Bedrock luôn nằm trong phạm vi (MinBuildY -> 0)
            minOccupiedLocalY = 0;
            
            if (minOccupiedLocalY > maxOccupiedLocalY)
            {
                minOccupiedLocalY = 0;
                maxOccupiedLocalY = 0;
                UnityEngine.Debug.LogWarning($"[TerrainGenerator] Chunk {chunkX},{chunkZ} hoàn toàn rỗng ngoài dự kiến, fallback bounds 0..0.");
            }
        }

        public static BlockType GetExpectedBlock(int worldX, int worldY, int worldZ)
        {
            if (worldY < MinBuildY || worldY >= MaxBuildY) return BlockType.Air;

            GenerateColumn(worldX, worldZ, out int surfaceY, out bool isWater, out BiomeType biome);
            BlockType block = CalculateBlock(worldX, worldY, worldZ, surfaceY, isWater, biome);
            
            if (WorldManager.EnableCaves)
            {
                if (block == BlockType.Stone && worldY < surfaceY - 10)
                {
                    float caveScale = 0.03f;
                    float depthFactor = Mathf.Clamp01((float)(surfaceY - worldY) / 150f);
                    float dynThreshold = Mathf.Lerp(0.7f, 0.5f, depthFactor);

                    float caveNoise = Noise3D.FBM3D(worldX * caveScale, worldY * caveScale, worldZ * caveScale, 3, 0.5f, 2f);
                    if (caveNoise > dynThreshold)
                    {
                        if (worldY <= WaterLevel - 10) return BlockType.WaterSource;
                        return BlockType.Air;
                    }
                }
            }

            return block;
        }

        public static BiomeType GetBiome(int worldX, int worldZ)
        {
            GenerateColumn(worldX, worldZ, out int surfaceY, out bool isWater, out BiomeType biome);
            return biome;
        }

        private static void GenerateColumn(int worldX, int worldZ, out int surfaceY, out bool isWater, out BiomeType biome)
        {
            float cont = Mathf.PerlinNoise(worldX * 0.001f + Seed, worldZ * 0.001f + Seed);
            float erosion = Mathf.PerlinNoise(worldX * 0.002f + Seed * 2, worldZ * 0.002f + Seed * 2);
            float peaks = Mathf.PerlinNoise(worldX * 0.005f + Seed * 3, worldZ * 0.005f + Seed * 3);
            float detail = Mathf.PerlinNoise(worldX * 0.03f + Seed * 4, worldZ * 0.03f + Seed * 4);
            
            float temp = Mathf.PerlinNoise(worldX * 0.004f + Seed * 5, worldZ * 0.004f + Seed * 5);
            float humid = Mathf.PerlinNoise(worldX * 0.004f + Seed * 6, worldZ * 0.004f + Seed * 6);

            // Pipeline: Height calculation
            float baseHeight = 62f + (cont - 0.5f) * 40f; 
            
            // Apply erosion (flatter terrain vs steep)
            float erosionFactor = Mathf.Lerp(1.0f, 0.2f, erosion);
            
            // Apply peaks
            float peakHeight = Mathf.Pow(peaks, 2f) * 120f * erosionFactor;
            
            float rawHeight = baseHeight + peakHeight + (detail - 0.5f) * 5f;

            // Rivers (Ridge noise)
            isWater = false;
            float riverNoiseScale = 0.005f;
            float riverNoise = Mathf.PerlinNoise((worldX + Seed * 7) * riverNoiseScale, (worldZ + Seed * 7) * riverNoiseScale);
            float ridge = Mathf.Abs(riverNoise - 0.5f) * 2f; 
            
            // Smooth step based on distance to center
            float riverMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.05f, ridge)); // 1 tại tâm, 0 tại bờ
            
            float riverDepthNoise = Mathf.PerlinNoise((worldX + Seed * 8) * 0.02f, (worldZ + Seed * 8) * 0.02f);
            float maxRiverDepth = Mathf.Lerp(6f, 14f, riverDepthNoise);
            float riverDepth = riverMask * maxRiverDepth;
            float riverSurface = rawHeight - riverDepth;
            float bankBlend = Mathf.SmoothStep(0f, 1f, riverMask);
            
            rawHeight = Mathf.Lerp(rawHeight, riverSurface, bankBlend);

            // Lakes
            float lakeNoiseScale = 0.01f;
            float lakeNoise = Mathf.PerlinNoise((worldX + Seed + 999) * lakeNoiseScale, (worldZ + Seed + 999) * lakeNoiseScale);
            float lakeMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.8f, lakeNoise));
            
            float lakeDepth = lakeMask * 16f;
            float lakeSurface = rawHeight - lakeDepth;
            rawHeight = Mathf.Lerp(rawHeight, lakeSurface, lakeMask);

            int terrainHeight = Mathf.Clamp(Mathf.RoundToInt(rawHeight), 40, MaxBuildY - 20);

            if (terrainHeight < WaterLevel) isWater = true;
            surfaceY = terrainHeight;

            // Biome determination
            if (isWater) biome = BiomeType.RiverLake;
            else if (surfaceY > 120) biome = BiomeType.Mountains;
            else if (surfaceY > 80 || erosion < 0.4f) biome = BiomeType.Hills;
            else if (humid > 0.55f && temp > 0.4f) biome = BiomeType.Forest;
            else biome = BiomeType.Plains;
        }

        private static BlockType CalculateBlock(int worldX, int worldY, int worldZ, int surfaceY, bool isWater, BiomeType biome)
        {
            if (worldY == MinBuildY) return BlockType.Bedrock;
            
            int dirtThickness = 4;
            if (biome == BiomeType.Mountains) dirtThickness = 1;
            else if (biome == BiomeType.Hills) dirtThickness = 2;

            if (worldY < surfaceY - dirtThickness)
            {
                return BlockType.Stone;
            }
            else if (worldY < surfaceY)
            {
                if (biome == BiomeType.RiverLake || isWater || surfaceY <= WaterLevel + 1) 
                {
                    // Scale nhiễu nhỏ đi để tạo mảng lớn, không bị vụn vặt
                    float bedNoise = Mathf.PerlinNoise(worldX * 0.05f + Seed, worldZ * 0.05f + Seed);
                    if (worldY <= surfaceY - 2) 
                    {
                        return bedNoise > 0.4f ? BlockType.Stone : BlockType.Dirt;
                    }
                    // Tập trung nhiều đất hơn cát ở lớp ngay dưới bề mặt
                    return bedNoise > 0.3f ? BlockType.Dirt : BlockType.Sand;
                }
                return BlockType.Dirt;
            }
            else if (worldY == surfaceY)
            {
                if (isWater || biome == BiomeType.RiverLake || surfaceY <= WaterLevel + 1) 
                {
                    // Scale nhiễu 0.05 tạo ra các mảng Cát/Cỏ phân chia rõ ràng, to bản ở mép sông
                    float bankNoise = Mathf.PerlinNoise(worldX * 0.05f + Seed, worldZ * 0.05f + Seed);
                    if (surfaceY < WaterLevel) // Đáy sông
                    {
                        // Nhiều đất (60%), ít cát (30%), hiếm đá (10%)
                        if (bankNoise > 0.4f) return BlockType.Dirt;
                        if (bankNoise > 0.1f) return BlockType.Sand;
                        return BlockType.Stone;
                    }
                    else // Bờ sông (chỉ sát mép nước)
                    {
                        return bankNoise > 0.5f ? BlockType.Grass : BlockType.Sand;
                    }
                }
                if (biome == BiomeType.Mountains && surfaceY > 180) return BlockType.Stone; // High peaks
                return BlockType.Grass;
            }
            else if (isWater && worldY <= WaterLevel)
            {
                if (WorldManager.EnableWater)
                    return BlockType.WaterSource;
                else
                    return BlockType.Air; // Terrain khô
            }

            return BlockType.Air;
        }

        public static void DebugPrintTerrainInfo(int worldX, int worldZ)
        {
            float cont = Mathf.PerlinNoise(worldX * 0.001f + Seed, worldZ * 0.001f + Seed);
            float baseHeight = 62f + (cont - 0.5f) * 40f; 

            float riverNoiseScale = 0.005f;
            float riverNoise = Mathf.PerlinNoise((worldX + Seed * 7) * riverNoiseScale, (worldZ + Seed * 7) * riverNoiseScale);
            float ridge = Mathf.Abs(riverNoise - 0.5f) * 2f; 
            float riverMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.05f, ridge)); // 0 ở lòng sông, 1 ở mép/xa sông
            
            float riverDepthNoise = Mathf.PerlinNoise((worldX + Seed * 8) * 0.02f, (worldZ + Seed * 8) * 0.02f);
            float maxRiverDepth = Mathf.Lerp(6f, 14f, riverDepthNoise);
            float riverDepth = (1f - riverMask) * maxRiverDepth;

            float lakeNoiseScale = 0.01f;
            float lakeNoise = Mathf.PerlinNoise((worldX + Seed + 999) * lakeNoiseScale, (worldZ + Seed + 999) * lakeNoiseScale);
            float lakeMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.8f, lakeNoise)); // 0 khi < 0.6, 1 khi > 0.8
            float lakeDepth = lakeMask * 16f;

            float bedNoise = Mathf.PerlinNoise(worldX * 0.05f + Seed, worldZ * 0.05f + Seed);
            float bankNoise = Mathf.PerlinNoise(worldX * 0.05f + Seed, worldZ * 0.05f + Seed);

            Debug.Log($"[TerrainDebug] POS X:{worldX} Z:{worldZ}\n" +
                      $"-- LAKES: Noise={lakeNoise:F3}, Mask={lakeMask:F3}, Depth={lakeDepth:F1}\n" +
                      $"-- RIVERS: Ridge={ridge:F3}, Mask={riverMask:F3}, Depth={riverDepth:F1}\n" +
                      $"-- MATERIALS: BedNoise={bedNoise:F3}, BankNoise={bankNoise:F3}");
        }
    }
}
