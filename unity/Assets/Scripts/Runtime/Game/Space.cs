using UnityEngine;

namespace BoLike.Game
{
    /// <summary>
    /// Converts between the simulation's Phaser pixel space (x right, y down, z up) and Unity world units.
    /// 100 px = 1 unit; Unity y = -(y - z) / 100, so the ground strip sits between y = -4.5 and -6.9.
    /// </summary>
    public static class Space
    {
        public const float Scale = 0.01f;

        public static Vector3 ToWorld(float px, float py, float pz = 0f, float depth = 0f) => new(px * Scale, -(py - pz) * Scale, depth);

        /// <summary>Draw order from the lane y: lower on screen = in front. Leaves room for ±9 offsets per y.</summary>
        public static int Order(float py) => Mathf.RoundToInt(-py * 10f);

        public const int OrderGround = -20000;
        public const int OrderSky = -30000;
        public const int OrderFx = 100000;
        public const int OrderFxText = 100020;
        public const int OrderFlash = 100100;
    }
}
