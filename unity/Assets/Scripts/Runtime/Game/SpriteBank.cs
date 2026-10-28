using System.Collections.Generic;
using UnityEngine;

namespace BoLike.Game
{
    /// <summary>
    /// Sprites loaded from Resources, plus a few procedural shapes (circle, ring, arc, pixel) the effects use.
    /// Keys match the Phaser texture keys: "swordsman_side", "anim/swordsman_walk_2", "icon_bash".
    /// </summary>
    public static class SpriteBank
    {
        static readonly Dictionary<string, Sprite> cache = new();
        static Material flashMaterial;
        static Sprite pixel, circle, ring, arc, softCircle;

        public static Material FlashMaterial
        {
            get
            {
                if (flashMaterial == null)
                {
                    var shader = Shader.Find("BoLike/SpriteFlash");
                    flashMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                }
                return flashMaterial;
            }
        }

        public static Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            s = Resources.Load<Sprite>("Sprites/" + key);
            if (s == null) Debug.LogWarning($"SpriteBank: missing Sprites/{key}");
            cache[key] = s;
            return s;
        }

        public static bool Has(string key) => Get(key) != null;

        public static Sprite Icon(string classId, int index)
        {
            string key = $"icon/{classId}_{index}";
            if (cache.TryGetValue(key, out var s)) return s;
            s = Resources.Load<Sprite>($"Icons/skill_{classId}_{index}");
            cache[key] = s;
            return s;
        }

        public static string AnimKey(string cls, string label, int i) => $"anim/{cls}_{label}_{i}";

        public static Texture2D Background(string bgKey) => Resources.Load<Texture2D>("Bg/" + bgKey.Replace("bg_", ""));

        // ---------- procedural shapes (100 px per unit, pivot centre) ----------

        /// <summary>4×4 white square; scale it to draw rectangles.</summary>
        public static Sprite Pixel => pixel ??= Make(4, 4, (_, _) => 1f);

        /// <summary>64 px filled circle with a 1.5 px anti-aliased edge.</summary>
        public static Sprite Circle => circle ??= Make(64, 64, (u, v) => Edge(1f - Len(u, v), 1.5f / 32f));

        /// <summary>64 px circle with a soft falloff for glows.</summary>
        public static Sprite SoftCircle => softCircle ??= Make(64, 64, (u, v) => Mathf.Clamp01(1f - Len(u, v)) * Mathf.Clamp01(1f - Len(u, v)));

        /// <summary>128 px ring: outer radius 1, stroke 12%.</summary>
        public static Sprite Ring => ring ??= Make(128, 128, (u, v) => { float l = Len(u, v); return Edge(1f - l, 1f / 64f) * Edge(l - 0.86f, 1f / 64f); });

        /// <summary>128 px slash arc facing +x: a 120° sweep with a 10% stroke, drawn from -70° to +50°.</summary>
        public static Sprite Arc => arc ??= Make(128, 128, (u, v) =>
        {
            float l = Len(u, v);
            float a = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
            float ring = Edge(1f - l, 1f / 64f) * Edge(l - 0.82f, 1f / 64f);
            float sweep = Edge(a + 70f, 6f) * Edge(50f - a, 6f);
            return ring * sweep;
        });

        static float Len(float u, float v) => Mathf.Sqrt(u * u + v * v);
        static float Edge(float d, float soft) => Mathf.Clamp01(d / soft);

        static Sprite Make(int w, int h, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha(u, v)));
                    px[y * w + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
