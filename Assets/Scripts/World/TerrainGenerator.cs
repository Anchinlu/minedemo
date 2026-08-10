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
        RiverLake,
        Desert,
        SnowyPlains,
        SnowyMountains,
        FrozenRiverLake
    }

    public class TerrainGenerator
    {
        public static int Seed = 12345;
        public const int WaterLevel = WorldBounds.SeaLevel;
        public const int MinBuildY = -250;
        public const int MaxBuildY = 300;

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
                    
                    GenerateColumn(worldX, worldZ, out int surfaceY, out bool isWater, out bool isLake, out BiomeType biome);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (isWater && WorldManager.EnableWater)
                    {
                        for (int y = surfaceY + 1; y <= WaterLevel; y++)
                        {
                            BlockType block = PipelineGetBlock(worldX, y, worldZ, surfaceY, isWater, isLake, biome);
                            bool isValidWater = block == BlockType.WaterSource || (y == WaterLevel && block == BlockType.Ice);
                            UnityEngine.Debug.Assert(isValidWater,
                                         $"Missing water at {worldX},{y},{worldZ}. Got: {block}");
                        }
                    }
#endif

                    for (int worldY = MinBuildY; worldY < MaxBuildY; worldY++)
                    {
                        int yLocal = worldY - MinBuildY;
                        int index = x + width * (yLocal + height * z);

                        BlockType expectedBlock = PipelineGetBlock(worldX, worldY, worldZ, surfaceY, isWater, isLake, biome);

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

            GenerateColumn(worldX, worldZ, out int surfaceY, out bool isWater, out bool isLake, out BiomeType biome);
            return PipelineGetBlock(worldX, worldY, worldZ, surfaceY, isWater, isLake, biome);
        }

        public static BlockType PipelineGetBlock(int worldX, int worldY, int worldZ, int surfaceY, bool isWater, bool isLake, BiomeType biome)
        {
            BlockType expectedBlock = CalculateBlock(worldX, worldY, worldZ, surfaceY, isWater, isLake, biome);
            
            if (WorldManager.EnableCaves)
            {
                // Surface protection: do not carve caves within 8 blocks below surface or water level
                int minCaveDepth = Mathf.Max(surfaceY - 8, WaterLevel - 5);
                if (expectedBlock == BlockType.Stone && worldY < minCaveDepth)
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

            return expectedBlock;
        }

        public static BiomeType GetBiome(int worldX, int worldZ)
        {
            GenerateColumn(worldX, worldZ, out int surfaceY, out bool isWater, out bool isLake, out BiomeType biome);
            return biome;
        }

        private static void GenerateColumn(int worldX, int worldZ, out int surfaceY, out bool isWater, out bool isLake, out BiomeType biome)
        {
            TerrainShapeResult shape = TerrainShapeGenerator.GenerateShape(worldX, worldZ, Seed);
            surfaceY = shape.surfaceY;
            isWater = shape.isWater;
            isLake = shape.isLake;
            biome = BiomeResolver.ResolveBiome(worldX, worldZ, surfaceY, isWater, shape.mountainMask, shape.hillsMask, Seed);
        }

        private static BlockType CalculateBlock(int worldX, int worldY, int worldZ, int surfaceY, bool isWater, bool isLake, BiomeType biome)
        {
            return SurfaceRuleResolver.ResolveBlock(worldX, worldY, worldZ, surfaceY, isWater, isLake, biome, Seed);
        }

        public static void DebugPrintTerrainInfo(int worldX, int worldZ)
        {
            TerrainShapeResult shape = TerrainShapeGenerator.GenerateShape(worldX, worldZ, Seed);
            BiomeType biome = BiomeResolver.ResolveBiome(worldX, worldZ, shape.surfaceY, shape.isWater, shape.mountainMask, shape.hillsMask, Seed);
            BlockType topBlock = PipelineGetBlock(worldX, shape.surfaceY, worldZ, shape.surfaceY, shape.isWater, shape.isLake, biome);

            float t = WorldGenNoise.Noise2D(worldX, worldZ, 0.004f, Seed, 10);
            Debug.Log($"[TerrainDebug] POS X:{worldX} Z:{worldZ} Biome:{biome} SurfY:{shape.surfaceY} isWater:{shape.isWater} isRiver:{shape.isRiver} isLake:{shape.isLake} " +
                      $"Temp:{t:F2} HillsM:{shape.hillsMask:F3} MntM:{shape.mountainMask:F3} TopBlock:{topBlock}");
            Debug.Log($"Height={shape.surfaceY} isWater={shape.isWater} HillsMask={shape.hillsMask:F3} " +
                      $"MountainMask={shape.mountainMask:F3} Final={shape.rawFinalHeight:F2}");
        }
    }
}
