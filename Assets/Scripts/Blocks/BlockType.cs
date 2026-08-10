namespace MineDemo.Blocks
{
    public enum BlockType : byte
    {
        Air = 0,
        Dirt = 1,
        Stone = 2,
        Grass,
        OakLog,
        OakLeaves,
        WaterSource,
        WaterFlow,
        Sand = 13,
        Bedrock = 14,
        
        // Phase 1: Địa chất nền
        Gravel = 15,
        Cobblestone,
        Deepslate,
        CoarseDirt,
        Clay,
        
        // Phase 2
        Sandstone,
        Snow,
        GrassSnow,
        Ice,
        PackedIce,
        Mud
    }
}
