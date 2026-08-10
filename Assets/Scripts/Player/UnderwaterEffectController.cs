using UnityEngine;
using UnityEngine.UI;
using MineDemo.World;
using MineDemo.Blocks;

namespace MineDemo
{
    public class UnderwaterEffectController : MonoBehaviour
    {
        [Header("References")]
        public Transform playerCamera;
        public WorldManager worldManager;
        
        [Header("Overlay Settings")]
        [Range(0.20f, 0.40f)]
        public float overlayAlpha = 0.35f;
        public float fadeSpeed = 6.66f; // Fade in ~0.15s
        public Color underwaterColor = new Color(0.02f, 0.12f, 0.30f, 1.0f);
        
        private Image overlayImage;
        private bool isUnderwater = false;
        private float currentAlpha = 0f;

        private void Start()
        {
            if (worldManager == null)
                worldManager = Object.FindFirstObjectByType<WorldManager>();
                
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main.transform;
                
            SetupCanvas();
        }

        private void SetupCanvas()
        {
            GameObject canvasObj = new GameObject("UnderwaterCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Render on top of everything
            
            GameObject imageObj = new GameObject("OverlayImage");
            imageObj.transform.SetParent(canvasObj.transform, false);
            overlayImage = imageObj.AddComponent<Image>();
            
            RectTransform rect = overlayImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            Texture2D tex = Resources.Load<Texture2D>("underwater");
            if (tex != null)
            {
                overlayImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                Debug.LogWarning("[UnderwaterEffect] Missing Resources/underwater.png. Using solid color fallback.");
            }
            
            Color startColor = underwaterColor;
            startColor.a = 0f;
            overlayImage.color = startColor;
            overlayImage.raycastTarget = false;
        }

        private void Update()
        {
            if (playerCamera == null || worldManager == null) return;

            Vector3 camPos = playerCamera.position;
            BlockType type = worldManager.GetBlockForPlayerCheck(
                Mathf.FloorToInt(camPos.x),
                Mathf.FloorToInt(camPos.y),
                Mathf.FloorToInt(camPos.z)
            );

            isUnderwater = (type == BlockType.WaterSource || type == BlockType.WaterFlow);

            // Handle UI Overlay
            float targetAlpha = isUnderwater ? overlayAlpha : 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            
            Color c = underwaterColor;
            c.a = currentAlpha;
            overlayImage.color = c;
            
            // Set global state for URP Render Feature
            UnderwaterFogFeature.IsUnderwater = isUnderwater;
        }
    }
}
