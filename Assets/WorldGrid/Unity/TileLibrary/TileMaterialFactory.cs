using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    internal static class TileMaterialFactory
    {
        public static Material CreateInstance(Material template, Texture2D atlasTexture)
        {
            if (template == null) return null;

            var inst = new Material(template);

            // Try the most common texture property names (URP + built-in).
            if (atlasTexture != null)
            {
                if (inst.HasProperty("_BaseMap")) inst.SetTexture("_BaseMap", atlasTexture);
                if (inst.HasProperty("_MainTex")) inst.SetTexture("_MainTex", atlasTexture);

                // Also set Unity's mainTexture (maps to _MainTex typically).
                inst.mainTexture = atlasTexture;
            }

            return inst;
        }
    }
}
