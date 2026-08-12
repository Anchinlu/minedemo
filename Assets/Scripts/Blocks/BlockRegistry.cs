using System.Collections.Generic;

namespace MineDemo.Blocks
{
    public static class BlockRegistry
    {
        private static readonly Dictionary<BlockType, BlockDefinition> registry = new Dictionary<BlockType, BlockDefinition>();

        // Caching for fast transparency lookups in mesh generation
        public static readonly bool[] isTransparentCache;

        static BlockRegistry()
        {
            // Allocate cache for all possible enum values
            int maxEnumValue = 0;
            foreach (BlockType enumValue in System.Enum.GetValues(typeof(BlockType)))
            {
                int value = (int)enumValue;
                if (value > maxEnumValue) maxEnumValue = value;
            }
            isTransparentCache = new bool[maxEnumValue + 1];

            // Default to true (Air or unrecognized blocks)
            for (int i = 0; i < isTransparentCache.Length; i++)
            {
                isTransparentCache[i] = true;
            }

            Initialize();
            BuildCache();
        }

        private static void Initialize()
        {
            // Air / Water (These might not be fully handled by mesh generator same as solids, but good to have)
            registry[BlockType.Air] = new BlockDefinition(BlockType.Air, TextureId.Dirt, isSolid: false, hasCollider: false);
            registry[BlockType.WaterSource] = new BlockDefinition(BlockType.WaterSource, TextureId.Dirt, isSolid: false, hasCollider: false);
            registry[BlockType.WaterFlow] = new BlockDefinition(BlockType.WaterFlow, TextureId.Dirt, isSolid: false, hasCollider: false);

            // Base terrain
            registry[BlockType.Dirt] = new BlockDefinition(BlockType.Dirt, TextureId.Dirt);
            registry[BlockType.Stone] = new BlockDefinition(BlockType.Stone, TextureId.Stone);
            registry[BlockType.Grass] = new BlockDefinition(BlockType.Grass, TextureId.GrassTop, TextureId.Dirt, TextureId.GrassSide);
            registry[BlockType.Sand] = new BlockDefinition(BlockType.Sand, TextureId.Sand);
            registry[BlockType.Bedrock] = new BlockDefinition(BlockType.Bedrock, TextureId.Bedrock);

            // Flora
            registry[BlockType.OakLog] = new BlockDefinition(BlockType.OakLog, TextureId.OakLogTop, TextureId.OakLogTop, TextureId.OakLogSide);
            registry[BlockType.OakLeaves] = new BlockDefinition(BlockType.OakLeaves, TextureId.OakLeaves, isSolid: false);
            
            registry[BlockType.BirchLog] = new BlockDefinition(BlockType.BirchLog, TextureId.BirchLogTop, TextureId.BirchLogTop, TextureId.BirchLogSide);
            registry[BlockType.BirchLeaves] = new BlockDefinition(BlockType.BirchLeaves, TextureId.BirchLeaves, isSolid: false);

            // Phase 1
            registry[BlockType.Gravel] = new BlockDefinition(BlockType.Gravel, TextureId.Gravel);
            registry[BlockType.Cobblestone] = new BlockDefinition(BlockType.Cobblestone, TextureId.Cobblestone);
            registry[BlockType.Deepslate] = new BlockDefinition(BlockType.Deepslate, TextureId.DeepslateTop, TextureId.DeepslateTop, TextureId.Deepslate);
            registry[BlockType.CoarseDirt] = new BlockDefinition(BlockType.CoarseDirt, TextureId.CoarseDirt);
            registry[BlockType.Clay] = new BlockDefinition(BlockType.Clay, TextureId.Clay);
            
            // Phase 2
            registry[BlockType.Sandstone] = new BlockDefinition(BlockType.Sandstone, TextureId.SandstoneTop, TextureId.SandstoneBottom, TextureId.Sandstone);
            registry[BlockType.Snow] = new BlockDefinition(BlockType.Snow, TextureId.Snow);
            registry[BlockType.GrassSnow] = new BlockDefinition(BlockType.GrassSnow, TextureId.Snow, TextureId.Dirt, TextureId.GrassSnowSide);
            // Ice is solid (to stand on), has collider, is transparent (to see through)
            registry[BlockType.Ice] = new BlockDefinition(BlockType.Ice, TextureId.Ice, true, true, true);
            registry[BlockType.PackedIce] = new BlockDefinition(BlockType.PackedIce, TextureId.PackedIce);
            registry[BlockType.Mud] = new BlockDefinition(BlockType.Mud, TextureId.Mud);

            // Phase 3: Decoration & Flora
            RegisterDecoration(BlockType.Poppy, TextureId.Poppy);
            RegisterDecoration(BlockType.Dandelion, TextureId.Dandelion);
            RegisterDecoration(BlockType.BlueOrchid, TextureId.BlueOrchid);
            RegisterDecoration(BlockType.Allium, TextureId.Allium);
            RegisterDecoration(BlockType.AzureBluet, TextureId.AzureBluet);
            RegisterDecoration(BlockType.RedTulip, TextureId.RedTulip);
            RegisterDecoration(BlockType.OrangeTulip, TextureId.OrangeTulip);
            RegisterDecoration(BlockType.WhiteTulip, TextureId.WhiteTulip);
            RegisterDecoration(BlockType.PinkTulip, TextureId.PinkTulip);
            RegisterDecoration(BlockType.OxeyeDaisy, TextureId.OxeyeDaisy);
            RegisterDecoration(BlockType.Cornflower, TextureId.Cornflower);
            
            RegisterDecoration(BlockType.ShortGrassPlant, TextureId.ShortGrass);
            RegisterDecoration(BlockType.TallGrassLower, TextureId.TallGrassLower);
            RegisterDecoration(BlockType.TallGrassUpper, TextureId.TallGrassUpper);
            RegisterDecoration(BlockType.Fern, TextureId.Fern);
            
            RegisterDecoration(BlockType.ShortDryGrass, TextureId.ShortDryGrass);
            RegisterDecoration(BlockType.TallDryGrassLower, TextureId.TallDryGrassLower);
            RegisterDecoration(BlockType.TallDryGrassUpper, TextureId.TallDryGrassUpper);
        }

        private static void RegisterDecoration(BlockType type, TextureId texture)
        {
            registry[type] = new BlockDefinition(type, texture, isSolid: false, hasCollider: false, isTransparent: true, isDecoration: true);
        }

        public static void BuildCache()
        {
            // Populate the fast cache array
            foreach (var kvp in registry)
            {
                int idx = (int)kvp.Key;
                if (idx >= 0 && idx < isTransparentCache.Length)
                {
                    isTransparentCache[idx] = kvp.Value.isTransparent || !kvp.Value.isSolid;
                }
            }
        }

        public static BlockDefinition Get(BlockType type)
        {
            if (registry.TryGetValue(type, out BlockDefinition def))
                return def;
            
            // Fallback
            return new BlockDefinition(type, TextureId.Dirt, isSolid: false, hasCollider: false);
        }
    }
}
