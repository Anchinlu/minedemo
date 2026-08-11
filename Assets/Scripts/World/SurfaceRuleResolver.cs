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
