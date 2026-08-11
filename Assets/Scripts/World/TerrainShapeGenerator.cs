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
        public float foothillMask;
        public float mountainMask;
        
        // Multi-Noise Parameters
        public float continentalness;
        public float erosion;
        public float weirdness;
        public float temperature;
        public float humidity;
        
        // Debug
        public float mountainPotential;
        public float plainsWeight;
        public float hillWeight;
        public float mountainWeight;
        public float baseHeight;

        public float rawFinalHeight;
    }

    public static class TerrainShapeGenerator
    {
        public static TerrainShapeResult GenerateShape(int worldX, int worldZ, int seed)
        {
            // 5 Multi-Noise Parameters (Tần số thấp để tạo vùng biome rộng hàng trăm block)
            float continentalness = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, seed, 400); 
            float erosion = WorldGenNoise.Noise2D(worldX, worldZ, 0.0025f, seed, 2); 
            float weirdness = WorldGenNoise.Noise2D(worldX, worldZ, 0.004f, seed, 300); 
            float temperature = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, seed, 10);
            float humidity = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, seed, 11);

            float mountainPotential = WorldGenNoise.Noise2D(worldX, worldZ, 0.0035f, seed, 600);

            float foothillMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.68f, mountainPotential));
            float mountainMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.86f, mountainPotential));

            float erosionFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, erosion));
            float naturalFoothillMask = foothillMask * Mathf.Lerp(1.0f, 0.35f, erosionFactor);

            float erosionBasedHills = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, erosion)); // Plains -> Hills based on erosion
            float hillsMask = Mathf.Max(erosionBasedHills * 0.6f, naturalFoothillMask * (1f - mountainMask));

            // Fractal Brownian Motion (FBM) 4 octaves - Terrain Detail Noise
            float fbm = 0f;
            float amplitude = 1f;
            float freq = 0.02f; // Tần số cao (0.02f) tạo độ gồ ghề nhỏ
            float maxAmplitude = 0f;
            for (int i = 0; i < 4; i++)
            {
                fbm += WorldGenNoise.Noise2D(worldX, worldZ, freq, seed, 12 + i) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= 0.5f;
                freq *= 2f;
            }
            fbm /= maxAmplitude; // Normalize to 0..1
            
            float fbmCentered = fbm - 0.5f;

            // Profiles per Biome Category
            float plainsHeight = 66f + fbmCentered * 8f;
            float hillsHeight = 82f + fbmCentered * 16f;
            float mountainsHeight = 120f + fbmCentered * 35f;

            // Interpolate smoothly with Plains as default base
            float hillWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.70f, hillsMask));
            float mountainWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.79f, 0.95f, mountainMask));

            hillWeight *= 1f - mountainWeight;
            float plainsWeight = 1f - hillWeight - mountainWeight;

            float baseHeight = plainsHeight * plainsWeight + hillsHeight * hillWeight + mountainsHeight * mountainWeight;
            float finalHeight = baseHeight;

            // River Carving Noise
            float riverNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.015f, seed, 7);
            float ridgeRiver = Mathf.Abs(riverNoise - 0.5f) * 2f; 
            float riverMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, ridgeRiver)); 

            // Lake Carving Noise
            float lakeNoise = WorldGenNoise.Noise2D(worldX, worldZ, 0.02f, seed, 99);
            float lakeMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.8f, lakeNoise));

            // Chỉ thực hiện đào lòng sông/hồ khi cờ EnableWaterTerrainCarving được bật
            if (WorldManager.EnableWaterTerrainCarving)
            {
                float riverDepth = Mathf.Lerp(3f, 7f, fbm);
                float riverCarve = riverMask * riverDepth;
                finalHeight -= riverCarve;

                float lakeDepth = lakeMask * Mathf.Lerp(6f, 15f, fbm);
                float lakeCarve = lakeMask * lakeDepth;
                finalHeight -= lakeCarve;
            }

            int terrainHeight = Mathf.Clamp(Mathf.RoundToInt(finalHeight), WorldBounds.MinBuildY + 5, WorldBounds.MaxBuildY - 20);

            // Add Debug.Log every few chunks for monitoring
            if (worldX % 256 == 0 && worldZ % 256 == 0)
            {
                Debug.Log(
                    $"[ShapeCheck] X:{worldX} Z:{worldZ} " +
                    $"PlainsH:{plainsHeight:F2} HillsH:{hillsHeight:F2} MountainH:{mountainsHeight:F2} " +
                    $"PWeight:{plainsWeight:F3} HWeight:{hillWeight:F3} MWeight:{mountainWeight:F3} " +
                    $"Base:{baseHeight:F2} Final:{finalHeight:F2} Surface:{terrainHeight}"
                );
            }

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
                foothillMask = foothillMask,
                mountainMask = mountainMask,
                mountainPotential = mountainPotential,
                plainsWeight = plainsWeight,
                hillWeight = hillWeight,
                mountainWeight = mountainWeight,
                baseHeight = baseHeight,
                continentalness = continentalness,
                erosion = erosion,
                weirdness = weirdness,
                temperature = temperature,
                humidity = humidity,
                rawFinalHeight = finalHeight
            };
        }
    }
}
