using UnityEngine;
using UnityEditor;
using System.IO;

public class TempTextureGenerator
{
    [MenuItem("MineDemo/Generate Missing Textures")]
    public static void Generate()
    {
        string path = "Assets/Textures/TempMinecraft/";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        CreateTexture(path + "sand.png", new Color(0.85f, 0.8f, 0.6f), new Color(0.9f, 0.85f, 0.65f));
        CreateTexture(path + "bedrock.png", new Color(0.15f, 0.15f, 0.15f), new Color(0.25f, 0.25f, 0.25f));

        Debug.Log("Đã tạo sand.png và bedrock.png!");
    }

    private static void CreateTexture(string path, Color c1, Color c2)
    {
        if (File.Exists(path)) return;

        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.5f, y * 0.5f);
                tex.SetPixel(x, y, Color.Lerp(c1, c2, noise));
            }
        }
        tex.Apply();
        
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
