using UnityEngine;

namespace MineDemo.World
{
    public static class DensityRouter
    {
        public static float CalculateTerrainSurface(
            int worldX, int worldZ,
            in WorldGenContext context,
            in NoiseSample n)
        {
            float continental = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, n.continentalness));
            
            float mountainRegion = Mathf.SmoothStep(0.52f, 0.72f, n.ridges);
            float mountainCore = Mathf.SmoothStep(0.64f, 0.94f, n.ridges);

            float lowErosion = 1f - Mathf.SmoothStep(0.30f, 0.75f, n.erosion);

            float peakSeed = Mathf.SmoothStep(0.70f, 0.90f, n.peakPotential);
            float peakWeight = mountainCore * Mathf.Pow(peakSeed, 1.5f) * lowErosion;

            float plainsBase = 66f + n.detail * 5f;
            float continentalLift = (continental - 0.5f) * 8f;

            float foothillLift = mountainRegion * (1f - mountainCore) * 24f;
            float coreLift = mountainCore * 68f * (0.35f + 0.65f * lowErosion);

            float ridgeLift = (n.jaggedness - 0.45f) * 32f * mountainCore * lowErosion;

            float peakLift = peakWeight * 70f;

            return plainsBase + continentalLift + foothillLift + coreLift + ridgeLift + peakLift;
        }

        public static int GetBaseSurfaceY(
            int worldX, int worldZ,
            in WorldGenContext context,
            in NoiseSample n)
        {
            float terrainSurface = CalculateTerrainSurface(worldX, worldZ, context, n);
            return Mathf.Clamp(Mathf.FloorToInt(terrainSurface), context.MinY, context.MaxY - 1);
        }

        public static float GetDensity(
            int worldX, int worldY, int worldZ,
            in WorldGenContext context,
            in NoiseSample n)
        {
            float terrainSurface = CalculateTerrainSurface(worldX, worldZ, context, n);
            float baseDensity = terrainSurface - worldY;

            // In Phase C, we would subtract cave noise here.
            // float cave = NoiseRouter.SampleCave3D(worldX, worldY, worldZ, context);
            // float caveStrength = ...
            // float caveMask = ...
            // baseDensity -= cave * caveMask * caveStrength;

            return baseDensity;
        }
    }
}
