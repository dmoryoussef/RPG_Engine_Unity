using UnityEngine;

namespace WorldGrid.Unity.Rendering
{
    internal static class TileMaterialFactory
    {
        #region Public API

        public static Material CreateInstance(Material template, Texture2D atlasTexture)
        {
            if (template == null)
                return null;

            var inst = new Material(template);
            bindAtlasTexture(inst, atlasTexture);
            return inst;
        }

        #endregion

        #region Texture Binding

        private static void bindAtlasTexture(Material material, Texture2D atlasTexture)
        {
            if (material == null || atlasTexture == null)
                return;

            // Try the most common texture property names (URP + built-in).
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", atlasTexture);

            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", atlasTexture);

            // Also set Unity's mainTexture (typically maps to _MainTex).
            material.mainTexture = atlasTexture;
        }

        #endregion
    }
}
