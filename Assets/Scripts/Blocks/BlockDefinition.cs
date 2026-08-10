namespace MineDemo.Blocks
{
    [System.Serializable]
    public struct BlockDefinition
    {
        public BlockType type;
        public TextureId top;
        public TextureId bottom;
        public TextureId side;
        public bool isSolid;
        public bool hasCollider;
        public bool isTransparent;
        public bool isDecoration;
        public bool isOre;

        public BlockDefinition(BlockType type, TextureId top, TextureId bottom, TextureId side, bool isSolid = true, bool hasCollider = true, bool isTransparent = false, bool isDecoration = false, bool isOre = false)
        {
            this.type = type;
            this.top = top;
            this.bottom = bottom;
            this.side = side;
            this.isSolid = isSolid;
            this.hasCollider = hasCollider;
            this.isTransparent = isTransparent;
            this.isDecoration = isDecoration;
            this.isOre = isOre;
        }

        // Helper constructor for blocks with uniform texture
        public BlockDefinition(BlockType type, TextureId all, bool isSolid = true, bool hasCollider = true, bool isTransparent = false, bool isDecoration = false, bool isOre = false)
            : this(type, all, all, all, isSolid, hasCollider, isTransparent, isDecoration, isOre) { }
    }
}
