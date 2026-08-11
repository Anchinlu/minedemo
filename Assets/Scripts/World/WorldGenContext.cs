using UnityEngine;

namespace MineDemo.World
{
    public readonly struct WorldGenContext
    {
        public readonly int Seed;
        public readonly int MinY;
        public readonly int MaxY;
        public readonly int SeaLevel;

        public WorldGenContext(int seed, int minY, int maxY, int seaLevel)
        {
            Seed = seed;
            MinY = minY;
            MaxY = maxY;
            SeaLevel = seaLevel;
        }
    }
}
