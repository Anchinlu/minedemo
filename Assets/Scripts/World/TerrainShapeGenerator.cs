using UnityEngine;

namespace MineDemo.World
{
    public struct TerrainShapeResult
    {
        public int surfaceY;
        public bool isWater;
        public bool isLake;
        public bool isRiver;
        public float riverMask;
        public float lakeMask;
        public float hillsMask;
        public float mountainMask;
        public float regionMask;
        public float peakMask;
        public float ridges;
        public float rawFinalHeight;
    }

    public static class TerrainShapeGenerator
    {
        public static TerrainShapeResult GenerateShape(int worldX, int worldZ, int seed)
        {
            float erosion = WorldGenNoise.Noise2D(worldX, worldZ, 0.0025f, seed, 2);
            float mountainRegion = WorldGenNoise.Noise2D(worldX, worldZ, 0.0045f, seed, 400);
            float mountainPeak = WorldGenNoise.Noise2D(worldX, worldZ, 0.012f, seed, 300);

            float hillsMask = Mathf.SmoothStep(0.35f, 0.65f, erosion);
            float regionMask = Mathf.SmoothStep(0.62f, 0.80f, mountainRegion);
            float peakMask = Mathf.SmoothStep(0.74f, 0.92f, mountainPeak);
            float mountainMask = regionMask * peakMask;

            float macro = WorldGenNoise.Noise2D(worldX, worldZ, 0.004f, seed, 12);
            float meso = WorldGenNoise.Noise2D(worldX, worldZ, 0.012f, seed, 13);
            float detail = WorldGenNoise.Noise2D(worldX, worldZ, 0.04f, seed, 14);

            float commonHeight =
                68f
                + (macro - 0.5f) * 16f
                + (meso - 0.5f) * 6f
                + (detail - 0.5f) * 1f;

            float hillRise = hillsMask * 9f;
            float hillShape = hillsMask * (macro - 0.5f) * 10f;
            float baseTerrainHeight = commonHeight + hillRise + hillShape;

            float mountainRise = mountainMask * 30f;
            float mountainShape = mountainMask * peakMask * 35f;
            float mountainTerrainHeight = baseTerrainHeight + mountainRise + mountainShape;

            float mountainBlend = Mathf.SmoothStep(0.0f, 1.0f, mountainMask);
            float finalHeight = Mathf.Lerp(baseTerrainHeight, mountainTerrainHeight, mountainBlend);

            // River Carving Noise
            float riverNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.005f, seed, 7);
            float ridgeRiver = Mathf.Abs(riverNoise - 0.5f) * 2f; 
            float riverMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, ridgeRiver)); 

            // Lake Carving Noise
            float lakeNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.01f, seed, 99);
            float lakeMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.8f, lakeNoise));

            // Chỉ thực hiện đào lòng sông/hồ khi cờ EnableWaterTerrainCarving được bật
            if (WorldManager.EnableWaterTerrainCarving)
            {
                float riverDepth = Mathf.Lerp(2f, 5f, meso);
                float riverCarve = riverMask * riverDepth;
                finalHeight -= riverCarve;

                float lakeDepth = lakeMask * Mathf.Lerp(4f, 10f, detail);
                float lakeCarve = lakeMask * lakeDepth;
                finalHeight -= lakeCarve;
            }

            int terrainHeight = Mathf.Clamp(Mathf.RoundToInt(finalHeight), 40, WorldBounds.MaxBuildY - 20);

            bool isLake = lakeMask > 0.5f;
            bool isRiver = riverMask > 0.1f;
            bool isWater = terrainHeight < WorldBounds.SeaLevel && (isRiver || isLake);

            return new TerrainShapeResult
            {
                surfaceY = terrainHeight,
                isWater = isWater,
                isLake = isLake,
                isRiver = isRiver,
                riverMask = riverMask,
                lakeMask = lakeMask,
                hillsMask = hillsMask,
                mountainMask = mountainMask,
                regionMask = regionMask,
                peakMask = peakMask,
                ridges = mountainPeak,
                rawFinalHeight = finalHeight
            };
        }
    }
}
