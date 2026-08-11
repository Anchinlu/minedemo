using UnityEngine;
using MineDemo.Blocks;

namespace MineDemo.World
{
    public static class SurfaceRuleResolver
    {
        public static BlockType ResolveBlock(
            int worldX, 
            int worldY, 
            int worldZ, 
            int surfaceY, 
            bool isWater, 
            bool isLake, 
            BiomeType biome,
            int seed)
        {
            if (worldY == WorldBounds.MinBuildY) return BlockType.Bedrock;

            // Liquid / Air above surface
            if (isWater && worldY > surfaceY && worldY <= WorldBounds.SeaLevel)
            {
                if (worldY == WorldBounds.SeaLevel && biome == BiomeType.FrozenRiverLake) 
                    return BlockType.Ice;

                if (!WorldManager.EnableWater) return BlockType.Air;
                return BlockType.WaterSource;
            }

            if (worldY > surfaceY) return BlockType.Air;

            // Surface block (worldY == surfaceY)
            if (worldY == surfaceY)
            {
                if (biome == BiomeType.Desert)
                {
                    return BlockType.Sand;
                }

                if (isWater) 
                {
                    float bankNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, seed, 20);
                    float t = WorldGenNoise.Noise2D(worldX, worldZ, 0.004f, seed, 10);
                    float h = WorldGenNoise.Noise2D(worldX, worldZ, 0.004f, seed, 11);
                    
                    if (isLake && h > 0.7f && t > 0.5f) return BlockType.Mud;

                    if (bankNoise < 0.25f) return BlockType.Clay;
                    if (bankNoise < 0.50f) return BlockType.Gravel;
                    if (bankNoise < 0.75f) return BlockType.Sand;
                    return BlockType.Mud;
                }
                else
                {
                    if (biome == BiomeType.SnowyMountains)
                    {
                        float snowNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.1f, seed, 21);
                        if (surfaceY > 120 && snowNoise > 0.3f) return BlockType.Snow;
                        return BlockType.GrassSnow;
                    }
                    if (biome == BiomeType.SnowyPlains) return BlockType.GrassSnow;

                    float coarseNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.3f, seed, 22);
                    if (biome == BiomeType.Mountains)
                    {
                        if (surfaceY > 180) 
                        {
                            float cobbleNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.2f, seed, 23);
                            return cobbleNoise < 0.04f ? BlockType.Cobblestone : BlockType.Stone;
                        }
                        if (coarseNoise < 0.06f) return BlockType.CoarseDirt;
                    }
                    else if (biome == BiomeType.Hills)
                    {
                        if (coarseNoise < 0.10f) return BlockType.CoarseDirt;
                    }
                    else if (biome == BiomeType.Plains)
                    {
                        if (coarseNoise < 0.01f) return BlockType.CoarseDirt;
                    }
                    return BlockType.Grass;
                }
            }

            // Sub-surface layer (worldY >= surfaceY - 3)
            if (worldY >= surfaceY - 3)
            {
                if (biome == BiomeType.Desert)
                {
                    return BlockType.Sand;
                }

                if (isWater)
                {
                    float bedNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, seed, 24);
                    if (bedNoise > 0.5f) return BlockType.Gravel;
                    else return BlockType.Dirt;
                }
                else
                {
                    return BlockType.Dirt;
                }
            }

            // Deep stone & Deepslate transition
            BlockType rockType = BlockType.Stone;
            if (biome == BiomeType.Desert && worldY >= surfaceY - 6)
            {
                rockType = BlockType.Sandstone;
            }
            else if (worldY <= -48)
            {
                float deepNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.1f, seed, 25);
                rockType = deepNoise > 0.85f ? BlockType.Stone : BlockType.Deepslate;
            }
            else if (worldY < -8)
            {
                float t = Mathf.InverseLerp(-8f, -48f, worldY);
                float transitionNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.1f, seed, 26);
                rockType = transitionNoise < t * 0.85f ? BlockType.Deepslate : BlockType.Stone;
            }

            if (rockType == BlockType.Stone && worldY >= surfaceY - 6)
            {
                float cobbleNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.2f, seed, 27);
                if (cobbleNoise < 0.03f) return BlockType.Cobblestone;
            }

            return rockType;
        }
        public static BlockType ResolveBlock(
            int worldX,
            int worldY,
            int worldZ,
            in WorldColumn col,
            bool isSolid,
            in WorldGenContext context)
        {
            if (worldY == WorldBounds.MinBuildY) return BlockType.Bedrock;

            if (!isSolid)
            {
                // In Phase A/B, water and caves are disabled, so non-solid is just Air.
                // Later phases will use AquiferResolver here.
                return BlockType.Air;
            }

            // Surface layers
            if (worldY == col.surfaceY)
            {
                // Bờ biển / Mép đảo: Nếu độ cao nằm trong khoảng Y=63 đến Y=65 (lấp xấp mặt nước)
                if (col.surfaceY >= 63 && col.surfaceY <= 65 && col.biome != BiomeType.RiverLake && col.biome != BiomeType.Mountains)
                {
                    // Phủ cát hầu hết dải bờ biển, xen kẽ tí cỏ cho tự nhiên
                    float beachNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.1f, context.Seed, 40);
                    if (beachNoise > 0.15f) return BlockType.Sand; // 85% bãi biển/đảo là Cát
                }

                if (col.biome == BiomeType.RiverLake || col.biome == BiomeType.FrozenRiverLake)
                {
                    int depth = 63 - worldY; // Giả sử WaterLevel = 63
                    float noise1 = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, context.Seed, 20);
                    float noise2 = WorldGenNoise.Noise2D(worldX, worldZ, 0.1f, context.Seed, 30);

                    // 1. Hai bên mép sông (độ sâu 0 - 1 khối)
                    if (depth <= 1)
                    {
                        // Chủ yếu là cát (70%), còn lại là cỏ (30%)
                        if (noise1 > 0.3f) return BlockType.Sand;
                        return BlockType.Grass;
                    }
                    
                    // 2. Phần đáy sông (độ sâu > 1)
                    // Khối đá lốm đốm rải rác hoặc xuất hiện nhiều ở cuối đáy sâu (> 10 block)
                    if (depth > 10 && noise2 > 0.4f) return BlockType.Stone;
                    if (noise2 > 0.92f) return BlockType.Stone; 

                    // Tỷ lệ phân bổ: Đất cao nhất, Cát vừa, Sét/Sỏi rất ít
                    if (noise1 < 0.10f) return BlockType.Clay;   // 10% Đất sét
                    if (noise1 < 0.20f) return BlockType.Gravel; // 10% Sỏi
                    if (noise1 < 0.50f) return BlockType.Sand;   // 30% Cát
                    
                    return BlockType.Dirt;                       // 50% Đất (Chiếm tỷ lệ cao nhất)
                }

                if (col.biome == BiomeType.Desert) return BlockType.Sand;
                if (col.biome == BiomeType.SnowyMountains)
                {
                    float snowNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.1f, context.Seed, 21);
                    if (col.surfaceY > 120 && snowNoise > 0.3f) return BlockType.Snow;
                    return BlockType.GrassSnow;
                }
                if (col.biome == BiomeType.SnowyPlains) return BlockType.GrassSnow;
                
                if (col.biome == BiomeType.Mountains)
                {
                    // 1. Tầng tuyết vĩnh cửu bao phủ đỉnh núi siêu cao (Y > 155)
                    if (col.surfaceY > 155) 
                    {
                        float snowNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, context.Seed, 50);
                        return snowNoise > 0.1f ? BlockType.Snow : BlockType.Stone; // Chủ yếu là Tuyết
                    }
                    
                    // 2. Tầng chuyển giao: Tuyết lốm đốm xen lẫn Đá (Y: 130 -> 155)
                    if (col.surfaceY > 130)
                    {
                        float snowNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.08f, context.Seed, 51);
                        float snowChance = Mathf.InverseLerp(130f, 155f, col.surfaceY);
                        // Càng lên cao tỷ lệ Tuyết càng dày đặc
                        return snowNoise < snowChance ? BlockType.Snow : BlockType.Stone;
                    }

                    // 3. Các vùng núi đá thông thường
                    if (col.mountainZone == MountainZone.Peak)
                    {
                        float cobbleNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.2f, context.Seed, 23);
                        return cobbleNoise < 0.05f ? BlockType.Cobblestone : BlockType.Stone;
                    }

                    if (col.mountainZone == MountainZone.Slope && (col.surfaceY >= 145 || col.slope >= 3f))
                    {
                        return BlockType.Stone;
                    }

                    float coarseNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, context.Seed, 22);
                    if (col.mountainZone == MountainZone.Slope && col.slope >= 2f && coarseNoise < 0.12f)
                    {
                        return BlockType.CoarseDirt;
                    }
                    
                    return BlockType.Grass;
                }

                float forestCoarseNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.3f, context.Seed, 22);
                if (col.biome == BiomeType.Forest && forestCoarseNoise > 0.7f) return BlockType.CoarseDirt;
                return BlockType.Grass;
            }

            // Sub-surface dirt/sand (1-3 blocks deep)
            if (worldY >= col.surfaceY - 3 && worldY < col.surfaceY)
            {
                if (col.biome == BiomeType.RiverLake || col.biome == BiomeType.FrozenRiverLake)
                {
                    float bedNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, context.Seed, 24);
                    if (bedNoise > 0.5f) return BlockType.Gravel;
                    if (bedNoise > 0.2f) return BlockType.Sand;
                    return BlockType.Dirt;
                }
                
                if (col.biome == BiomeType.Desert) return BlockType.Sandstone;
                if (col.biome == BiomeType.Mountains && (col.mountainZone == MountainZone.Peak || col.mountainZone == MountainZone.Slope))
                {
                    // Subsurface is stone if surface is stone or steep
                    return BlockType.Stone;
                }
                return BlockType.Dirt;
            }

            // Deep underground
            if (worldY < 0)
            {
                float deepslateNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.05f, context.Seed, 26);
                float threshold = Mathf.InverseLerp(0, -16, worldY); 
                return deepslateNoise < threshold ? BlockType.Deepslate : BlockType.Stone;
            }

            return BlockType.Stone;
        }
    }
}
