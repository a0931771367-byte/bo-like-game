using UnityEngine;

namespace BoLike.Core
{
    /// <summary>The data tables store colours as 0xRRGGBB ints (Phaser style).</summary>
    public static class Colors
    {
        public static Color Rgb(int rgb, float a = 1f) =>
            new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public const int White = 0xffffff;
        public const int Gold = 0xffd34d;
        public const int Hp = 0xff5a5a;
        public const int Sp = 0x7fb2ff;
        public const int Heal = 0x7dff9a;
        public const int Crit = 0xffe066;
        public const int Buff = 0xb6f28a;
        public const int Debuff = 0xff8fa3;
        public const int Poison = 0xc47eff;
        public const int Ice = 0x9fe8ff;
        public const int Miss = 0xd0d0d0;
        public const int Guard = 0x9fd8ff;
    }
}
