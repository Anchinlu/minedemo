using UnityEngine;
using MineDemo.Blocks;

namespace MineDemo.World
{
    public static class DensityChunkSampler
    {
        public static void GenerateChunkData(
            int chunkX, int chunkZ, int width, int height, int depth,
            in WorldGenContext context,
            out BlockType[] blocks, out byte[] waterLevels,
            out int minOccupiedLocalY, out int maxOccupiedLocalY)
        {
            blocks = new BlockType[width * height * depth];
            waterLevels = new byte[width * height * depth];
            minOccupiedLocalY = height - 1;
            maxOccupiedLocalY = 0;
            WorldColumn[] columns = new WorldColumn[width * depth];

            // 1. Build columns using the centralized canonical API and apply Surface Rules directly
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int worldX = chunkX * width + x;
                    int worldZ = chunkZ * depth + z;
                    
                    WorldColumn col = TerrainGenerator.GetWorldColumn(worldX, worldZ);
                    columns[x + width * z] = col;
                    
                    int surfaceY = col.surfaceY;

                    // 2. We only need to iterate below surfaceY to place blocks
                    for (int worldY = surfaceY; worldY >= context.MinY; worldY--)
                    {
                        int yLocal = worldY - context.MinY;
                        int index = x + width * (yLocal + height * z);
                        
                        // Overwrite with surface rules (Phase B)
                        // isSolid is always true since we only iterate at or below surfaceY
                        BlockType finalBlock = SurfaceRuleResolver.ResolveBlock(worldX, worldY, worldZ, col, true, context);
                        blocks[index] = finalBlock;
                        
                        if (finalBlock != BlockType.Air)
                        {
                            minOccupiedLocalY = Mathf.Min(minOccupiedLocalY, yLocal);
                            maxOccupiedLocalY = Mathf.Max(maxOccupiedLocalY, yLocal);
                        }
                    }
                }
            }
        }
    }
}
