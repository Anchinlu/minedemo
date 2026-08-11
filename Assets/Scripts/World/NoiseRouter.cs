using UnityEngine;

namespace MineDemo.World
{
    public struct NoiseSample
    {
        public float continentalness;
        public float erosion;
        public float ridges;
        public float temperature;
        public float humidity;
        public float detail;
        public float cave;
        public float river; // Noise xẻ rãnh tạo sông chính
        public float river2;// Noise tạo nhánh sông phụ
        public float lake;  // Noise tạo hồ cục bộ hình cái bát
        
        public float peakPotential; // 0..1, separate field for rare high peaks
        public float jaggedness;    // 0..1, ridged FBM for mountain shape
    }

    public static class NoiseRouter
    {
        public static NoiseSample Sample2D(int worldX, int worldZ, in WorldGenContext context)
        {
            NoiseSample sample = new NoiseSample();
            
            // Frequencies and offsets for Phase A/B
            // Normalize detail to roughly -1 to 1
            sample.detail = WorldGenNoise.Noise2D(worldX, worldZ, 0.02f, context.Seed, 500) * 2f - 1f;

            // Macro noises (0 to 1 range)
            // Reduced frequencies (0.0008f) to simulate massive Minecraft biomes (wide plains, expansive mountains)
            sample.continentalness = WorldGenNoise.Noise2D(worldX, worldZ, 0.0008f, context.Seed, 400);
            sample.erosion = WorldGenNoise.Noise2D(worldX, worldZ, 0.0008f, context.Seed, 2);
            sample.ridges = WorldGenNoise.Noise2D(worldX, worldZ, 0.0012f, context.Seed, 600);
            sample.river = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, context.Seed, 7); // Sông rất thưa thớt
            sample.river2 = WorldGenNoise.Noise2D(worldX, worldZ, 0.0025f, context.Seed, 9); // Nhánh phụ cũng thưa
            sample.lake = WorldGenNoise.Noise2D(worldX, worldZ, 0.015f, context.Seed, 8); // Hồ nhỏ ngẫu nhiên
            
            // Peak/Weirdness drives whether mountains have sharp ridges or smooth valleys
            sample.peakPotential = WorldGenNoise.Noise2D(worldX, worldZ, 0.0015f, context.Seed, 901);
            
            // Jaggedness for the actual sharp FBM on peaks
            sample.jaggedness = WorldGenNoise.RidgedFbm2D(worldX, worldZ, 0.01f, context.Seed, 950, 3);
            
            // Climate noises (0 to 1 range) - Reduced frequency for larger biomes
            sample.temperature = WorldGenNoise.Noise2D(worldX, worldZ, 0.001f, context.Seed, 700);
            sample.humidity = WorldGenNoise.Noise2D(worldX, worldZ, 0.001f, context.Seed, 800);
            
            return sample;
        }

        public static float SampleCave3D(int worldX, int worldY, int worldZ, in WorldGenContext context)
        {
            // Placeholder for Phase C
            return 0f;
        }
    }
}
