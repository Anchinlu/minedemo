using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using MineDemo.Blocks;

public class AtlasGenerator
{
    private static Color grassTint = new Color(0.48f, 0.70f, 0.25f, 1f); // Màu xanh cỏ

    [MenuItem("MineDemo/Generate Atlas")]
    public static void Generate()
    {
        TempTextureGenerator.Generate(); // Đảm bảo tạo sand và bedrock trước
        
        string path = "Assets/Textures/TempMinecraft/";
        
        // Define all standard textures and their IDs
        var textures = new List<Texture2D>();
        var ids = new List<TextureId>();

        void Add(TextureId id, string file)
        {
            Texture2D tex = LoadAndReadable(path + file);
            if (tex == null)
            {
                Debug.LogError($"Thiếu file texture gốc: {file}! Vui lòng kiểm tra lại {path}");
            }
            textures.Add(tex);
            ids.Add(id);
        }

        void AddTinted(TextureId id, string file)
        {
            Texture2D tex = LoadAndReadable(path + file);
            if (tex == null)
            {
                Debug.LogError($"Thiếu file texture gốc: {file}! Vui lòng kiểm tra lại {path}");
                textures.Add(null);
                ids.Add(id);
                return;
            }
            textures.Add(TintTexture(tex, grassTint));
            ids.Add(id);
        }

        // Base Textures
        Add(TextureId.Dirt, "dirt.png");
        Add(TextureId.Stone, "stone.png");
        Add(TextureId.GrassTop, "grass_block_top.png"); // Không tint để C# tự tint
        Add(TextureId.GrassSide, "grass_block_side.png");
        Add(TextureId.OakLogSide, "oak_log.png");
        Add(TextureId.OakLogTop, "oak_log_top.png");
        AddTinted(TextureId.OakLeaves, "oak_leaves.png");
        Add(TextureId.BirchLogSide, "birch_log.png");
        Add(TextureId.BirchLogTop, "birch_log_top.png");
        Add(TextureId.BirchLeaves, "birch_leaves.png");
        AddTinted(TextureId.ShortGrass, "short_grass.png");
        Add(TextureId.Sand, "sand.png");
        Add(TextureId.Bedrock, "bedrock.png");
        Add(TextureId.GrassSideOverlay, "grass_block_side_overlay.png");

        // Phase 1 Textures
        Add(TextureId.Gravel, "gravel.png");
        Add(TextureId.Cobblestone, "cobblestone.png");
        Add(TextureId.Deepslate, "deepslate.png");
        Add(TextureId.DeepslateTop, "deepslate_top.png");
        Add(TextureId.CoarseDirt, "coarse_dirt.png");
        Add(TextureId.Clay, "clay.png");

        // Phase 2 Textures
        Add(TextureId.Sandstone, "sandstone.png");
        Add(TextureId.SandstoneTop, "sandstone_top.png");
        Add(TextureId.SandstoneBottom, "sandstone_bottom.png");
        Add(TextureId.Snow, "snow.png");
        Add(TextureId.GrassSnowSide, "grass_block_snow.png");
        Add(TextureId.Ice, "ice.png");
        Add(TextureId.PackedIce, "packed_ice.png");
        Add(TextureId.Mud, "mud.png");

        // Phase 3 Textures (Flora)
        Add(TextureId.Poppy, "poppy.png");
        Add(TextureId.Dandelion, "dandelion.png");
        Add(TextureId.BlueOrchid, "blue_orchid.png");
        Add(TextureId.Allium, "allium.png");
        Add(TextureId.AzureBluet, "azure_bluet.png");
        Add(TextureId.RedTulip, "red_tulip.png");
        Add(TextureId.OrangeTulip, "orange_tulip.png");
        Add(TextureId.WhiteTulip, "white_tulip.png");
        Add(TextureId.PinkTulip, "pink_tulip.png");
        Add(TextureId.OxeyeDaisy, "oxeye_daisy.png");
        Add(TextureId.Cornflower, "cornflower.png");
        
        AddTinted(TextureId.TallGrassLower, "tall_grass_bottom.png");
        AddTinted(TextureId.TallGrassUpper, "tall_grass_top.png");
        AddTinted(TextureId.Fern, "fern.png");
        
        Add(TextureId.ShortDryGrass, "short_dry_grass.png");
        Add(TextureId.TallDryGrassLower, "tall_dry_grass.png");
        Add(TextureId.TallDryGrassUpper, "tall_dry_grass.png");

        if (textures.Contains(null))
        {
            Debug.LogError("Dừng quá trình tạo Atlas vì có file bị thiếu!");
            return;
        }

        // Pack Atlas (Tạo Atlas với 2px padding để chống bleed màu)
        Texture2D atlas = new Texture2D(8192, 8192);
        Rect[] rects = atlas.PackTextures(textures.ToArray(), 2, 8192); 

        // Save Atlas
        byte[] bytes = atlas.EncodeToPNG();
        string atlasPath = "Assets/Textures/TextureAtlas.png";
        File.WriteAllBytes(atlasPath, bytes);
        AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);

        // Cấu hình Atlas Importer
        TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (importer != null)
        {
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Ghi dữ liệu UV vào ScriptableObject
        AtlasData atlasData = AssetDatabase.LoadAssetAtPath<AtlasData>("Assets/Scripts/Blocks/AtlasData.asset");
        if (atlasData == null)
        {
            atlasData = ScriptableObject.CreateInstance<AtlasData>();
            AssetDatabase.CreateAsset(atlasData, "Assets/Scripts/Blocks/AtlasData.asset");
        }

        atlasData.keys.Clear();
        atlasData.values.Clear();

        Debug.Assert(ids.Count == textures.Count, "Số lượng TextureId không khớp với số lượng Texture2D pack!");

        for (int i = 0; i < ids.Count; i++)
        {
            UVRect rect = new UVRect
            {
                bottomLeft = new Vector2(rects[i].xMin, rects[i].yMin),
                bottomRight = new Vector2(rects[i].xMax, rects[i].yMin),
                topRight = new Vector2(rects[i].xMax, rects[i].yMax),
                topLeft = new Vector2(rects[i].xMin, rects[i].yMax)
            };
            
            atlasData.keys.Add(ids[i]);
            atlasData.values.Add(rect);
        }

        EditorUtility.SetDirty(atlasData);
        AssetDatabase.SaveAssets();

        Debug.Log($"Atlas generated successfully! Packed {ids.Count} textures.");
    }

    private static Texture2D TintTexture(Texture2D baseTex, Color tint)
    {
        Texture2D newTex = new Texture2D(baseTex.width, baseTex.height, TextureFormat.RGBA32, false);
        for (int y = 0; y < newTex.height; y++)
        {
            for (int x = 0; x < newTex.width; x++)
            {
                Color c = baseTex.GetPixel(x, y);
                newTex.SetPixel(x, y, c * tint);
            }
        }
        newTex.Apply();
        return newTex;
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
