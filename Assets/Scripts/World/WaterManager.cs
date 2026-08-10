using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MineDemo.Blocks;

namespace MineDemo.World
{
    public struct WaterCell
    {
        public Vector3Int pos;
        public byte level;
    }

    public class WaterManager : MonoBehaviour
    {
        public static WaterManager Instance { get; private set; }
        
        public Material stillMaterial;
        public Material flowMaterial;
        
        public int maxCellsPerFrame = 500;
        public float updateInterval = 0.15f;
        
        private Queue<WaterCell> pendingUpdates = new Queue<WaterCell>();
        private HashSet<Vector3Int> queuedCells = new HashSet<Vector3Int>();
        
        private WorldManager worldManager;
        private WaterAnimationData animData;

        // Animation timers
        private float stillTimer = 0f;
        private int stillIndex = 0;
        private float flowTimer = 0f;
        private int flowIndex = 0;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                CreateWaterMaterial();
            }
            else
            {
                Destroy(this);
            }
        }

        void Start()
        {
            worldManager = FindAnyObjectByType<WorldManager>();
            StartCoroutine(WaterFlowRoutine());
        }

        void Update()
        {
            if (animData == null) return;

            // Animate Still Water
            if (animData.stillFrames != null && animData.stillFrames.Length > 0)
            {
                stillTimer += Time.deltaTime;
                if (stillTimer >= animData.stillFrameTime)
                {
                    stillTimer -= animData.stillFrameTime;
                    stillIndex = (stillIndex + 1) % animData.stillFrameSequence.Length;
                    int frameNum = animData.stillFrameSequence[stillIndex];
                    if (frameNum >= 0 && frameNum < animData.stillFrames.Length && stillMaterial != null)
                    {
                        stillMaterial.mainTexture = animData.stillFrames[frameNum];
                    }
                }
            }

            // Animate Flow Water
            if (animData.flowFrames != null && animData.flowFrames.Length > 0)
            {
                flowTimer += Time.deltaTime;
                if (flowTimer >= animData.flowFrameTime)
                {
                    flowTimer -= animData.flowFrameTime;
                    flowIndex = (flowIndex + 1) % animData.flowFrameSequence.Length;
                    int frameNum = animData.flowFrameSequence[flowIndex];
                    if (frameNum >= 0 && frameNum < animData.flowFrames.Length && flowMaterial != null)
                    {
                        flowMaterial.mainTexture = animData.flowFrames[frameNum];
                    }
                }
            }
        }

        private void CreateWaterMaterial()
        {
            animData = Resources.Load<WaterAnimationData>("TempMinecraft/WaterAnimationData");
            if (animData == null)
            {
                Debug.LogError("WaterAnimationData not found! Please run 'MineDemo -> Import Water Animations' first.");
                return;
            }

            // Dùng Shader tự viết cho Water để tránh lỗi tương thích của Unity RP
            Shader shader = Shader.Find("MineDemo/WaterTransparent");
            if (shader == null)
            {
                // Fallback nếu Shader chưa kịp compile
                shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Standard");
            }

            stillMaterial = new Material(shader);
            flowMaterial = new Material(shader);
            
            // Khởi tạo Frame 00
            if (animData.stillFrames != null && animData.stillFrames.Length > 0)
                stillMaterial.mainTexture = animData.stillFrames[animData.stillFrameSequence[0]];
                
            if (animData.flowFrames != null && animData.flowFrames.Length > 0)
                flowMaterial.mainTexture = animData.flowFrames[animData.flowFrameSequence[0]];
            
            Debug.Log($"Water Material created successfully! Render Pipeline Shader: {shader.name}");
        }

        public void EnqueueWaterUpdate(Vector3Int pos, byte level)
        {
            if (!queuedCells.Contains(pos))
            {
                queuedCells.Add(pos);
                pendingUpdates.Enqueue(new WaterCell { pos = pos, level = level });
            }
        }

        private IEnumerator WaterFlowRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(updateInterval);

                int cellsProcessed = 0;
                while (pendingUpdates.Count > 0 && cellsProcessed < maxCellsPerFrame)
                {
                    WaterCell cell = pendingUpdates.Dequeue();
                    queuedCells.Remove(cell.pos);
                    cellsProcessed++;
                    
                    ProcessWaterCell(cell);
                }
            }
        }

        private const byte FullLevel = 8;
        private const byte HorizontalLoss = 2;

        private byte GetNextSideLevel(byte currentLevel)
        {
            if (currentLevel <= HorizontalLoss)
                return 0;

            return (byte)(currentLevel - HorizontalLoss);
        }

        private void ProcessWaterCell(WaterCell cell)
        {
            if (worldManager == null) return;
            if (!WorldManager.EnableWaterFlow) return;

            // Kiểm tra xem vị trí này còn là nước không
            BlockType currentType = worldManager.GetExpectedBlock(cell.pos.x, cell.pos.y, cell.pos.z);
            if (currentType != BlockType.WaterSource && currentType != BlockType.WaterFlow)
                return;

            byte currentLevel = worldManager.GetWaterLevelWorld(cell.pos.x, cell.pos.y, cell.pos.z);
            if (currentLevel == 0) return; // Khô

            // Ưu tiên 1: Chảy xuống
            Vector3Int downPos = cell.pos + Vector3Int.down;
            BlockType downType = worldManager.GetExpectedBlock(downPos.x, downPos.y, downPos.z);
            
            if (CanFlowInto(downType))
            {
                if (downType == BlockType.Grass) DestroyGrass(downPos);
                
                byte downLevel = worldManager.GetWaterLevelWorld(downPos.x, downPos.y, downPos.z);
                if (downLevel < FullLevel) // Dòng chảy rơi xuống luôn đầy (level 8)
                {
                    worldManager.SetGlobalBlock(downPos.x, downPos.y, downPos.z, BlockType.WaterFlow, FullLevel, true);
                    // SetGlobalBlock giờ đã tự gọi EnqueueWaterUpdate nên không cần gọi tay nữa.
                }
                return; // Đã chảy xuống thì không lan ngang nữa để tạo thác nước chuẩn
            }

            // Ưu tiên 2: Lan ngang
            byte nextLevel = GetNextSideLevel(currentLevel);
            if (nextLevel == 0)
                return;
                
            Vector3Int[] directions = { Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back };
            foreach (var dir in directions)
            {
                Vector3Int sidePos = cell.pos + dir;
                BlockType sideType = worldManager.GetExpectedBlock(sidePos.x, sidePos.y, sidePos.z);
                
                if (sideType == BlockType.WaterSource)
                    continue;

                byte sideLevel = worldManager.GetWaterLevelWorld(sidePos.x, sidePos.y, sidePos.z);
                bool canReplace = sideType == BlockType.Air ||
                                  sideType == BlockType.Grass ||
                                  (sideType == BlockType.WaterFlow && sideLevel < nextLevel);

                if (!canReplace)
                    continue;

                if (sideType == BlockType.Grass) DestroyGrass(sidePos);
                
                worldManager.SetGlobalBlock(sidePos.x, sidePos.y, sidePos.z, BlockType.WaterFlow, nextLevel, true);
            }
        }

        private bool CanFlowInto(BlockType type)
        {
            return type == BlockType.Air || type == BlockType.Grass || type == BlockType.WaterFlow;
        }

        private void DestroyGrass(Vector3Int pos)
        {
            // Sinh Particle đất vỡ ra
            for (int i = 0; i < 4; i++)
            {
                GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                particle.transform.position = pos + new Vector3(Random.Range(0.2f, 0.8f), 0.5f, Random.Range(0.2f, 0.8f));
                particle.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
                
                MeshRenderer renderer = particle.GetComponent<MeshRenderer>();
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = new Color(0.3f, 0.6f, 0.2f); // Màu xanh cỏ
                
                Rigidbody rb = particle.AddComponent<Rigidbody>();
                rb.AddExplosionForce(150f, particle.transform.position - Vector3.up * 0.5f, 2f);
                
                Destroy(particle, 0.5f);
            }
        }
    }
}
