using UnityEngine;
using UnityEngine.InputSystem;
using MineDemo.World;
using MineDemo.Blocks;

namespace MineDemo.Player
{
    public class PlayerInteraction : MonoBehaviour
    {
        public Camera playerCamera;
        public float reachDistance = 5f;
        
        public WorldManager worldManager;
        
        // Mặc định khối đặt ra là Dirt
        public BlockType selectedBlock = BlockType.Dirt;

        void Update()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame) // Chuột trái đập block
            {
                Interact(false);
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame) // Chuột phải đặt block
            {
                Interact(true);
            }
        }

        private void Interact(bool isPlacing)
        {
            if (playerCamera == null || worldManager == null) return;

            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
            {
                Vector3 pointInBlock;
                if (isPlacing)
                {
                    // Lùi ra ngoài bề mặt một chút theo pháp tuyến để lấy toạ độ ô liền kề
                    pointInBlock = hit.point + hit.normal * 0.1f;
                }
                else
                {
                    // Tiến vào trong bề mặt một chút theo pháp tuyến ngược để lấy toạ độ ô bị bắn trúng
                    pointInBlock = hit.point - hit.normal * 0.1f;
                }
                
                int x = Mathf.FloorToInt(pointInBlock.x);
                int y = Mathf.FloorToInt(pointInBlock.y);
                int z = Mathf.FloorToInt(pointInBlock.z);

                if (y < WorldBounds.MinBuildY || y >= WorldBounds.MaxBuildY)
                {
                    Debug.Log($"Vượt giới hạn Build Limit! (Min: {WorldBounds.MinBuildY}, Max: {WorldBounds.MaxBuildY-1})");
                    return;
                }

                if (!isPlacing)
                {
                    BlockType currentBlock = worldManager.GetBlockFromWorld(x, y, z);
                    if (currentBlock == BlockType.Bedrock)
                    {
                        Debug.Log("Không thể phá Bedrock!");
                        return;
                    }
                }

                worldManager.EditBlock(x, y, z, isPlacing ? selectedBlock : BlockType.Air);
            }
        }
    }
}
