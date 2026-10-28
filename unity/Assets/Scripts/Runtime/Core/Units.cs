namespace BoLike.Core
{
    /// <summary>
    /// The data tables come from the Phaser build, which uses pixels and milliseconds.
    /// Gameplay keeps those numbers and converts here: 100 px = 1 Unity unit.
    /// </summary>
    public static class Units
    {
        public const float PixelsPerUnit = 100f;

        // Phaser world constants (src/config.ts), in pixels.
        public const float ViewWidthPx = 1280f;
        public const float ViewHeightPx = 720f;
        public const float GroundTopPx = 450f;
        public const float GroundBottomPx = 690f;
        public const float GravityPx = 2100f;

        public static float Px(float pixels) => pixels / PixelsPerUnit;
        public static float Sec(float milliseconds) => milliseconds / 1000f;
    }
}
