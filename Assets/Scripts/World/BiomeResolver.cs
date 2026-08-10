using UnityEngine;

namespace MineDemo.World
{
    public static class BiomeResolver
    {
        public static BiomeType ResolveBiome(
            int worldX, 
            int worldZ, 
            int surfaceY, 
            bool isWater, 
            float mountainMask, 
            float hillsMask, 
            int seed)
        {
            if (!WorldManager.EnableClimateBiomes)
            {
                if (isWater)
                {
                    return BiomeType.RiverLake;
                }
                else
                {
                    if (mountainMask >= 0.45f) return BiomeType.Mountains;
                    else if (hillsMask >= 0.40f) return BiomeType.Hills;
                    else return BiomeType.Plains;
                }
            }

            float t = WorldGenNoise.Noise2D(worldX, worldZ, 0.004f, seed, 10);
            float h = WorldGenNoise.Noise2D(worldX, worldZ, 0.004f, seed, 11);

            float hotEdge = Mathf.SmoothStep(0.6f, 0.7f, t);
            float dryEdge = Mathf.SmoothStep(0.3f, 0.4f, h);

            bool isDesert = hotEdge > 0.5f && dryEdge < 0.5f;

            if (isWater)
            {
                return (t < 0.12f) ? BiomeType.FrozenRiverLake : BiomeType.RiverLake;
            }
            else if (t < 0.12f && surfaceY > 62)
            {
                return BiomeType.SnowyPlains;
            }
            else if (t < 0.20f && mountainMask >= 0.45f)
            {
                return BiomeType.SnowyMountains;
            }
            else if (isDesert)
            {
                return BiomeType.Desert;
            }
            else
            {
                if (mountainMask >= 0.45f) return BiomeType.Mountains;
                else if (hillsMask >= 0.40f && mountainMask < 0.45f) return BiomeType.Hills;
                else if (h > 0.55f && t > 0.4f) return BiomeType.Forest;
                else return BiomeType.Plains;
            }
        }
    }
}
