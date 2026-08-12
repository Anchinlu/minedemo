using UnityEngine;
using System.Diagnostics;
using System.Collections.Generic;

namespace MineDemo.Utils
{
    public class ProfilerLogger : MonoBehaviour
    {
        public static ProfilerLogger Instance;

        private float deltaTime = 0.0f;
        private float timer = 0.0f;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private float currentFps = 0f;

        void Update()
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            timer += Time.unscaledDeltaTime;

            if (timer >= 1.0f)
            {
                currentFps = 1.0f / deltaTime;
                timer = 0f;
            }
        }

        void OnGUI()
        {
            GUIStyle style = new GUIStyle();
            int h = Screen.height;
            int w = Screen.width;
            
            // Đặt font size dựa trên màn hình để dễ nhìn
            style.fontSize = Mathf.Max(16, h * 3 / 100);
            style.normal.textColor = Color.white;
            
            // Vẽ đổ bóng chữ để dễ đọc trên nền sáng
            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = Color.black;

            long memory = System.GC.GetTotalMemory(false) / (1024 * 1024);
            string text = $"FPS: {Mathf.Ceil(currentFps)} | Memory: {memory} MB";

            Camera cam = Camera.main;
            if (cam != null)
            {
                int worldX = Mathf.FloorToInt(cam.transform.position.x);
                int worldZ = Mathf.FloorToInt(cam.transform.position.z);
                MineDemo.World.BiomeType biome = MineDemo.World.TerrainGenerator.GetBiome(worldX, worldZ);
                
                string biomeName = biome.ToString();
                // Thay thế tên Biome cho đẹp
                if (biome == MineDemo.World.BiomeType.RiverLake) biomeName = "River / Lake";
                else if (biome == MineDemo.World.BiomeType.FrozenRiverLake) biomeName = "Frozen River";
                else if (biome == MineDemo.World.BiomeType.BirchForest) biomeName = "Birch Forest";
                else if (biome == MineDemo.World.BiomeType.SnowyMountains) biomeName = "Snowy Mountains";
                else if (biome == MineDemo.World.BiomeType.SnowyPlains) biomeName = "Snowy Plains";

                text += $"\nXYZ: {worldX}, {Mathf.FloorToInt(cam.transform.position.y)}, {worldZ}\nBiome: {biomeName}";
            }

            Rect rect = new Rect(15, 15, w, h);
            Rect shadowRect = new Rect(17, 17, w, h);

            GUI.Label(shadowRect, text, shadowStyle);
            GUI.Label(rect, text, style);
        }

        public void LogMeshGeneration(string chunkName, long timeMs, int vertexCount, int triangleCount, long memoryDelta, int minY, int maxY, bool isInitial, int rebuildsPerSec)
        {
            string type = isInitial ? "INIT" : "REBUILD";
            UnityEngine.Debug.Log($"[ProfilerLogger - GenerateMesh] {chunkName} [{type}] | Bounds: {minY}..{maxY} | Time: {timeMs} ms | Verts: {vertexCount} | Tris: {triangleCount} | GC Delta: {memoryDelta / 1024} KB | Rebuilds/sec: {rebuildsPerSec}");
        }
    }
}
