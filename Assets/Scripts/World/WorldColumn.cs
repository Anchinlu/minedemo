namespace MineDemo.World
{
    public enum MountainZone
    {
        None,
        Foothill,
        Meadow,
        Slope,
        Peak
    }

    public struct WorldColumn
    {
        public int surfaceY;
        public float slope;
        public NoiseSample noise;
        public BiomeType biome;
        public bool isOceanOrLake;
        public MountainZone mountainZone;
    }
}
