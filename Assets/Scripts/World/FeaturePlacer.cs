namespace MineDemo.World
{
    public static class FeaturePlacer
    {
        public static void PlaceChunkFeatures(Chunk chunk, int seed)
        {
            // Feature Stage 1: Trees
            TreeGenerator.GenerateChunkTrees(chunk, seed);

            // Feature Stage 2: Flowers & Grass
            FlowerGrassGenerator.PlaceChunkFlowersAndGrass(chunk, seed);

            // Future Feature Stages: (Disk Sand/Gravel, Ores)
        }
    }
}
