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
            sample.continentalness = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, context.Seed, 400);
            sample.erosion = WorldGenNoise.Noise2D(worldX, worldZ, 0.0025f, context.Seed, 2);
            sample.ridges = WorldGenNoise.Noise2D(worldX, worldZ, 0.0035f, context.Seed, 600);
            
            sample.peakPotential = WorldGenNoise.Noise2D(worldX, worldZ, 0.0080f, context.Seed, 901);
            sample.jaggedness = WorldGenNoise.RidgedFbm2D(worldX, worldZ, 0.0120f, context.Seed, 950, 3);
            
            // Climate noises (0 to 1 range)
            sample.temperature = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, context.Seed, 700);
            sample.humidity = WorldGenNoise.Noise2D(worldX, worldZ, 0.002f, context.Seed, 800);
            
            return sample;
        }

        public static float SampleCave3D(int worldX, int worldY, int worldZ, in WorldGenContext context)
        {
            // Placeholder for Phase C
            return 0f;
        }
    }
}
