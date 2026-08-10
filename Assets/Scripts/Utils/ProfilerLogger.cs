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

        void Update()
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            timer += Time.unscaledDeltaTime;

            if (timer >= 1.0f)
            {
                float fps = 1.0f / deltaTime;
                UnityEngine.Debug.Log($"[ProfilerLogger] FPS: {Mathf.Ceil(fps)} | Memory: {System.GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                timer = 0f;
            }
        }

        public void LogMeshGeneration(string chunkName, long timeMs, int vertexCount, int triangleCount, long memoryDelta, int minY, int maxY, bool isInitial, int rebuildsPerSec)
        {
            string type = isInitial ? "INIT" : "REBUILD";
            UnityEngine.Debug.Log($"[ProfilerLogger - GenerateMesh] {chunkName} [{type}] | Bounds: {minY}..{maxY} | Time: {timeMs} ms | Verts: {vertexCount} | Tris: {triangleCount} | GC Delta: {memoryDelta / 1024} KB | Rebuilds/sec: {rebuildsPerSec}");
        }
    }
}
