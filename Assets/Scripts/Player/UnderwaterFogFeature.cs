using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MineDemo
{
    public class UnderwaterFogFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class FogSettings
        {
            public Color fogColor = new Color(0.02f, 0.12f, 0.28f, 1.0f);
            public float fogStart = 4f;
            public float fogEnd = 28f;
            public Shader fogShader;
        }

        public FogSettings settings = new FogSettings();
        private UnderwaterFogPass fogPass;
        
        public static bool IsUnderwater = false;

        public override void Create()
        {
            if (settings.fogShader == null)
            {
                settings.fogShader = Shader.Find("Hidden/UnderwaterFog");
            }

            fogPass = new UnderwaterFogPass(settings);
            fogPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Kiểm tra trạng thái của Depth Texture theo yêu cầu
            if (!renderingData.cameraData.requiresDepthTexture)
            {
                Debug.LogWarning("[UnderwaterFog] Cảnh báo: Depth Texture chưa được bật trong URP Asset hoặc Camera! Sương mù dưới nước sẽ không hoạt động đúng.");
            }

            if (IsUnderwater && settings.fogShader != null)
            {
                renderer.EnqueuePass(fogPass);
            }
        }

        class UnderwaterFogPass : ScriptableRenderPass
        {
            private Material fogMaterial;
            private FogSettings settings;
            private RTHandle source;

#pragma warning disable CS0672 // Disable obsolete warning for Execute/OnCameraSetup
#pragma warning disable CS0618 // Disable obsolete warning for Blit/cameraColorTargetHandle

            public UnderwaterFogPass(FogSettings settings)
            {
                this.settings = settings;
                if (settings.fogShader != null)
                {
                    fogMaterial = CoreUtils.CreateEngineMaterial(settings.fogShader);
                }
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                source = renderer.cameraColorTargetHandle;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (fogMaterial == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("UnderwaterFog");

                fogMaterial.SetColor("_UnderwaterFogColor", settings.fogColor);
                fogMaterial.SetFloat("_FogStart", settings.fogStart);
                fogMaterial.SetFloat("_FogEnd", settings.fogEnd);

                Blit(cmd, ref renderingData, fogMaterial);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                source = null;
            }
        }
    }
}
