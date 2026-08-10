using UnityEngine;

namespace MineDemo.World
{
    [CreateAssetMenu(fileName = "WaterAnimationData", menuName = "MineDemo/WaterAnimationData")]
    public class WaterAnimationData : ScriptableObject
    {
        public Texture2D[] stillFrames;
        public int[] stillFrameSequence;
        public float stillFrameTime = 0.1f;

        public Texture2D[] flowFrames;
        public int[] flowFrameSequence;
        public float flowFrameTime = 0.1f;
    }
}
