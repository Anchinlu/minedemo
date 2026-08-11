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

    public enum WorldGenMode
    {
        LegacyHeightmap,
        Density
    }

    public class TerrainGenerator
    {
        private static int _seed = 12345;
        public static int Seed 
        { 
            get => _seed; 
        }

        public static void SetSeed(int newSeed)
        {
            _seed = newSeed;
            if (CurrentMode == WorldGenMode.Density)
            {
                columnCache.Clear();
            }
        }

        public const int WaterLevel = WorldBounds.SeaLevel;
        public const int MinBuildY = WorldBounds.MinBuildY;
        public const int MaxBuildY = WorldBounds.MaxBuildY;
        
        public static WorldGenMode CurrentMode = WorldGenMode.Density;

        public static void GenerateChunkData(int chunkX, int chunkZ, int width, int height, int depth, out BlockType[] blocks, out byte[] waterLevels, out int minOccupiedLocalY, out int maxOccupiedLocalY)
        {
            if (CurrentMode == WorldGenMode.Density)
            {
                WorldGenContext context = new WorldGenContext(Seed, MinBuildY, MaxBuildY, WaterLevel);
                DensityChunkSampler.GenerateChunkData(chunkX, chunkZ, width, height, depth, context, out blocks, out waterLevels, out minOccupiedLocalY, out maxOccupiedLocalY);
                return;
            }

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

        // Cache for columns in Density Mode
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<UnityEngine.Vector2Int, WorldColumn> columnCache 
            = new System.Collections.Concurrent.ConcurrentDictionary<UnityEngine.Vector2Int, WorldColumn>();

        public static WorldColumn GetWorldColumn(int worldX, int worldZ)
        {
            UnityEngine.Vector2Int pos = new UnityEngine.Vector2Int(worldX, worldZ);
            if (columnCache.TryGetValue(pos, out WorldColumn col))
            {
                return col;
            }

            WorldGenContext context = new WorldGenContext(Seed, MinBuildY, MaxBuildY, WaterLevel);
            NoiseSample noise = NoiseRouter.Sample2D(worldX, worldZ, context);
            int surfaceY = DensityRouter.GetBaseSurfaceY(worldX, worldZ, context, noise);

            // Compute neighbors to find slope
            int nY = DensityRouter.GetBaseSurfaceY(worldX, worldZ + 1, context, NoiseRouter.Sample2D(worldX, worldZ + 1, context));
            int sY = DensityRouter.GetBaseSurfaceY(worldX, worldZ - 1, context, NoiseRouter.Sample2D(worldX, worldZ - 1, context));
            int eY = DensityRouter.GetBaseSurfaceY(worldX + 1, worldZ, context, NoiseRouter.Sample2D(worldX + 1, worldZ, context));
            int wY = DensityRouter.GetBaseSurfaceY(worldX - 1, worldZ, context, NoiseRouter.Sample2D(worldX - 1, worldZ, context));

            float slope = UnityEngine.Mathf.Max(
                UnityEngine.Mathf.Abs(surfaceY - nY),
                UnityEngine.Mathf.Abs(surfaceY - sY),
                UnityEngine.Mathf.Abs(surfaceY - eY),
                UnityEngine.Mathf.Abs(surfaceY - wY));

            col = new WorldColumn
            {
                noise = noise,
                surfaceY = surfaceY,
                slope = slope
            };

            col.mountainZone = MountainZoneResolver.ResolveZone(col);
            col.biome = BiomeResolver.ResolveBiome(col);

            columnCache.TryAdd(pos, col);
            return col;
        }

        public static BlockType GetExpectedBlock(int worldX, int worldY, int worldZ)
        {
            if (worldY < MinBuildY || worldY >= MaxBuildY) return BlockType.Air;

            if (CurrentMode == WorldGenMode.Density)
            {
                WorldColumn col = GetWorldColumn(worldX, worldZ);
                bool isSolid = worldY <= col.surfaceY;
                WorldGenContext context = new WorldGenContext(Seed, MinBuildY, MaxBuildY, WaterLevel);
                return SurfaceRuleResolver.ResolveBlock(worldX, worldY, worldZ, col, isSolid, context);
            }

            GenerateColumn(worldX, worldZ, out int surface, out bool isWater, out bool isLake, out BiomeType biome);
            return PipelineGetBlock(worldX, worldY, worldZ, surface, isWater, isLake, biome);
        }

        public static BiomeType GetBiome(int worldX, int worldZ)
        {
            if (CurrentMode == WorldGenMode.Density)
            {
                return GetWorldColumn(worldX, worldZ).biome;
            }

            GenerateColumn(worldX, worldZ, out _, out _, out _, out BiomeType biome);
            return biome;
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



        private static void GenerateColumn(int worldX, int worldZ, out int surfaceY, out bool isWater, out bool isLake, out BiomeType biome)
        {
            TerrainShapeResult shape = TerrainShapeGenerator.GenerateShape(worldX, worldZ, Seed);
            surfaceY = shape.surfaceY;
            isWater = shape.isWater;
            isLake = shape.isLake;
            biome = BiomeResolver.ResolveBiome(shape);
        }

        private static BlockType CalculateBlock(int worldX, int worldY, int worldZ, int surfaceY, bool isWater, bool isLake, BiomeType biome)
        {
            return SurfaceRuleResolver.ResolveBlock(worldX, worldY, worldZ, surfaceY, isWater, isLake, biome, Seed);
        }

        public static void DebugPrintTerrainInfo(int worldX, int worldZ)
        {
            TerrainShapeResult shape = TerrainShapeGenerator.GenerateShape(worldX, worldZ, Seed);
            BiomeType biome = BiomeResolver.ResolveBiome(shape);
            BlockType topBlock = PipelineGetBlock(worldX, shape.surfaceY, worldZ, shape.surfaceY, shape.isWater, shape.isLake, biome);

            float t = shape.temperature;
            Debug.Log($"[TerrainDebug] POS X:{worldX} Z:{worldZ} Biome:{biome} SurfY:{shape.surfaceY} isWater:{shape.isWater} isRiver:{shape.isRiver} isLake:{shape.isLake} " +
                      $"Temp:{t:F2} HillsM:{shape.hillsMask:F3} MntM:{shape.mountainMask:F3} TopBlock:{topBlock}");
            Debug.Log($"Height={shape.surfaceY} isWater={shape.isWater} HillsMask={shape.hillsMask:F3} " +
                      $"MountainMask={shape.mountainMask:F3} Final={shape.rawFinalHeight:F2}");
        }
    }
}
