using UnityEngine;
using MineDemo.World;
using MineDemo.Blocks;

namespace MineDemo.UI
{
    public class WorldDebugOverlay : MonoBehaviour
    {
        public static WorldDebugOverlay Instance;

        private WorldManager worldManager;
        private Transform playerTransform;

        private string debugText = "";
        private float timer = 0f;
        private const float UpdateInterval = 0.2f;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            worldManager = FindFirstObjectByType<WorldManager>();
            var pc = FindFirstObjectByType<MineDemo.Player.PlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }

        void Update()
        {
            if (playerTransform == null && worldManager != null && worldManager.player != null)
            {
                playerTransform = worldManager.player;
            }

            timer += Time.deltaTime;
            if (timer >= UpdateInterval)
            {
                timer = 0f;
                UpdateDebugInfo();
            }
        }

        private void UpdateDebugInfo()
        {
            if (playerTransform == null || worldManager == null)
            {
                debugText = "Debug Overlay: Đang chờ Player / WorldManager...";
                return;
            }

            Vector3 pos = playerTransform.position;
            int x = Mathf.FloorToInt(pos.x);
            int y = Mathf.FloorToInt(pos.y);
            int z = Mathf.FloorToInt(pos.z);

            BiomeType biome = TerrainGenerator.GetBiome(x, z);
            BlockType blockFeet = worldManager.GetBlockFromWorld(x, y - 1, z);
            BlockType blockUnderSurface = worldManager.GetBlockFromWorld(x, y, z);
            TerrainShapeResult shape = TerrainShapeGenerator.GenerateShape(x, z, TerrainGenerator.Seed);

            // Thống kê phân bổ Biome & MountainMask trong bán kính 512 block (grid 17x17)
            int plainsCount = 0, hillsCount = 0, mntCount = 0, totalSamples = 0;
            float mntMin = 1.0f, mntMax = 0.0f, mntSum = 0.0f;
            int step = 32;
            for (int sx = -256; sx <= 256; sx += step)
            {
                for (int sz = -256; sz <= 256; sz += step)
                {
                    totalSamples++;
                    TerrainShapeResult s = TerrainShapeGenerator.GenerateShape(x + sx, z + sz, TerrainGenerator.Seed);
                    if (s.mountainMask < mntMin) mntMin = s.mountainMask;
                    if (s.mountainMask > mntMax) mntMax = s.mountainMask;
                    mntSum += s.mountainMask;

                    BiomeType b = TerrainGenerator.GetBiome(x + sx, z + sz);
                    if (b == BiomeType.Mountains || b == BiomeType.SnowyMountains) mntCount++;
                    else if (b == BiomeType.Hills) hillsCount++;
                    else plainsCount++;
                }
            }
            float plainsPct = (plainsCount * 100f) / totalSamples;
            float hillsPct = (hillsCount * 100f) / totalSamples;
            float mntPct = (mntCount * 100f) / totalSamples;
            float mntAvg = mntSum / totalSamples;

            debugText = $"<b>[MINEDEMO DEBUG OVERLAY]</b>\n" +
                        $"Biome: <b><color=cyan>{biome}</color></b>\n" +
                        $"Surface Y: <b><color=yellow>{shape.surfaceY}</color></b>\n" +
                        $"Player XYZ: <b>X: {pos.x:F1}  Y: {pos.y:F1}  Z: {pos.z:F1}</b>\n" +
                        $"Block Under Feet (Y-1): <b><color=green>{blockFeet}</color></b>\n" +
                        $"HillsMask: {shape.hillsMask:F2} | RegMask: {shape.regionMask:F2} | MntMask: {shape.mountainMask:F2}\n" +
                        $"<b>Biome Dist (512m):</b> Plains: <b>{plainsPct:F0}%</b> | Hills: <b>{hillsPct:F0}%</b> | Mountains: <b><color=orange>{mntPct:F0}%</color></b>\n" +
                        $"<b>MntMask Stats (512m):</b> Min: {mntMin:F2} | Max: {mntMax:F2} | Avg: <b><color=yellow>{mntAvg:F3}</color></b>";
        }

        void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.normal.textColor = Color.white;
            style.richText = true;

            // Gradient / Dark box background for readability
            GUI.Box(new Rect(10, 10, 440, 200), "");
            GUI.Label(new Rect(20, 15, 420, 190), debugText, style);
        }
    }
}
