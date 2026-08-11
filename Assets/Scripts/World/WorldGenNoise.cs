using UnityEngine;

namespace MineDemo.World
{
    public static class WorldGenNoise
    {
        private static int Hash(int seed, int salt, int prime)
        {
            int h = seed ^ (salt * prime);
            h = (h ^ (h >> 16)) * 0x45d9f3b;
            h = (h ^ (h >> 16)) * 0x45d9f3b;
            h = h ^ (h >> 16);
            return h;
        }

        public static float Noise2D(int worldX, int worldZ, float scale, int seed, int salt)
        {
            int offsetX = Mathf.Abs(Hash(seed, salt, 17) % 10000);
            int offsetZ = Mathf.Abs(Hash(seed, salt, 31) % 10000);

            return Mathf.PerlinNoise((worldX + offsetX) * scale, (worldZ + offsetZ) * scale);
        }

        public static float RidgedFbm2D(
            int worldX, int worldZ,
            float baseFrequency, int seed, int salt,
            int octaves = 3)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = baseFrequency;
            float amplitudeSum = 0f;

            for (int octave = 0; octave < octaves; octave++)
            {
                float centered = Noise2D(worldX, worldZ, frequency, seed, salt + octave) * 2f - 1f;
                float ridge = 1f - Mathf.Abs(centered);

                // Pow > 1 narrows high crests and avoids broad rounded tops.
                ridge = Mathf.Pow(ridge, 2.0f);

                total += ridge * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return amplitudeSum > 0f ? total / amplitudeSum : 0f;
        }
    }
}
