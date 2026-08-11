using UnityEngine;

namespace MineDemo.World
{
    public static class BiomeResolver
    {
        private const float MountainThreshold = 0.79f;
        private const float HillsThreshold = 0.40f;

        public static BiomeType ResolveBiome(TerrainShapeResult shape)
        {
            if (!WorldManager.EnableClimateBiomes)
            {
                if (shape.isWater)
                {
                    return BiomeType.RiverLake;
                }
                else
                {
                    if (shape.mountainMask >= MountainThreshold) return BiomeType.Mountains;
                    else if (shape.hillsMask >= HillsThreshold) return BiomeType.Hills;
                    else return BiomeType.Plains;
                }
            }

            float t = shape.temperature;
            float h = shape.humidity;

            float hotEdge = Mathf.SmoothStep(0.6f, 0.7f, t);
            float dryEdge = Mathf.SmoothStep(0.3f, 0.4f, h);

            bool isDesert = hotEdge > 0.5f && dryEdge < 0.5f;

            if (shape.isWater)
            {
                return (t < 0.12f) ? BiomeType.FrozenRiverLake : BiomeType.RiverLake;
            }
            else if (t < 0.12f && shape.surfaceY > 62)
            {
                return BiomeType.SnowyPlains;
            }
            else if (t < 0.20f && shape.mountainMask >= MountainThreshold)
            {
                return BiomeType.SnowyMountains;
            }
            else if (isDesert)
            {
                return BiomeType.Desert;
            }
            else
            {
                if (shape.mountainMask >= MountainThreshold) return BiomeType.Mountains;
                else if (shape.hillsMask >= HillsThreshold && shape.mountainMask < MountainThreshold) return BiomeType.Hills;
                else if (h > 0.55f && t > 0.4f) return BiomeType.Forest;
                else return BiomeType.Plains;
            }
        }
    }
}
