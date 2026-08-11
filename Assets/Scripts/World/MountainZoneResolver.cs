using UnityEngine;

namespace MineDemo.World
{
    internal static class MountainZoneResolver
    {
        public static float GetMountainRegionWeight(in WorldColumn col)
        {
            // Vùng núi bắt đầu khi Erosion < 0.32 (giống DensityRouter)
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.28f, col.noise.erosion));
        }

        public static float GetMountainCoreWeight(in WorldColumn col)
        {
            // Lõi núi (đỉnh) khi Erosion < 0.20
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.15f, col.noise.erosion));
        }

        public static float GetIsolatedPeakWeight(in WorldColumn col)
        {
            float mountainCore = GetMountainCoreWeight(col);
            float weirdness = Mathf.SmoothStep(0.60f, 1.0f, col.noise.peakPotential);
            return mountainCore * weirdness;
        }

        public static MountainZone ResolveZone(in WorldColumn col)
        {
            float mountainRegionWeight = GetMountainRegionWeight(col);
            float isolatedPeakWeight = GetIsolatedPeakWeight(col);

            if (isolatedPeakWeight >= 0.60f && col.surfaceY >= 155)
            {
                return MountainZone.Peak;
            }

            if (col.surfaceY >= 170 && col.slope >= 2)
            {
                return MountainZone.Peak;
            }
            
            if (mountainRegionWeight > 0.4f)
            {
                if (col.slope <= 2 && col.surfaceY >= 90 && col.surfaceY <= 125)
                {
                    return MountainZone.Meadow;
                }
                return MountainZone.Slope;
            }
            
            if (mountainRegionWeight > 0.1f && col.surfaceY < 105)
            {
                return MountainZone.Foothill;
            }
            
            return MountainZone.None;
        }
    }
}
