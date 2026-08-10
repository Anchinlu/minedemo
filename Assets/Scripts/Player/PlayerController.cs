using UnityEngine;
using UnityEngine.InputSystem;
using MineDemo.World;
using MineDemo.Blocks;

namespace MineDemo.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 5f;
        public float flySpeed = 15f;
        public float gravity = -15f;
        public float jumpHeight = 1.2f;
        public float lookSensitivity = 2f;
        
        public Transform playerCamera;
        
        public WorldManager worldManager;
        public float swimSpeed = 3.0f;
        public float swimUpSpeed = 4.0f;
        public float waterGravity = -3.0f;

        private CharacterController controller;
        private Vector3 velocity;
        private bool isGrounded;
        private float xRotation = 0f;

        private float waterCheckTimer = 0f;
        private bool isCurrentlyInWater = false;
        
        private bool isFlying = false;
        private float lastSpacePressTime = -1f;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            // Ẩn và khóa con trỏ chuột vào giữa màn hình
            Cursor.lockState = CursorLockMode.Locked; 
            
            if (worldManager == null)
            {
                worldManager = FindFirstObjectByType<WorldManager>();
            }
        }

        void Update()
        {
            HandleMouseLook();
            HandleMovement();
        }

        private void HandleMouseLook()
        {
            if (playerCamera == null || Mouse.current == null) return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            // Scale sensitivity slightly for New Input System raw delta
            float mouseX = mouseDelta.x * lookSensitivity * 0.1f;
            float mouseY = mouseDelta.y * lookSensitivity * 0.1f;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        private bool IsInWater()
        {
            if (worldManager == null || controller == null)
                return false;

            waterCheckTimer += Time.deltaTime;
            if (waterCheckTimer < 0.05f)
            {
                return isCurrentlyInWater;
            }
            waterCheckTimer = 0f;

            Vector3 center = controller.bounds.center;
            int x = Mathf.FloorToInt(center.x);
            int y = Mathf.FloorToInt(center.y);
            int z = Mathf.FloorToInt(center.z);

            BlockType centerType = worldManager.GetBlockForPlayerCheck(x, y, z);
            
            Vector3 min = controller.bounds.min;
            int yMin = Mathf.FloorToInt(min.y);
            BlockType feetType = worldManager.GetBlockForPlayerCheck(x, yMin, z);

            isCurrentlyInWater = (centerType == BlockType.WaterSource || centerType == BlockType.WaterFlow) ||
                                 (feetType == BlockType.WaterSource || feetType == BlockType.WaterFlow);
            return isCurrentlyInWater;
        }

        private void HandleMovement()
        {
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; 
                if (isFlying) isFlying = false;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (Time.time - lastSpacePressTime < 0.3f)
                {
                    isFlying = !isFlying;
                }
                lastSpacePressTime = Time.time;
            }

            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                int wx = Mathf.FloorToInt(transform.position.x);
                int wz = Mathf.FloorToInt(transform.position.z);
                TerrainGenerator.DebugPrintTerrainInfo(wx, wz);
            }

            float x = 0f;
            float z = 0f;
            
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) z += 1f;
                if (Keyboard.current.sKey.isPressed) z -= 1f;
                if (Keyboard.current.aKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed) x += 1f;
            }

            if (isFlying)
            {
                velocity.y = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.spaceKey.isPressed) velocity.y = flySpeed;
                    if (Keyboard.current.leftShiftKey.isPressed) velocity.y = -flySpeed;
                }
                Vector3 moveFly = transform.right * x + transform.forward * z;
                controller.Move(moveFly.normalized * flySpeed * Time.deltaTime + velocity * Time.deltaTime);
                return;
            }

            bool inWater = IsInWater();
            float currentSpeed = inWater ? swimSpeed : walkSpeed;

            Vector3 move = transform.right * x + transform.forward * z;
            controller.Move(move.normalized * currentSpeed * Time.deltaTime);

            if (inWater)
            {
                velocity.y = Mathf.Max(velocity.y, waterGravity);

                if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                {
                    velocity.y = swimUpSpeed;
                }
            }
            else
            {
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
                
                velocity.y += gravity * Time.deltaTime;
            }

            controller.Move(velocity * Time.deltaTime);
        }
    }
}
