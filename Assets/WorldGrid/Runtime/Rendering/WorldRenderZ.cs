// WorldRenderZ.cs
namespace WorldGrid.Runtime.Rendering
{
    /// <summary>
    /// Shared Z layer definitions for world render channels.
    /// Materials/shaders do not define layering; renderers decide via Z.
    /// </summary>
    public static class WorldRenderZ
    {
        public const float TilesGround = 0.00f;
        public const float Grass = -0.02f;
        public const float Decals = -0.03f;
        public const float Props = -0.05f;
        public const float Units = -0.07f;
        public const float FX = -0.10f;
    }
}
