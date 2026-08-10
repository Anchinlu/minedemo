using UnityEngine;
using System.Collections.Generic;

namespace MineDemo.Blocks
{
    [System.Serializable]
    public struct UVRect
    {
        public Vector2 bottomLeft;
        public Vector2 bottomRight;
        public Vector2 topRight;
        public Vector2 topLeft;
    }

    [CreateAssetMenu(fileName = "AtlasData", menuName = "MineDemo/AtlasData")]
    public class AtlasData : ScriptableObject
    {
        public List<TextureId> keys = new List<TextureId>();
        public List<UVRect> values = new List<UVRect>();

        private Dictionary<TextureId, UVRect> dict;

        public UVRect GetUVs(TextureId id)
        {
            if (dict == null || dict.Count != keys.Count)
            {
                dict = new Dictionary<TextureId, UVRect>();
                for (int i = 0; i < keys.Count; i++)
                {
                    dict[keys[i]] = values[i];
                }
            }
            if (dict.TryGetValue(id, out UVRect rect))
                return rect;
                
            return new UVRect(); // Return zeroed if not found
        }
    }
}
