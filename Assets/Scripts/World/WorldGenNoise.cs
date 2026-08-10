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
    }
}
