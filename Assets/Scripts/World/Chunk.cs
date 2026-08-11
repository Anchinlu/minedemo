using UnityEngine;
using MineDemo.Blocks;
using System.Collections.Generic;

namespace MineDemo.World
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class Chunk : MonoBehaviour
    {
        public static readonly int Width = 16;
        public static readonly int Height = WorldBounds.WorldHeight; // 550
        public static readonly int Depth = 16;

        private BlockType[] blocks;
        private byte[] waterLevels;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;

        public int chunkX { get; private set; }
        public int chunkZ { get; private set; }
        public WorldManager worldManager;
        public AtlasData atlasData;
        
        private MeshFilter decorationFilter;
        private MeshRenderer decorationRenderer;
        private MeshFilter fluidFilter;
        private MeshRenderer fluidRenderer;

        private int meshMinLocalY;
        private int meshMaxLocalY;
        private const int MeshSafetyMargin = 1;

        private bool hasGeneratedMeshOnce = false;
        private float lastRebuildTime = -1f;
        private int rebuildsThisSecond = 0;

        // GC Pooling
        private List<Vector3> vertices = new List<Vector3>(4000);
        private List<int> triangles = new List<int>(4000);
        private List<Vector2> uvs = new List<Vector2>(4000);
        private List<Color> colors = new List<Color>(4000);

        private List<Vector3> decVertices = new List<Vector3>(1000);
        private List<int> decTriangles = new List<int>(1000);
        private List<Vector2> decUvs = new List<Vector2>(1000);
        private List<Color> decColors = new List<Color>(1000);

        private List<Vector3> fluidVertices = new List<Vector3>(1000);
        private List<int> stillTriangles = new List<int>(1000);
        private List<int> flowTriangles = new List<int>(1000);
        private List<Vector2> fluidUvs = new List<Vector2>(1000);
        private List<Color> fluidColors = new List<Color>(1000);

        private Mesh mainMesh;
        private Mesh decMesh;
        private Mesh fluidMesh;

        void Awake()
        {
            blocks = new BlockType[Width * Height * Depth];
            waterLevels = new byte[Width * Height * Depth];
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();

            mainMesh = new Mesh();
            mainMesh.name = "ChunkMainMesh";
            meshFilter.sharedMesh = mainMesh;
            
            GameObject decObj = new GameObject("Decoration");
            decObj.transform.parent = transform;
            decObj.transform.localPosition = Vector3.zero;
            decorationFilter = decObj.AddComponent<MeshFilter>();
            decorationRenderer = decObj.AddComponent<MeshRenderer>();
            decMesh = new Mesh();
            decMesh.name = "ChunkDecMesh";
            decorationFilter.sharedMesh = decMesh;

            GameObject fluidObj = new GameObject("Fluid");
            fluidObj.transform.parent = transform;
            fluidObj.transform.localPosition = Vector3.zero;
            fluidFilter = fluidObj.AddComponent<MeshFilter>();
            fluidRenderer = fluidObj.AddComponent<MeshRenderer>();
            fluidMesh = new Mesh();
            fluidMesh.name = "ChunkFluidMesh";
            fluidFilter.sharedMesh = fluidMesh;
            fluidRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Initialize(int offsetX, int offsetZ, WorldManager manager, AtlasData atlas)
        {
            this.chunkX = offsetX;
            this.chunkZ = offsetZ;
            this.worldManager = manager;
            this.atlasData = atlas;
            
            GenerateTerrain();
            
            // Cập nhật block từ Player và Nước (Global Modifications)
            foreach (var kvp in worldManager.globalModifications)
            {
                Vector3Int wPos = kvp.Key;
                int chunkXForBlock = Mathf.FloorToInt((float)wPos.x / Width);
                int chunkZForBlock = Mathf.FloorToInt((float)wPos.z / Depth);
                
                if (chunkXForBlock == chunkX && chunkZForBlock == chunkZ)
                {
                    int localX = wPos.x - chunkX * Width;
                    int localZ = wPos.z - chunkZ * Depth;
                    int localY = wPos.y - WorldBounds.MinBuildY;
                    
                    if (localY >= 0 && localY < Height)
                    {
                        SetBlockLocal(localX, localY, localZ, kvp.Value);
                        if (worldManager.globalWaterLevels.TryGetValue(wPos, out byte level))
                        {
                            SetWaterLevelLocal(localX, localY, localZ, level);
                            if (WaterManager.Instance != null)
                                WaterManager.Instance.EnqueueWaterUpdate(wPos, level);
                        }
                        if (kvp.Value != BlockType.Air)
                            IncludeLocalYInMeshBounds(localY);
                    }
                }
            }

            // Áp dụng lá cây/block từ chunk lân cận đâm sang (Procedural Blocks)
            foreach (var chunkBlocks in worldManager.chunkProceduralBlocks.Values)
            {
                foreach (var kvp in chunkBlocks)
                {
                    Vector3Int wPos = kvp.Key;
                    int chunkXForBlock = Mathf.FloorToInt((float)wPos.x / Width);
                    int chunkZForBlock = Mathf.FloorToInt((float)wPos.z / Depth);
                    
                    if (chunkXForBlock == chunkX && chunkZForBlock == chunkZ)
                    {
                        int localX = wPos.x - chunkX * Width;
                        int localZ = wPos.z - chunkZ * Depth;
                        int localY = wPos.y - WorldBounds.MinBuildY;
                        
                        if (localY >= 0 && localY < Height)
                        {
                            SetBlockLocal(localX, localY, localZ, kvp.Value);
                            if (kvp.Value != BlockType.Air)
                                IncludeLocalYInMeshBounds(localY);
                        }
                    }
                }
            }

            // Sinh các Feature cho chunk này (Cây cối, thực vật)
            FeaturePlacer.PlaceChunkFeatures(this, TerrainGenerator.Seed);

            decorationRenderer.material = GetComponent<MeshRenderer>().sharedMaterial;
            
            if (WaterManager.Instance == null)
            {
                GameObject wm = new GameObject("WaterManager");
                wm.AddComponent<WaterManager>();
            }

            if (WaterManager.Instance != null && WaterManager.Instance.stillMaterial != null && WaterManager.Instance.flowMaterial != null)
            {
                fluidRenderer.materials = new Material[] { 
                    WaterManager.Instance.stillMaterial, 
                    WaterManager.Instance.flowMaterial 
                };
            }
            
            GenerateMesh();
        }

        private Color GetGrassTint(int worldX, int worldY, int worldZ)
        {
            BiomeType biome = TerrainGenerator.GetBiome(worldX, worldZ);
            Color baseGrass = new Color(0.36f, 0.68f, 0.20f);
            Color finalTint = baseGrass;
            
            if (biome == BiomeType.Forest) finalTint = new Color(0.30f, 0.60f, 0.18f);
            else if (biome == BiomeType.Mountains) finalTint = new Color(0.31f, 0.59f, 0.18f);
            else if (biome == BiomeType.Hills) finalTint = new Color(0.33f, 0.63f, 0.18f);
            else
            {
                float t = Mathf.InverseLerp(62f, 150f, worldY);
                finalTint = Color.Lerp(baseGrass, new Color(0.31f, 0.59f, 0.18f), t);
            }
            
            // Limit how dark grass can get
            finalTint.r = Mathf.Max(finalTint.r, 0.30f);
            finalTint.g = Mathf.Max(finalTint.g, 0.58f);
            finalTint.b = Mathf.Max(finalTint.b, 0.17f);
            
            return finalTint;
        }

        private void IncludeLocalYInMeshBounds(int localY)
        {
            if (localY < 0 || localY >= Height)
                return;

            meshMinLocalY = Mathf.Min(meshMinLocalY, localY);
            meshMaxLocalY = Mathf.Max(meshMaxLocalY, localY);
        }

        private void GenerateTerrain()
        {
            TerrainGenerator.GenerateChunkData(chunkX, chunkZ, Width, Height, Depth, out blocks, out waterLevels, out int minOccupiedLocalY, out int maxOccupiedLocalY);
            
            int minSurfaceY = 999;
            int maxSurfaceY = -999;

            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    int worldX = chunkX * Width + x;
                    int worldZ = chunkZ * Depth + z;
                    
                    int surfaceY;
                    if (TerrainGenerator.CurrentMode == WorldGenMode.Density)
                    {
                        WorldGenContext context = new WorldGenContext(TerrainGenerator.Seed, WorldBounds.MinBuildY, WorldBounds.MaxBuildY, WorldBounds.SeaLevel);
                        NoiseSample noise = NoiseRouter.Sample2D(worldX, worldZ, context);
                        surfaceY = DensityRouter.GetBaseSurfaceY(worldX, worldZ, context, noise);
                    }
                    else
                    {
                        TerrainShapeResult shape = TerrainShapeGenerator.GenerateShape(worldX, worldZ, TerrainGenerator.Seed);
                        surfaceY = shape.surfaceY;
                    }
                    
                    if (surfaceY < minSurfaceY) minSurfaceY = surfaceY;
                    if (surfaceY > maxSurfaceY) maxSurfaceY = surfaceY;
                }
            }

            int minSurfaceLocalY = minSurfaceY - WorldBounds.MinBuildY;
            
            if (WorldManager.EnableCaves)
            {
                meshMinLocalY = Mathf.Max(0, minOccupiedLocalY - MeshSafetyMargin);
            }
            else
            {
                meshMinLocalY = Mathf.Max(0, minSurfaceLocalY - 2);
            }
            
            meshMaxLocalY = Mathf.Min(Height - 1, maxOccupiedLocalY + MeshSafetyMargin);

            if (WorldManager.EnableWorldGenDiagnostics)
            {
                int grassCount = 0, dirtCount = 0, stoneCount = 0, airCount = 0;
                for (int i = 0; i < blocks.Length; i++)
                {
                    BlockType b = blocks[i];
                    if (b == BlockType.Grass || b == BlockType.GrassSnow) grassCount++;
                    else if (b == BlockType.Dirt || b == BlockType.CoarseDirt) dirtCount++;
                    else if (b == BlockType.Stone || b == BlockType.Cobblestone || b == BlockType.Deepslate || b == BlockType.Sandstone) stoneCount++;
                    else if (b == BlockType.Air) airCount++;
                }
                Debug.Log($"[ChunkTerrain] {chunkX},{chunkZ} surfaceMin={minSurfaceY} surfaceMax={maxSurfaceY} grass={grassCount} dirt={dirtCount} stone={stoneCount} air={airCount}");
            }
        }

        private void SetBlock(int x, int y, int z, BlockType type)
        {
            blocks[x + Width * (y + Height * z)] = type;
            if (type != BlockType.Air)
                IncludeLocalYInMeshBounds(y);
        }

        private BlockType GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= Height) return BlockType.Air;
            
            // Xử lý block nằm ngoài phạm vi chunk (ranh giới)
            if (x < 0 || x >= Width || z < 0 || z >= Depth)
            {
                if (worldManager != null) 
                {
                    int worldX = chunkX * Width + x;
                    int worldY = y + WorldBounds.MinBuildY;
                    int worldZ = chunkZ * Depth + z;
                    return worldManager.GetBlockFromWorld(worldX, worldY, worldZ);
                }
                return BlockType.Air;
            }

            return blocks[x + Width * (y + Height * z)];
        }

        public BlockType GetBlockLocal(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
                return BlockType.Air;
            return blocks[x + Width * (y + Height * z)];
        }

        public void SetBlockLocal(int x, int y, int z, BlockType type)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth) return;
            blocks[x + Width * (y + Height * z)] = type;
            if (type != BlockType.Air)
                IncludeLocalYInMeshBounds(y);
        }

        public byte GetWaterLevelLocal(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
                return 0;
            return waterLevels[x + Width * (y + Height * z)];
        }

        private byte GetWaterLevelForMesh(int localX, int localY, int localZ)
        {
            if (localX >= 0 && localX < Width &&
                localY >= 0 && localY < Height &&
                localZ >= 0 && localZ < Depth)
            {
                return GetWaterLevelLocal(localX, localY, localZ);
            }

            int worldX = chunkX * Width + localX;
            int worldY = localY + WorldBounds.MinBuildY;
            int worldZ = chunkZ * Depth + localZ;

            return worldManager == null
                ? (byte)0
                : worldManager.GetWaterLevelWorld(worldX, worldY, worldZ);
        }

        public void SetWaterLevelLocal(int x, int y, int z, byte level)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth) return;
            waterLevels[x + Width * (y + Height * z)] = level;
        }

        public void EditBlock(int localX, int worldY, int localZ, BlockType newType)
        {
            int localY = worldY - WorldBounds.MinBuildY;
            if (localX >= 0 && localX < Width && localY >= 0 && localY < Height && localZ >= 0 && localZ < Depth)
            {
                if (GetBlockLocal(localX, localY, localZ) == newType)
                    return;

                SetBlockLocal(localX, localY, localZ, newType);
                if (newType == BlockType.Air)
                {
                    SetWaterLevelLocal(localX, localY, localZ, 0); // Xoá nước nếu có
                }
                
                GenerateMesh();
            }
        }

        private TextureId GetTextureId(BlockType type, Vector3 direction)
        {
            var def = BlockRegistry.Get(type);
            if (direction == Vector3.up) return def.top;
            if (direction == Vector3.down) return def.bottom;
            return def.side;
        }

        private bool IsTransparent(BlockType type)
        {
            if (type == BlockType.Air || type == BlockType.WaterSource || type == BlockType.WaterFlow) return true;
            return BlockRegistry.Get(type).isTransparent || !BlockRegistry.Get(type).isSolid;
        }

        public void GenerateMesh()
        {
            long startMemory = System.GC.GetTotalMemory(false);
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            vertices.Clear();
            triangles.Clear();
            uvs.Clear();
            colors.Clear();

            decVertices.Clear();
            decTriangles.Clear();
            decUvs.Clear();
            decColors.Clear();

            fluidVertices.Clear();
            stillTriangles.Clear();
            flowTriangles.Clear();
            fluidUvs.Clear();
            fluidColors.Clear();

            int waterCells = 0, topFaces = 0, sideFaces = 0, bottomFaces = 0;
            int solidFacesCount = 0;

            for (int x = 0; x < Width; x++)
            {
                for (int y = meshMinLocalY; y <= meshMaxLocalY; y++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        BlockType type = GetBlock(x, y, z);

                        if (type == BlockType.WaterSource || type == BlockType.WaterFlow)
                        {
                            waterCells++;
                            byte level = GetWaterLevelLocal(x, y, z);
                            
                            BlockType upBlock = GetBlock(x, y + 1, z);
                            bool hasWaterAbove = upBlock == BlockType.WaterSource || upBlock == BlockType.WaterFlow;
                            
                            float topHeight = GetCellTopHeight(hasWaterAbove, level);
                            float sideHeight = hasWaterAbove ? 1.0f : topHeight;
                            
                            List<int> targetTriangles = (type == BlockType.WaterSource) ? stillTriangles : flowTriangles;

                            if (!hasWaterAbove)
                            {
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.up, topHeight); 
                                topFaces++;
                            }

                            if (ShouldRenderWaterFace(x, y - 1, z, level, Vector3.down))
                            {
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.down, sideHeight);
                                bottomFaces++;
                            }

                            if (ShouldRenderWaterFace(x, y, z + 1, level, Vector3.forward))
                            {
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.forward, sideHeight);
                                sideFaces++;
                            }

                            if (ShouldRenderWaterFace(x, y, z - 1, level, Vector3.back))
                            {
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.back, sideHeight);
                                sideFaces++;
                            }

                            if (ShouldRenderWaterFace(x + 1, y, z, level, Vector3.right))
                            {
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.right, sideHeight);
                                sideFaces++;
                            }

                            if (ShouldRenderWaterFace(x - 1, y, z, level, Vector3.left))
                            {
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.left, sideHeight);
                                sideFaces++;
                            }
                        }
                        else if (BlockRegistry.Get(type).isDecoration)
                        {
                            // Thực vật (cỏ, hoa) vẽ dạng chữ X và cho vào decMesh
                            AddCrossedQuads(decVertices, decTriangles, decUvs, decColors, x, y, z, GetTextureId(type, Vector3.up));
                        }
                        else if (type != BlockType.Air)
                        {
                            Color baseColor = Color.white;
                            if (type == BlockType.Grass)
                            {
                                int worldX = chunkX * Width + x;
                                int worldY = y + WorldBounds.MinBuildY;
                                int worldZ = chunkZ * Depth + z;
                                baseColor = GetGrassTint(worldX, worldY, worldZ);
                            }

                            if (IsTransparent(GetBlock(x, y + 1, z)))
                            {
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.up, GetTextureId(type, Vector3.up), baseColor * GetMaterialFaceShade(type, Vector3.up));
                                solidFacesCount++;
                            }

                            if (IsTransparent(GetBlock(x, y - 1, z)))
                            {
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.down, GetTextureId(type, Vector3.down), baseColor * GetMaterialFaceShade(type, Vector3.down));
                                solidFacesCount++;
                            }

                            if (IsTransparent(GetBlock(x, y, z + 1)))
                            {
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.forward, GetTextureId(type, Vector3.forward), baseColor * GetMaterialFaceShade(type, Vector3.forward));
                                solidFacesCount++;
                            }

                            if (IsTransparent(GetBlock(x, y, z - 1)))
                            {
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.back, GetTextureId(type, Vector3.back), baseColor * GetMaterialFaceShade(type, Vector3.back));
                                solidFacesCount++;
                            }

                            if (IsTransparent(GetBlock(x + 1, y, z)))
                            {
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.right, GetTextureId(type, Vector3.right), baseColor * GetMaterialFaceShade(type, Vector3.right));
                                solidFacesCount++;
                            }

                            if (IsTransparent(GetBlock(x - 1, y, z)))
                            {
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.left, GetTextureId(type, Vector3.left), baseColor * GetMaterialFaceShade(type, Vector3.left));
                                solidFacesCount++;
                            }
                        }
                    }
                }
            }

            mainMesh.Clear();
            mainMesh.SetVertices(vertices);
            mainMesh.SetTriangles(triangles, 0);
            mainMesh.SetUVs(0, uvs);
            mainMesh.SetColors(colors);
            mainMesh.RecalculateNormals();
            mainMesh.RecalculateBounds();

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mainMesh;
            }

            decMesh.Clear();
            decMesh.SetVertices(decVertices);
            decMesh.SetTriangles(decTriangles, 0);
            decMesh.SetUVs(0, decUvs);
            decMesh.SetColors(decColors);
            decMesh.RecalculateNormals();
            decMesh.RecalculateBounds();

            fluidMesh.Clear();
            fluidMesh.subMeshCount = 2;
            fluidMesh.SetVertices(fluidVertices);
            fluidMesh.SetTriangles(stillTriangles, 0);
            fluidMesh.SetTriangles(flowTriangles, 1);
            fluidMesh.SetUVs(0, fluidUvs);
            fluidMesh.SetColors(fluidColors);
            fluidMesh.RecalculateNormals();
            fluidMesh.RecalculateBounds();

            sw.Stop();
            long memoryDelta = System.GC.GetTotalMemory(false) - startMemory;

            if (WorldManager.EnableWorldGenDiagnostics)
            {
                Debug.Log($"[WaterMesh] chunk={chunkX},{chunkZ} waterCells={waterCells} topFaces={topFaces} sideFaces={sideFaces} bottomFaces={bottomFaces}");
                Debug.Log($"[TerrainMesh] chunk={chunkX},{chunkZ} solidFaces={solidFacesCount}");
                
                if (MineDemo.Utils.ProfilerLogger.Instance != null)
                {
                    MineDemo.Utils.ProfilerLogger.Instance.LogMeshGeneration(
                        $"Chunk_{chunkX}_{chunkZ}", 
                        sw.ElapsedMilliseconds, 
                        vertices.Count + fluidVertices.Count + decVertices.Count,
                        (triangles.Count + flowTriangles.Count + stillTriangles.Count + decTriangles.Count) / 3,
                        memoryDelta,
                        meshMinLocalY,
                        meshMaxLocalY,
                        !hasGeneratedMeshOnce,
                        rebuildsThisSecond
                    );
                }
            }

            if (Time.time - lastRebuildTime > 1.0f)
            {
                rebuildsThisSecond = 1;
                lastRebuildTime = Time.time;
            }
            else
            {
                rebuildsThisSecond++;
            }
            
            hasGeneratedMeshOnce = true;
        }

        private static float GetFaceShade(Vector3 direction)
        {
            if (direction == Vector3.up) return 1.00f;
            if (direction == Vector3.down) return 0.78f;
            if (direction == Vector3.forward || direction == Vector3.back) return 0.93f;
            return 0.88f;
        }

        private static float GetMaterialFaceShade(BlockType type, Vector3 direction)
        {
            if (type == BlockType.Grass)
            {
                if (direction == Vector3.up) return 1.00f;
                if (direction == Vector3.down) return 0.82f;
                return 0.96f;
            }
            return GetFaceShade(direction);
        }

        private void AddFace(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, int x, int y, int z, Vector3 direction, TextureId tex, Color tintColor)
        {
            int startIndex = vertices.Count;
            int worldY = y + WorldBounds.MinBuildY;
            Vector3 pos = new Vector3(x, worldY, z);

            if (direction == Vector3.up)
            {
                vertices.Add(pos + new Vector3(0, 1, 0));
                vertices.Add(pos + new Vector3(0, 1, 1));
                vertices.Add(pos + new Vector3(1, 1, 1));
                vertices.Add(pos + new Vector3(1, 1, 0));
            }
            else if (direction == Vector3.down)
            {
                vertices.Add(pos + new Vector3(0, 0, 1));
                vertices.Add(pos + new Vector3(0, 0, 0));
                vertices.Add(pos + new Vector3(1, 0, 0));
                vertices.Add(pos + new Vector3(1, 0, 1));
            }
            else if (direction == Vector3.forward) // Z+
            {
                vertices.Add(pos + new Vector3(1, 0, 1));
                vertices.Add(pos + new Vector3(1, 1, 1));
                vertices.Add(pos + new Vector3(0, 1, 1));
                vertices.Add(pos + new Vector3(0, 0, 1));
            }
            else if (direction == Vector3.back) // Z-
            {
                vertices.Add(pos + new Vector3(0, 0, 0));
                vertices.Add(pos + new Vector3(0, 1, 0));
                vertices.Add(pos + new Vector3(1, 1, 0));
                vertices.Add(pos + new Vector3(1, 0, 0));
            }
            else if (direction == Vector3.right) // X+
            {
                vertices.Add(pos + new Vector3(1, 0, 0));
                vertices.Add(pos + new Vector3(1, 1, 0));
                vertices.Add(pos + new Vector3(1, 1, 1));
                vertices.Add(pos + new Vector3(1, 0, 1));
            }
            else if (direction == Vector3.left) // X-
            {
                vertices.Add(pos + new Vector3(0, 0, 1));
                vertices.Add(pos + new Vector3(0, 1, 1));
                vertices.Add(pos + new Vector3(0, 1, 0));
                vertices.Add(pos + new Vector3(0, 0, 0));
            }

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);

            // Gắn UV từ Atlas
            UVRect rect = atlasData.GetUVs(tex);
            uvs.Add(rect.bottomLeft);
            uvs.Add(rect.topLeft);
            uvs.Add(rect.topRight);
            uvs.Add(rect.bottomRight);
            
            // Tính toán màu thực tế cho mặt này
            Color actualColor = tintColor;
            
            if (tex == TextureId.GrassSide)
            {
                // Phần đất nền phải giữ màu texture gốc.
                actualColor = Color.white * GetFaceShade(direction);
            }

            for (int i = 0; i < 4; i++) colors.Add(actualColor);

            if (tex == TextureId.GrassSide)
            {
                int overlayStartIndex = vertices.Count;
                Vector3 overlayPos = pos + direction * 0.001f;

                if (direction == Vector3.up)
                {
                    vertices.Add(overlayPos + new Vector3(0, 1, 0));
                    vertices.Add(overlayPos + new Vector3(0, 1, 1));
                    vertices.Add(overlayPos + new Vector3(1, 1, 1));
                    vertices.Add(overlayPos + new Vector3(1, 1, 0));
                }
                else if (direction == Vector3.down)
                {
                    vertices.Add(overlayPos + new Vector3(0, 0, 1));
                    vertices.Add(overlayPos + new Vector3(0, 0, 0));
                    vertices.Add(overlayPos + new Vector3(1, 0, 0));
                    vertices.Add(overlayPos + new Vector3(1, 0, 1));
                }
                else if (direction == Vector3.forward) // Z+
                {
                    vertices.Add(overlayPos + new Vector3(1, 0, 1));
                    vertices.Add(overlayPos + new Vector3(1, 1, 1));
                    vertices.Add(overlayPos + new Vector3(0, 1, 1));
                    vertices.Add(overlayPos + new Vector3(0, 0, 1));
                }
                else if (direction == Vector3.back) // Z-
                {
                    vertices.Add(overlayPos + new Vector3(0, 0, 0));
                    vertices.Add(overlayPos + new Vector3(0, 1, 0));
                    vertices.Add(overlayPos + new Vector3(1, 1, 0));
                    vertices.Add(overlayPos + new Vector3(1, 0, 0));
                }
                else if (direction == Vector3.right) // X+
                {
                    vertices.Add(overlayPos + new Vector3(1, 0, 0));
                    vertices.Add(overlayPos + new Vector3(1, 1, 0));
                    vertices.Add(overlayPos + new Vector3(1, 1, 1));
                    vertices.Add(overlayPos + new Vector3(1, 0, 1));
                }
                else if (direction == Vector3.left) // X-
                {
                    vertices.Add(overlayPos + new Vector3(0, 0, 1));
                    vertices.Add(overlayPos + new Vector3(0, 1, 1));
                    vertices.Add(overlayPos + new Vector3(0, 1, 0));
                    vertices.Add(overlayPos + new Vector3(0, 0, 0));
                }

                triangles.Add(overlayStartIndex);
                triangles.Add(overlayStartIndex + 1);
                triangles.Add(overlayStartIndex + 2);
                triangles.Add(overlayStartIndex);
                triangles.Add(overlayStartIndex + 2);
                triangles.Add(overlayStartIndex + 3);

                UVRect overlayRect = atlasData.GetUVs(TextureId.GrassSideOverlay);
                uvs.Add(overlayRect.bottomLeft);
                uvs.Add(overlayRect.topLeft);
                uvs.Add(overlayRect.topRight);
                uvs.Add(overlayRect.bottomRight);

                Color overlayColor = tintColor; // tintColor đã bao gồm baseColor * GetMaterialFaceShade ở hàm GenerateMesh
                for (int i = 0; i < 4; i++) colors.Add(overlayColor);
            }
        }

        private void AddCrossedQuads(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, int x, int y, int z, TextureId tex)
        {
            UVRect rect = atlasData.GetUVs(tex);
            Color tint = Color.white * GetMaterialFaceShade(BlockType.Grass, Vector3.up);

            // Quad 1 (Diagonal /)
            int startIndex = vertices.Count;
            int worldY = y + WorldBounds.MinBuildY;
            vertices.Add(new Vector3(x, worldY, z));
            vertices.Add(new Vector3(x, worldY + 1, z));
            vertices.Add(new Vector3(x + 1, worldY + 1, z + 1));
            vertices.Add(new Vector3(x + 1, worldY, z + 1));
            
            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);

            // Mặt sau
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 3);
            triangles.Add(startIndex + 2);

            uvs.Add(rect.bottomLeft);
            uvs.Add(rect.topLeft);
            uvs.Add(rect.topRight);
            uvs.Add(rect.bottomRight);

            for (int i = 0; i < 4; i++) colors.Add(tint);

            // Quad 2 (Diagonal \)
            startIndex = vertices.Count;
            vertices.Add(new Vector3(x + 1, worldY, z));
            vertices.Add(new Vector3(x + 1, worldY + 1, z));
            vertices.Add(new Vector3(x, worldY + 1, z + 1));
            vertices.Add(new Vector3(x, worldY, z + 1));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);

            // Mặt sau
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 3);
            triangles.Add(startIndex + 2);

            uvs.Add(rect.bottomLeft);
            uvs.Add(rect.topLeft);
            uvs.Add(rect.topRight);
            uvs.Add(rect.bottomRight);

            for (int i = 0; i < 4; i++) colors.Add(tint);
        }

        private float GetVisibleSurfaceHeight(byte level)
        {
            if (level >= 8) return 0.998f;
            if (level <= 0) return 0.0f;
            return Mathf.Clamp01(level / 8.0f) * 0.90f;
        }

        private float GetCellTopHeight(bool hasWaterAbove, byte level)
        {
            return hasWaterAbove ? 1.0f : GetVisibleSurfaceHeight(level);
        }

        private bool ShouldRenderWaterFace(
            int x,
            int y,
            int z,
            byte currentLevel,
            Vector3 direction)
        {
            BlockType neighborType = GetBlock(x, y, z);
        
            if (neighborType == BlockType.WaterSource ||
                neighborType == BlockType.WaterFlow)
            {
                byte neighborLevel = GetWaterLevelForMesh(x, y, z);
                return neighborLevel < currentLevel;
            }
        
            return neighborType == BlockType.Air ||
                   neighborType == BlockType.OakLeaves;
        }

        private void AddWaterFace(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, int x, int y, int z, Vector3 direction, float waterHeight)
        {
            int startIndex = vertices.Count;
            int worldY = y + WorldBounds.MinBuildY;
            Vector3 pos = new Vector3(x, worldY, z);

            if (direction == Vector3.up)
            {
                vertices.Add(pos + new Vector3(0, waterHeight, 0));
                vertices.Add(pos + new Vector3(0, waterHeight, 1));
                vertices.Add(pos + new Vector3(1, waterHeight, 1));
                vertices.Add(pos + new Vector3(1, waterHeight, 0));
            }
            else if (direction == Vector3.down)
            {
                vertices.Add(pos + new Vector3(0, 0, 1));
                vertices.Add(pos + new Vector3(0, 0, 0));
                vertices.Add(pos + new Vector3(1, 0, 0));
                vertices.Add(pos + new Vector3(1, 0, 1));
            }
            else if (direction == Vector3.forward) // Z+
            {
                vertices.Add(pos + new Vector3(1, 0, 1));
                vertices.Add(pos + new Vector3(1, waterHeight, 1));
                vertices.Add(pos + new Vector3(0, waterHeight, 1));
                vertices.Add(pos + new Vector3(0, 0, 1));
            }
            else if (direction == Vector3.back) // Z-
            {
                vertices.Add(pos + new Vector3(0, 0, 0));
                vertices.Add(pos + new Vector3(0, waterHeight, 0));
                vertices.Add(pos + new Vector3(1, waterHeight, 0));
                vertices.Add(pos + new Vector3(1, 0, 0));
            }
            else if (direction == Vector3.right) // X+
            {
                vertices.Add(pos + new Vector3(1, 0, 0));
                vertices.Add(pos + new Vector3(1, waterHeight, 0));
                vertices.Add(pos + new Vector3(1, waterHeight, 1));
                vertices.Add(pos + new Vector3(1, 0, 1));
            }
            else if (direction == Vector3.left) // X-
            {
                vertices.Add(pos + new Vector3(0, 0, 1));
                vertices.Add(pos + new Vector3(0, waterHeight, 1));
                vertices.Add(pos + new Vector3(0, waterHeight, 0));
                vertices.Add(pos + new Vector3(0, 0, 0));
            }

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);

            // Gắn UV chuẩn cho từng hướng (không dùng chung 1 mảng để tránh lộn ngược/gương mặt nước)
            if (direction == Vector3.up)
            {
                uvs.Add(new Vector2(0, 0)); // 0,0,0
                uvs.Add(new Vector2(0, 1)); // 0,0,1
                uvs.Add(new Vector2(1, 1)); // 1,0,1
                uvs.Add(new Vector2(1, 0)); // 1,0,0
            }
            else if (direction == Vector3.down)
            {
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
            }
            else if (direction == Vector3.forward) // Z+ (nhìn từ ngoài vào, x từ trái 0->phải 1)
            {
                uvs.Add(new Vector2(1, 0)); // (1,0,1) -> dưới phải
                uvs.Add(new Vector2(1, waterHeight)); // (1,y,1) -> trên phải
                uvs.Add(new Vector2(0, waterHeight)); // (0,y,1) -> trên trái
                uvs.Add(new Vector2(0, 0)); // (0,0,1) -> dưới trái
            }
            else if (direction == Vector3.back) // Z- (nhìn từ ngoài vào, x từ trái 1->phải 0)
            {
                uvs.Add(new Vector2(0, 0)); // (0,0,0) -> dưới trái
                uvs.Add(new Vector2(0, waterHeight)); // (0,y,0) -> trên trái
                uvs.Add(new Vector2(1, waterHeight)); // (1,y,0) -> trên phải
                uvs.Add(new Vector2(1, 0)); // (1,0,0) -> dưới phải
            }
            else if (direction == Vector3.right) // X+ (nhìn từ ngoài vào, z từ trái 0->phải 1)
            {
                uvs.Add(new Vector2(0, 0)); // (1,0,0) -> dưới trái
                uvs.Add(new Vector2(0, waterHeight)); // (1,y,0) -> trên trái
                uvs.Add(new Vector2(1, waterHeight)); // (1,y,1) -> trên phải
                uvs.Add(new Vector2(1, 0)); // (1,0,1) -> dưới phải
            }
            else if (direction == Vector3.left) // X- (nhìn từ ngoài vào, z từ trái 1->phải 0)
            {
                uvs.Add(new Vector2(1, 0)); // (0,0,1) -> dưới phải
                uvs.Add(new Vector2(1, waterHeight)); // (0,y,1) -> trên phải
                uvs.Add(new Vector2(0, waterHeight)); // (0,y,0) -> trên trái
                uvs.Add(new Vector2(0, 0)); // (0,0,0) -> dưới trái
            }

            for (int i = 0; i < 4; i++) colors.Add(Color.white);
        }
    }
}
