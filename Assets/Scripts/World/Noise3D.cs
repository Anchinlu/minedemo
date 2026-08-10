using UnityEngine;

namespace MineDemo.World
{
    public static class Noise3D
    {
        // Simple 3D Perlin Noise using Unity's 2D Perlin as a base approximation 
        // to save complex implementation, or implementing a real 3D noise.
        // A common trick to get 3D noise from 2D Perlin is combining 3 orthogonal 2D planes.
        public static float Perlin3D(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float yz = Mathf.PerlinNoise(y, z);
            float xz = Mathf.PerlinNoise(x, z);

            float yx = Mathf.PerlinNoise(y, x);
            float zy = Mathf.PerlinNoise(z, y);
            float zx = Mathf.PerlinNoise(z, x);

            return (xy + yz + xz + yx + zy + zx) / 6.0f;
        }
        
        // A more rugged noise often used for caves
        public static float FBM3D(float x, float y, float z, int octaves, float persistence, float lacunarity)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float maxValue = 0;

            for(int i = 0; i < octaves; i++)
            {
                total += Perlin3D(x * frequency, y * frequency, z * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / maxValue;
        }
    }
}
