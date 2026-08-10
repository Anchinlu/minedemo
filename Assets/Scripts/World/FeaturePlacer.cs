namespace MineDemo.World
{
    public static class FeaturePlacer
    {
        public static void PlaceChunkFeatures(Chunk chunk, int seed)
        {
            // Feature Stage 1: Trees & Vegetation
            TreeGenerator.GenerateChunkTrees(chunk, seed);

            // Future Feature Stages: (Grass patches, Flowers, Disk Sand/Gravel, Ores)
        }
    }
}
