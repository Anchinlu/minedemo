using UnityEngine;

namespace MineDemo.World
{
    public static class DensityRouter
    {
        public const float MountainRegionLow = 0.52f;
        public const float MountainRegionHigh = 0.72f;
        public const float MountainCoreLow = 0.64f;
        public const float MountainCoreHigh = 0.94f;

        public static float CalculateTerrainSurface(
            int worldX, int worldZ,
            in WorldGenContext context,
            in NoiseSample n)
        {
            // 1. Spline cho Erosion (Quyết định độ cao cơ bản - Base Height)
            // Giải quyết vấn đề "1 vùng toàn núi": Erosion thấp sẽ chỉ tạo ra một cái Bệ núi (Plateau) ở Y~90-110.
            // Các đỉnh núi cao (Peaks) sẽ do biến PeakPotential (Weirdness) quyết định để tạo thành các dãy hẹp.
            
            float baseHeight = 64f;
            if (n.erosion > 0.35f) 
            {
                baseHeight = 64f; // Plains chiếm phần lớn
            }
            else if (n.erosion > 0.20f)
            {
                // Chuyển tiếp lên Chân đồi (Foothills)
                float t = Mathf.InverseLerp(0.35f, 0.20f, n.erosion);
                baseHeight = Mathf.Lerp(64f, 85f, Mathf.SmoothStep(0f, 1f, t));
            }
            else
            {
                // Bệ núi (Plateau) - Độ cao nền tảng cho dãy núi
                float t = Mathf.InverseLerp(0.20f, 0.0f, n.erosion);
                baseHeight = Mathf.Lerp(85f, 110f, Mathf.SmoothStep(0f, 1f, t));
            }

            // Tính khoảng cách đến tâm sông chính và nhánh phụ
            float distToRiver1 = Mathf.Abs(n.river - 0.5f);
            float distToRiver2 = Mathf.Abs(n.river2 - 0.5f);
            // Lấy khoảng cách nhỏ nhất để tạo hiệu ứng rẽ nhánh (Ngã ba sông)
            float distToRiver = Mathf.Min(distToRiver1, distToRiver2);
            
            // 2. Continentalness (Đại dương/Biển và Bờ biển)
            // Đồng bằng thấp dần để tạo bờ biển lài xuống mặt nước (Thay vì sụt lún đột ngột)
            if (n.continentalness < 0.35f)
            {
                // Tăng cường độ sâu của biển (từ Y=35 xuống Y=20)
                float t = Mathf.InverseLerp(0.15f, 0.35f, n.continentalness);
                baseHeight = Mathf.Lerp(20f, baseHeight, Mathf.SmoothStep(0f, 1f, t));
                
                // Thêm vực thẳm sâu (Trenches) ngẫu nhiên ở biển
                if (n.continentalness < 0.25f)
                {
                    float trenchNoise = Mathf.Abs(Mathf.PerlinNoise(worldX * 0.02f, worldZ * 0.02f) - 0.5f) * 2f; 
                    if (trenchNoise < 0.15f)
                    {
                        float trenchCarve = Mathf.SmoothStep(0f, 1f, 1f - (trenchNoise / 0.15f));
                        baseHeight -= trenchCarve * 30f; // Khoét sâu thêm 30 khối xuống tận Y=-10
                    }
                }

                // Thêm quần đảo (Islands) trên biển
                // Tần số cực thấp (0.003) để tạo ra những hòn đảo khổng lồ
                float islandNoise = Mathf.PerlinNoise(worldX * 0.003f, worldZ * 0.003f);
                if (islandNoise > 0.65f)
                {
                    // 0.65 -> 0.75: Bãi biển thoai thoải ngoi lên khỏi mặt nước
                    // > 0.75: Bề mặt đảo rộng lớn, bằng phẳng
                    float islandMask = Mathf.InverseLerp(0.65f, 0.75f, islandNoise);
                    islandMask = Mathf.SmoothStep(0f, 1f, islandMask);
                    
                    // Đảo cao ổn định ở mốc Y=68 để dễ mọc rừng/cây
                    float islandHeight = Mathf.Lerp(baseHeight, 68f, islandMask);
                    
                    // Thêm chút gồ ghề ngẫu nhiên (+/- 2 block) cho bề mặt đảo
                    float islandDetail = (Mathf.PerlinNoise(worldX * 0.05f, worldZ * 0.05f) * 4f) - 2f;
                    islandHeight += (islandDetail * islandMask);
                    
                    baseHeight = Mathf.Max(baseHeight, islandHeight);
                }
            }
            else
            {
                float continentalLift = Mathf.Lerp(-2f, 12f, n.continentalness);
                baseHeight += continentalLift;
            }

            // 2.5. Hồ ngẫu nhiên (Local Lakes) - Khắc phục đáy hồ bị phẳng 1 block
            // Tạo các hồ nhỏ rải rác trên bản đồ (Lake noise < 0.25)
            if (n.lake < 0.25f && baseHeight >= 63f)
            {
                // Tính độ sâu dần từ mép (0) vào tâm hồ (1)
                float lakeDepthMask = 1f - (n.lake / 0.25f);
                // Dùng hàm bậc 2 (Pow 2) để tạo hình cái bát (bờ thoai thoải, tâm lõm sâu)
                float depthDrop = Mathf.Pow(lakeDepthMask, 2f) * 15f; // Sâu tối đa 15 block
                baseHeight -= depthDrop;
            }

            // 3. Weirdness/Ridges (Tạo chóp nhọn xen kẽ thung lũng)
            float ridgeLift = 0f;
            if (n.erosion < 0.35f) 
            {
                float mountainPower = Mathf.InverseLerp(0.35f, 0.0f, n.erosion);
                
                // Núi chỉ mọc khi xa sông (distToRiver > 0.12)
                float weirdnessLift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.85f, n.peakPotential));
                float riverSuppression = Mathf.SmoothStep(0f, 1f, distToRiver / 0.12f);
                
                float peakMultiplier = mountainPower * weirdnessLift * riverSuppression;
                
                if (peakMultiplier > 0f)
                {
                    float jagged = Mathf.Max(0f, n.jaggedness - 0.3f);
                    ridgeLift = (peakMultiplier * 100f) + (jagged * 40f * peakMultiplier);
                }
            }

            // 4. Detail noise (Tạo gợn sóng nhỏ trên bề mặt)
            float detailAmplitude = Mathf.Lerp(6f, 2f, n.erosion);
            float detailLift = n.detail * detailAmplitude;
            
            // 5. River Carve (Hệ thống sông ngòi chuẩn Minecraft)
            float riverCarve = 0f;
            
            // "đột ngột kết thúc khi độ sâu lục địa đạt đến mức cao nhất" -> không sinh sông khi continentalness > 0.8
            // "Không sinh sông cắt núi nữa": Nếu erosion < 0.25 (vùng núi), sông cạn dần và biến mất.
            if (n.continentalness <= 0.8f && n.erosion > 0.20f)
            {
                // Bề ngang thay đổi ngẫu nhiên liên tục (Perlin Noise)
                float widthNoise = (Mathf.PerlinNoise(worldX * 0.05f, worldZ * 0.05f) * 0.01f) - 0.005f;
                
                // Bán kính cơ bản (nhỏ hơn do tần số sinh sông đã giảm cực thấp)
                float baseRiverRadius = Mathf.Lerp(0.015f, 0.005f, Mathf.InverseLerp(0.20f, 0.8f, n.continentalness)) + widthNoise;
                
                // Đối với vùng đồng bằng (Erosion > 0.35), mở rộng bề rộng của sông
                if (n.erosion > 0.35f)
                {
                    baseRiverRadius += 0.005f;
                }
                
                // Thu hẹp và làm mờ rãnh sông khi đi vào vùng núi (Erosion 0.35 -> 0.20)
                float mountainFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.35f, n.erosion));
                float riverRadius = baseRiverRadius * mountainFade;
                
                if (riverRadius > 0f && distToRiver < riverRadius)
                {
                    float riverT = distToRiver / riverRadius;
                    float carveMask = 1f - Mathf.SmoothStep(0f, 1f, riverT);
                    
                    float currentHeight = baseHeight + ridgeLift + detailLift;
                    
                    // Giải quyết lỗi vết nứt đồi cao: Đáy sông dâng lên ngang với địa hình đồi núi, thay vì cắt sâu xuống 61.
                    // mountainFade = 1 (đồng bằng) -> đáy Y=61. mountainFade = 0 (núi) -> đáy = bề mặt - 1 block (suối cạn)
                    float baseBed = Mathf.Lerp(currentHeight - 1f, 61f, mountainFade);
                    
                    // Tăng độ random phần đáy (Perlin Noise), lồi lõm nhẹ
                    float bottomNoise = (Mathf.PerlinNoise(worldX * 0.1f, worldZ * 0.1f) * 2f) - 1f;
                    float dynamicRiverBed = baseBed + bottomNoise;
                    
                    float maxCarve = currentHeight - dynamicRiverBed;
                    
                    if (maxCarve > 0)
                    {
                        // Pow 2.5: Đào dốc thẳng đứng từ mặt cỏ xuống, không có bờ lài
                        riverCarve = -(maxCarve * Mathf.Pow(carveMask, 2.5f));
                    }
                }
            }

            return baseHeight + ridgeLift + detailLift + riverCarve;
        }

        public static int GetBaseSurfaceY(
            int worldX, int worldZ,
            in WorldGenContext context,
            in NoiseSample n)
        {
            float terrainSurface = CalculateTerrainSurface(worldX, worldZ, context, n);
            return Mathf.Clamp(Mathf.FloorToInt(terrainSurface), context.MinY, context.MaxY - 1);
        }

        public static float GetDensity(
            int worldX, int worldY, int worldZ,
            in WorldGenContext context,
            in NoiseSample n)
        {
            float terrainSurface = CalculateTerrainSurface(worldX, worldZ, context, n);
            float baseDensity = terrainSurface - worldY;

            // In Phase C, we would subtract cave noise here.
            // float cave = NoiseRouter.SampleCave3D(worldX, worldY, worldZ, context);
            // float caveStrength = ...
            // float caveMask = ...
            // baseDensity -= cave * caveMask * caveStrength;

            return baseDensity;
        }
    }
}
