using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using MineDemo.World;
using System.Text.RegularExpressions;

public class WaterAnimationImporter : EditorWindow
{
    [MenuItem("MineDemo/Import Water Animations")]
    public static void ImportWaterAnimations()
    {
        string stillPath = Path.Combine(Application.dataPath, "Textures", "TempMinecraft", "water_still.png");
        string flowPath = Path.Combine(Application.dataPath, "Textures", "TempMinecraft", "water_flow.png");
        
        string outputDir = Path.Combine(Application.dataPath, "Resources", "TempMinecraft", "WaterFrames");
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        WaterAnimationData data = ScriptableObject.CreateInstance<WaterAnimationData>();

        int numStill = ProcessTexture(stillPath, outputDir, "water_still", out data.stillFrameSequence, out data.stillFrameTime);
        int numFlow = ProcessTexture(flowPath, outputDir, "water_flow", out data.flowFrameSequence, out data.flowFrameTime);

        AssetDatabase.Refresh();

        data.stillFrames = new Texture2D[numStill];
        for (int i = 0; i < numStill; i++)
        {
            string assetPath = $"Assets/Resources/TempMinecraft/WaterFrames/water_still_{i:00}.png";
            ConfigureTexture(assetPath);
            data.stillFrames[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        data.flowFrames = new Texture2D[numFlow];
        for (int i = 0; i < numFlow; i++)
        {
            string assetPath = $"Assets/Resources/TempMinecraft/WaterFrames/water_flow_{i:00}.png";
            ConfigureTexture(assetPath);
            data.flowFrames[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        string dataDir = "Assets/Resources/TempMinecraft";
        if (!AssetDatabase.IsValidFolder(dataDir)) AssetDatabase.CreateFolder("Assets/Resources", "TempMinecraft");

        string dataPath = "Assets/Resources/TempMinecraft/WaterAnimationData.asset";
        AssetDatabase.CreateAsset(data, dataPath);
        AssetDatabase.SaveAssets();

        Debug.Log("Water Animation Imported Successfully!");
    }

    private static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    private static int ProcessTexture(string path, string outputDir, string prefix, out int[] sequence, out float frameTime)
    {
        if (!File.Exists(path))
        {
            sequence = new int[0];
            frameTime = 0.1f;
            return 0;
        }

        Texture2D source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(path));

        int width = source.width;
        int height = source.height;
        int numFrames = height / width;
        
        for (int i = 0; i < numFrames; i++)
        {
            Texture2D frame = new Texture2D(width, width);
            // Ảnh gốc 16x512, frame đầu tiên thường nằm ở trên cùng. Trong mảng GetPixels, toạ độ 0,0 là góc dưới cùng bên trái.
            frame.SetPixels(source.GetPixels(0, height - (i + 1) * width, width, width));
            frame.Apply();

            string framePath = Path.Combine(outputDir, $"{prefix}_{i:00}.png");
            File.WriteAllBytes(framePath, frame.EncodeToPNG());
        }

        // Check mcmeta
        string mcmetaPath = path + ".mcmeta";
        sequence = new int[numFrames];
        for (int i = 0; i < numFrames; i++) sequence[i] = i;
        frameTime = 0.1f; // Mặc định 2 ticks * 0.05s

        if (File.Exists(mcmetaPath))
        {
            string content = File.ReadAllText(mcmetaPath);
            Match timeMatch = Regex.Match(content, @"""frametime""\s*:\s*(\d+)");
            if (timeMatch.Success)
            {
                frameTime = int.Parse(timeMatch.Groups[1].Value) * 0.05f;
            }

            Match framesMatch = Regex.Match(content, @"""frames""\s*:\s*\[([\d\s,]+)\]");
            if (framesMatch.Success)
            {
                string[] numbers = framesMatch.Groups[1].Value.Split(',');
                List<int> seq = new List<int>();
                foreach (string num in numbers)
                {
                    if (int.TryParse(num.Trim(), out int n))
                    {
                        seq.Add(n);
                    }
                }
                if (seq.Count > 0)
                {
                    sequence = seq.ToArray();
                }
            }
        }

        return numFrames;
    }
}
