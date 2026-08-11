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

        public static BiomeType ResolveBiome(in WorldColumn col)
        {
            float mountainWeight = MountainZoneResolver.GetMountainCoreWeight(col);
            float foothillWeight = MountainZoneResolver.GetMountainRegionWeight(col);
            foothillWeight *= 1f - mountainWeight;

            if (!WorldManager.EnableClimateBiomes)
            {
                if (col.isOceanOrLake) return BiomeType.RiverLake;

                if (mountainWeight >= 0.45f) return BiomeType.Mountains;
                if (foothillWeight >= 0.35f) return BiomeType.Hills;
                return BiomeType.Plains;
            }

            float t = col.noise.temperature;
            float h = col.noise.humidity;

            float hotEdge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.7f, t));
            float dryEdge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.4f, h));

            bool isDesert = hotEdge > 0.5f && dryEdge < 0.5f;

            if (col.isOceanOrLake)
            {
                return (t < 0.12f) ? BiomeType.FrozenRiverLake : BiomeType.RiverLake;
            }
            
            if (t < 0.12f && col.surfaceY > 62) return BiomeType.SnowyPlains;
            if (t < 0.20f && mountainWeight >= 0.45f) return BiomeType.SnowyMountains;
            if (isDesert) return BiomeType.Desert;

            if (mountainWeight >= 0.45f) return BiomeType.Mountains;
            if (foothillWeight >= 0.35f) return BiomeType.Hills;
            
            if (h > 0.55f && t > 0.4f) return BiomeType.Forest;
            
            return BiomeType.Plains;
        }
    }
}
