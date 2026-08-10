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

        private MeshFilter meshFilter;
        private MeshCollider meshCollider;

        public int chunkX { get; private set; }
        public int chunkZ { get; private set; }
        public WorldManager worldManager;
        public AtlasData atlasData;
        
        private MeshFilter decorationFilter;
        private MeshFilter fluidFilter;
        private MeshRenderer fluidRenderer;

        void Awake()
        {
            blocks = new BlockType[Width * Height * Depth];
            waterLevels = new byte[Width * Height * Depth];
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();
            
            GameObject decObj = new GameObject("Decoration");
            decObj.transform.parent = this.transform;
            decObj.transform.localPosition = Vector3.zero;
            decorationFilter = decObj.AddComponent<MeshFilter>();
            MeshRenderer decRenderer = decObj.AddComponent<MeshRenderer>();
            decRenderer.material = GetComponent<MeshRenderer>().sharedMaterial;

            GameObject fluidObj = new GameObject("FluidMesh");
            fluidObj.transform.parent = this.transform;
            fluidObj.transform.localPosition = Vector3.zero;
            fluidFilter = fluidObj.AddComponent<MeshFilter>();
            fluidRenderer = fluidObj.AddComponent<MeshRenderer>();
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
                        }
                    }
                }
            }

            // Sinh cây cho chunk này
            TreeGenerator.GenerateChunkTrees(this, 12345); // Dùng seed tĩnh tạm thời

            MeshRenderer decRenderer = decorationFilter.GetComponent<MeshRenderer>();
            decRenderer.material = GetComponent<MeshRenderer>().sharedMaterial;
            
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
            
            if (biome == BiomeType.Forest) return new Color(0.25f, 0.55f, 0.15f);
            if (biome == BiomeType.Mountains) return new Color(0.31f, 0.59f, 0.19f); // Tối hơn
            if (biome == BiomeType.Hills) return new Color(0.33f, 0.63f, 0.18f);
            
            float t = Mathf.InverseLerp(62f, 150f, worldY);
            return Color.Lerp(baseGrass, new Color(0.31f, 0.59f, 0.19f), t);
        }

        private void GenerateTerrain()
        {
            TerrainGenerator.GenerateChunkData(chunkX, chunkZ, Width, Height, Depth, out blocks, out waterLevels);
        }

        private void SetBlock(int x, int y, int z, BlockType type)
        {
            blocks[x + Width * (y + Height * z)] = type;
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
                    return worldManager.GetExpectedBlock(worldX, worldY, worldZ);
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
        }

        public byte GetWaterLevelLocal(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
                return 0;
            return waterLevels[x + Width * (y + Height * z)];
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
                SetBlock(localX, localY, localZ, newType);
                if (newType == BlockType.Air)
                {
                    SetWaterLevelLocal(localX, localY, localZ, 0); // Xoá nước nếu có
                }
                
                // Clear toàn bộ mesh cũ trước khi generate
                meshFilter.mesh.Clear();
                fluidFilter.mesh.Clear();
                decorationFilter.mesh.Clear();
                if (meshCollider != null && meshCollider.sharedMesh != null)
                    meshCollider.sharedMesh.Clear();

                GenerateMesh();
            }
        }

        private TextureId GetTextureId(BlockType type, Vector3 direction)
        {
            if (type == BlockType.Dirt) return TextureId.Dirt;
            if (type == BlockType.Stone) return TextureId.Stone;
            if (type == BlockType.Sand) return TextureId.Sand;
            if (type == BlockType.Bedrock) return TextureId.Bedrock;
            if (type == BlockType.Grass)
            {
                if (direction == Vector3.up) return TextureId.GrassTop;
                if (direction == Vector3.down) return TextureId.Dirt;
                return TextureId.GrassSide;
            }
            if (type == BlockType.OakLog)
            {
                if (direction == Vector3.up || direction == Vector3.down) return TextureId.OakLogTop;
                return TextureId.OakLogSide;
            }
            if (type == BlockType.OakLeaves) return TextureId.OakLeaves;
            
            return TextureId.Dirt;
        }

        private bool IsTransparent(BlockType type)
        {
            // Các khối cho phép nhìn xuyên qua (Cần vẽ mặt cho khối đứng cạnh nó)
            return type == BlockType.Air || type == BlockType.OakLeaves;
        }

        public void GenerateMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            List<Vector3> decVertices = new List<Vector3>();
            List<int> decTriangles = new List<int>();
            List<Vector2> decUvs = new List<Vector2>();
            List<Color> decColors = new List<Color>();

            List<Vector3> fluidVertices = new List<Vector3>();
            List<int> stillTriangles = new List<int>();
            List<int> flowTriangles = new List<int>();
            List<Vector2> fluidUvs = new List<Vector2>();
            List<Color> fluidColors = new List<Color>();

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        BlockType type = GetBlock(x, y, z);

                        if (type == BlockType.WaterSource || type == BlockType.WaterFlow)
                        {
                            byte level = GetWaterLevelLocal(x, y, z);
                            float waterHeight = GetWaterHeight(level);

                            BlockType upBlock = GetBlock(x, y + 1, z);
                            bool hasWaterAbove = upBlock == BlockType.WaterSource || upBlock == BlockType.WaterFlow;
                            
                            List<int> targetTriangles = (type == BlockType.WaterSource) ? stillTriangles : flowTriangles;

                            if (!hasWaterAbove)
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.up, waterHeight); 

                            if (ShouldRenderWaterFace(x, y - 1, z, level, Vector3.down))
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.down, waterHeight);

                            if (ShouldRenderWaterFace(x, y, z + 1, level, Vector3.forward))
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.forward, waterHeight);

                            if (ShouldRenderWaterFace(x, y, z - 1, level, Vector3.back))
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.back, waterHeight);

                            if (ShouldRenderWaterFace(x + 1, y, z, level, Vector3.right))
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.right, waterHeight);

                            if (ShouldRenderWaterFace(x - 1, y, z, level, Vector3.left))
                                AddWaterFace(fluidVertices, targetTriangles, fluidUvs, fluidColors, x, y, z, Vector3.left, waterHeight);
                        }
                        else if (type != BlockType.Air)
                        {
                            Color faceColor = Color.white;
                            if (type == BlockType.Grass)
                            {
                                int worldX = chunkX * Width + x;
                                int worldY = y + WorldBounds.MinBuildY;
                                int worldZ = chunkZ * Depth + z;
                                faceColor = GetGrassTint(worldX, worldY, worldZ);
                            }

                            if (IsTransparent(GetBlock(x, y + 1, z)))
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.up, GetTextureId(type, Vector3.up), faceColor);

                            if (IsTransparent(GetBlock(x, y - 1, z)))
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.down, GetTextureId(type, Vector3.down), faceColor);

                            if (IsTransparent(GetBlock(x, y, z + 1)))
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.forward, GetTextureId(type, Vector3.forward), faceColor);

                            if (IsTransparent(GetBlock(x, y, z - 1)))
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.back, GetTextureId(type, Vector3.back), faceColor);

                            if (IsTransparent(GetBlock(x + 1, y, z)))
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.right, GetTextureId(type, Vector3.right), faceColor);

                            if (IsTransparent(GetBlock(x - 1, y, z)))
                                AddFace(vertices, triangles, uvs, colors, x, y, z, Vector3.left, GetTextureId(type, Vector3.left), faceColor);

                            // Sinh cỏ thấp ngẫu nhiên trên khối Grass
                            if (type == BlockType.Grass && GetBlock(x, y + 1, z) == BlockType.Air)
                            {
                                int worldX = chunkX * Width + x;
                                int worldZ = chunkZ * Depth + z;
                                int hash = (worldX * 73856093 ^ worldZ * 19349663) % 100;
                                if (hash < 0) hash = -hash;
                                
                                if (hash < 8) // Tỉ lệ 8%
                                {
                                    AddCrossedQuads(decVertices, decTriangles, decUvs, decColors, x, y + 1, z, TextureId.ShortGrass);
                                }
                            }
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();
            mesh.RecalculateNormals();

            meshFilter.mesh = mesh;
            if (meshCollider != null)
                meshCollider.sharedMesh = mesh;

            Mesh decMesh = new Mesh();
            decMesh.vertices = decVertices.ToArray();
            decMesh.triangles = decTriangles.ToArray();
            decMesh.uv = decUvs.ToArray();
            decMesh.colors = decColors.ToArray();
            decMesh.RecalculateNormals();
            
            decorationFilter.mesh = decMesh;

            Mesh fluidMesh = new Mesh();
            fluidMesh.vertices = fluidVertices.ToArray();
            fluidMesh.subMeshCount = 2; // 0: Still, 1: Flow
            fluidMesh.SetTriangles(stillTriangles.ToArray(), 0);
            fluidMesh.SetTriangles(flowTriangles.ToArray(), 1);
            fluidMesh.uv = fluidUvs.ToArray();
            fluidMesh.colors = fluidColors.ToArray();
            fluidMesh.RecalculateNormals();

            fluidFilter.mesh = fluidMesh;
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
            Color actualColor = Color.white;
            if (tex == TextureId.GrassTop || tex == TextureId.OakLeaves || tex == TextureId.ShortGrass) 
                actualColor = tintColor;
            else if (tex == TextureId.GrassSideOverlay) 
                actualColor = tintColor; // Grass side sẽ được tách thành 2 quad

            for (int i = 0; i < 4; i++) colors.Add(actualColor);

            // Nếu đây là GrassSide, vẽ thêm 1 quad GrassSideOverlay đè lên với khoảng cách rất nhỏ chống Z-Fighting
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

                for (int i = 0; i < 4; i++) colors.Add(tintColor);
            }
        }

        private void AddCrossedQuads(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, int x, int y, int z, TextureId tex)
        {
            UVRect rect = atlasData.GetUVs(tex);

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

            for (int i = 0; i < 4; i++) colors.Add(Color.white);

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

            for (int i = 0; i < 4; i++) colors.Add(Color.white);
        }

        private float GetWaterHeight(byte level)
        {
            if (level >= 8) return 0.90f;
            if (level <= 0) return 0.0f;
            return Mathf.Clamp01(level / 8.0f) * 0.90f;
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
                byte neighborLevel = GetWaterLevelLocal(x, y, z);
        
                // Nước cao hơn hoặc bằng che mặt chung.
                // Nước thấp hơn vẫn cần vẽ mặt để tạo bậc/thác.
                return neighborLevel < currentLevel;
            }
        
            // Mặt bên nước tiếp xúc với block đặc phải được vẽ ở bờ.
            if (direction != Vector3.down)
                return true;
        
            // Không cần vẽ mặt đáy khi bên dưới là block đặc.
            // Chỉ vẽ đáy nếu nước đang treo trên Air/Leaves.
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

            // Gắn UV toàn tấm (1 khối nước xài nguyên 1 ảnh từ frame hiện tại)
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0));

            for (int i = 0; i < 4; i++) colors.Add(Color.white);
        }
    }
}
