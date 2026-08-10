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

        void Start()
        {
            controller = GetComponent<CharacterController>();
            // Ẩn và khóa con trỏ chuột vào giữa màn hình
            Cursor.lockState = CursorLockMode.Locked; 
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

            Vector3 center = controller.bounds.center;
            int x = Mathf.FloorToInt(center.x);
            int y = Mathf.FloorToInt(center.y);
            int z = Mathf.FloorToInt(center.z);

            BlockType centerType = worldManager.GetExpectedBlock(x, y, z);
            
            Vector3 min = controller.bounds.min;
            int yMin = Mathf.FloorToInt(min.y);
            BlockType feetType = worldManager.GetExpectedBlock(x, yMin, z);

            return (centerType == BlockType.WaterSource || centerType == BlockType.WaterFlow) ||
                   (feetType == BlockType.WaterSource || feetType == BlockType.WaterFlow);
        }

        private void HandleMovement()
        {
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; 
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
