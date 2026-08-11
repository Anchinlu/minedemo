using UnityEngine;

namespace MineDemo.World
{
    internal static class MountainZoneResolver
    {
        public static float GetIsolatedPeakWeight(in WorldColumn col)
        {
            float mountainCore = Mathf.SmoothStep(0.64f, 0.94f, col.noise.ridges);
            float lowErosion = 1f - Mathf.SmoothStep(0.30f, 0.75f, col.noise.erosion);
            float peakSeed = Mathf.SmoothStep(0.70f, 0.90f, col.noise.peakPotential);
            return mountainCore * Mathf.Pow(peakSeed, 1.5f) * lowErosion;
        }

        public static MountainZone ResolveZone(in WorldColumn col)
        {
            float mountainRegionWeight = Mathf.SmoothStep(0.52f, 0.72f, col.noise.ridges);
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
