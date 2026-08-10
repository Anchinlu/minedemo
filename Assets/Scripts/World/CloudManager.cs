using UnityEngine;
using System.Collections.Generic;

namespace MineDemo.World
{
    public class CloudManager : MonoBehaviour
    {
        public int cloudArea = 7;
        public float cloudCellSize = 32f;
        public float cloudHeight = WorldBounds.MaxBuildY + 20f;
        public float cloudSpeed = 1f;

        private Transform player;
        private List<GameObject> clouds = new List<GameObject>();
        private Material cloudMaterial;

        void Start()
        {
            var pc = FindFirstObjectByType<MineDemo.Player.PlayerController>();
            if (pc != null) player = pc.transform;

            // Khởi tạo Material cho mây bằng Shader Sprites/Default (luôn hỗ trợ Color và Alpha trên mọi project)
            cloudMaterial = new Material(Shader.Find("Sprites/Default"));
            cloudMaterial.color = new Color(1f, 1f, 1f, 0.75f);
            cloudMaterial.renderQueue = 3000;

            GenerateClouds();
        }

        void GenerateClouds()
        {
            for (int x = -cloudArea / 2; x <= cloudArea / 2; x++)
            {
                for (int z = -cloudArea / 2; z <= cloudArea / 2; z++)
                {
                    // Dùng Perlin Noise để quyết định chỗ nào có mây
                    float noise = Mathf.PerlinNoise(x * 0.2f + 12.3f, z * 0.2f + 4.5f);
                    if (noise > 0.45f)
                    {
                        GameObject cloudObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cloudObj.name = $"Cloud_{x}_{z}";
                        cloudObj.transform.parent = this.transform;
                        
                        // Kích thước ngẫu nhiên
                        float scaleX = Random.Range(0.6f, 1.4f) * cloudCellSize;
                        float scaleZ = Random.Range(0.6f, 1.4f) * cloudCellSize;
                        cloudObj.transform.localScale = new Vector3(scaleX, 6f, scaleZ);

                        // Vị trí
                        Vector3 pos = new Vector3(x * cloudCellSize, cloudHeight, z * cloudCellSize);
                        if (player != null)
                        {
                            pos.x += player.position.x;
                            pos.z += player.position.z;
                        }
                        cloudObj.transform.position = pos;

                        // Bỏ đổ bóng và bỏ Collision
                        MeshRenderer renderer = cloudObj.GetComponent<MeshRenderer>();
                        renderer.material = cloudMaterial;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        Destroy(cloudObj.GetComponent<Collider>());

                        clouds.Add(cloudObj);
                    }
                }
            }
        }

        void Update()
        {
            if (player == null) return;

            float moveStep = cloudSpeed * Time.deltaTime;
            float maxDist = (cloudArea / 2f) * cloudCellSize;
            
            foreach (var cloud in clouds)
            {
                // Mây trôi về phía trục X dương
                cloud.transform.position += new Vector3(moveStep, 0, 0);

                // Thuật toán Object Pool: Nếu mây trôi quá xa, bế nó vòng lại phía sau lưng
                if (cloud.transform.position.x > player.position.x + maxDist)
                {
                    cloud.transform.position = new Vector3(
                        player.position.x - maxDist, 
                        cloud.transform.position.y, 
                        cloud.transform.position.z
                    );
                }
                else if (cloud.transform.position.x < player.position.x - maxDist)
                {
                    cloud.transform.position = new Vector3(
                        player.position.x + maxDist, 
                        cloud.transform.position.y, 
                        cloud.transform.position.z
                    );
                }
                
                if (cloud.transform.position.z > player.position.z + maxDist)
                {
                    cloud.transform.position = new Vector3(
                        cloud.transform.position.x, 
                        cloud.transform.position.y, 
                        player.position.z - maxDist
                    );
                }
                else if (cloud.transform.position.z < player.position.z - maxDist)
                {
                    cloud.transform.position = new Vector3(
                        cloud.transform.position.x, 
                        cloud.transform.position.y, 
                        player.position.z + maxDist
                    );
                }
            }
        }
    }
}
