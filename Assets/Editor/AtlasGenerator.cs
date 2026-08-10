using UnityEngine;
using UnityEditor;
using System.IO;
using MineDemo.Blocks;

public class AtlasGenerator
{
    private static Color grassTint = new Color(0.48f, 0.70f, 0.25f, 1f); // Màu xanh cỏ

    [MenuItem("MineDemo/Generate Atlas")]
    public static void Generate()
    {
        TempTextureGenerator.Generate(); // Đảm bảo tạo sand và bedrock trước
        
        string path = "Assets/Textures/TempMinecraft/";
        
        Texture2D texDirt = LoadAndReadable(path + "dirt.png");
        Texture2D texStone = LoadAndReadable(path + "stone.png");
        Texture2D texGrassTopBase = LoadAndReadable(path + "grass_block_top.png");
        Texture2D texGrassSideBase = LoadAndReadable(path + "grass_block_side.png");
        Texture2D texGrassSideOverlay = LoadAndReadable(path + "grass_block_side_overlay.png");

        Texture2D texOakLogSide = LoadAndReadable(path + "oak_log.png");
        Texture2D texOakLogTop = LoadAndReadable(path + "oak_log_top.png");
        Texture2D texOakLeavesBase = LoadAndReadable(path + "oak_leaves.png");
        Texture2D texShortGrassBase = LoadAndReadable(path + "short_grass.png");

        Texture2D texSand = LoadAndReadable(path + "sand.png");
        Texture2D texBedrock = LoadAndReadable(path + "bedrock.png");

        if (texDirt == null || texStone == null || texGrassTopBase == null || texGrassSideBase == null || texGrassSideOverlay == null ||
            texOakLogSide == null || texOakLogTop == null || texOakLeavesBase == null || texShortGrassBase == null ||
            texSand == null || texBedrock == null)
        {
            Debug.LogError("Thiếu file texture gốc! Vui lòng kiểm tra lại Assets/Textures/TempMinecraft/");
            return;
        }

        // 1. Không tint Grass Top (để C# tự tint bằng Vertex Color)
        Texture2D texGrassTop = texGrassTopBase; 

        // 2. Tách riêng Grass Side Base và Grass Side Overlay (để tint bằng C#)
        Texture2D texGrassSide = texGrassSideBase;
        Texture2D texGrassSideOverlayFinal = texGrassSideOverlay;

        // 2.5 Tint Leaves và Short Grass
        Texture2D texOakLeaves = new Texture2D(texOakLeavesBase.width, texOakLeavesBase.height, TextureFormat.RGBA32, false);
        for (int y = 0; y < texOakLeaves.height; y++)
        {
            for (int x = 0; x < texOakLeaves.width; x++)
            {
                Color c = texOakLeavesBase.GetPixel(x, y);
                texOakLeaves.SetPixel(x, y, c * grassTint);
            }
        }
        texOakLeaves.Apply();

        Texture2D texShortGrass = new Texture2D(texShortGrassBase.width, texShortGrassBase.height, TextureFormat.RGBA32, false);
        for (int y = 0; y < texShortGrass.height; y++)
        {
            for (int x = 0; x < texShortGrass.width; x++)
            {
                Color c = texShortGrassBase.GetPixel(x, y);
                texShortGrass.SetPixel(x, y, c * grassTint);
            }
        }
        texShortGrass.Apply();

        // 3. Pack Atlas (Tạo Atlas với 2px padding để chống bleed màu)
        Texture2D atlas = new Texture2D(8192, 8192);
        Texture2D[] texturesToPack = new Texture2D[] { 
            texDirt, texStone, texGrassTop, texGrassSide, 
            texOakLogSide, texOakLogTop, texOakLeaves, texShortGrass,
            texSand, texBedrock, texGrassSideOverlayFinal
        };
        Rect[] rects = atlas.PackTextures(texturesToPack, 2, 8192); 

        // 4. Save Atlas
        byte[] bytes = atlas.EncodeToPNG();
        string atlasPath = "Assets/Textures/TextureAtlas.png";
        File.WriteAllBytes(atlasPath, bytes);
        AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);

        // 5. Cấu hình Atlas Importer
        TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (importer != null)
        {
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // 6. Ghi dữ liệu UV vào ScriptableObject
        AtlasData atlasData = AssetDatabase.LoadAssetAtPath<AtlasData>("Assets/Scripts/Blocks/AtlasData.asset");
        if (atlasData == null)
        {
            atlasData = ScriptableObject.CreateInstance<AtlasData>();
            AssetDatabase.CreateAsset(atlasData, "Assets/Scripts/Blocks/AtlasData.asset");
        }

        atlasData.keys.Clear();
        atlasData.values.Clear();

        TextureId[] ids = new TextureId[] { 
            TextureId.Dirt, TextureId.Stone, TextureId.GrassTop, TextureId.GrassSide, 
            TextureId.OakLogSide, TextureId.OakLogTop, TextureId.OakLeaves, TextureId.ShortGrass,
            TextureId.Sand, TextureId.Bedrock, TextureId.GrassSideOverlay
        };
        for (int i = 0; i < ids.Length; i++)
        {
            Rect r = rects[i];
            UVRect uv = new UVRect();
            uv.bottomLeft = new Vector2(r.xMin, r.yMin);
            uv.bottomRight = new Vector2(r.xMax, r.yMin);
            uv.topRight = new Vector2(r.xMax, r.yMax);
            uv.topLeft = new Vector2(r.xMin, r.yMax);
            
            atlasData.keys.Add(ids[i]);
            atlasData.values.Add(uv);
        }

        EditorUtility.SetDirty(atlasData);
        AssetDatabase.SaveAssets();

        Debug.Log("Đã tạo Texture Atlas và cập nhật AtlasData thành công!");
    }

    private static Texture2D LoadAndReadable(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
