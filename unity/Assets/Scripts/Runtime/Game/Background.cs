using BoLike.Core;
using UnityEngine;

namespace BoLike.Game
{
    /// <summary>
    /// The stage painting split at bgGroundY: the sky half scrolls at half speed (parallax), the ground strip is
    /// tiled under the fighters and stretched to the walkable area. Port of GameScene.drawBackground().
    /// </summary>
    public class Background : MonoBehaviour
    {
        SpriteRenderer sky, ground;
        float groundY;

        public static Background Create(Transform parent, string bgKey, float groundYPx, float stageWidth, int? tint)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Background>();
            b.Build(bgKey, groundYPx, stageWidth, tint);
            return b;
        }

        void Build(string bgKey, float groundYPx, float stageWidth, int? tint)
        {
            var tex = SpriteBank.Background(bgKey);
            if (tex == null) { Debug.LogWarning($"Background: missing Bg/{bgKey}"); return; }
            groundY = groundYPx;
            float W = Units.ViewWidthPx * 1.5f; // extra so the widest phones never see the edge
            var skySprite = Sprite.Create(tex, new Rect(0, tex.height - groundYPx, tex.width, groundYPx), new Vector2(0, 1), 100f, 0, SpriteMeshType.FullRect);
            var groundSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height - groundYPx), new Vector2(0, 1), 100f, 0, SpriteMeshType.FullRect);

            sky = Make("Sky", skySprite, Space.OrderSky);
            sky.drawMode = SpriteDrawMode.Tiled;
            sky.size = new Vector2((stageWidth / 2f + W) * Space.Scale, groundYPx * Space.Scale);

            // The ground strip is drawn at its native scale (the web version stretched it ~1.6× vertically, which is
            // why grass and dirt looked smeared next to the crisp characters). Anything below the strip is filled with
            // the painting's bottom-row colour so short strips still reach the bottom of tall phone screens.
            ground = Make("Ground", groundSprite, Space.OrderGround);
            ground.drawMode = SpriteDrawMode.Tiled;
            float top = Units.GroundTopPx - 30;
            float gh = Units.ViewHeightPx + 200 - top; // 200 px of slack under the view for safe-area / aspect differences
            float srcH = tex.height - groundYPx;
            ground.size = new Vector2((stageWidth + W) * Space.Scale, srcH * Space.Scale);
            ground.transform.localPosition = new Vector3(0, -top * Space.Scale, 0);
            if (gh > srcH)
            {
                var fill = Make("GroundFill", SpriteBank.Pixel, Space.OrderGround - 1);
                fill.color = BottomRowColour(tex);
                float fillH = gh - srcH + 1;
                fill.transform.localPosition = new Vector3(0, -(top + srcH - 1 + fillH / 2) * Space.Scale, 0); // Pixel's pivot is its centre
                fill.transform.localScale = new Vector3((stageWidth + W) * Space.Scale / PixelSize(), fillH * Space.Scale / PixelSize(), 1);
                if (tint.HasValue) fill.color *= Colors.Rgb(tint.Value);
            }

            if (tint.HasValue) { sky.color = Colors.Rgb(tint.Value); ground.color = Colors.Rgb(tint.Value); }
        }

        static float PixelSize() => SpriteBank.Pixel.bounds.size.x;

        /// <summary>Average colour of the painting's bottom row. Compressed iOS textures aren't readable, so the
        /// shipped paintings are tabled (measured from the PNGs); anything else falls back to dark soil.</summary>
        static Color BottomRowColour(Texture2D tex)
        {
            switch (tex.name)
            {
                case "chapter1": return new Color(0.326f, 0.248f, 0.18f);
                case "chapter2": return new Color(0.143f, 0.149f, 0.13f);
            }
            try
            {
                var px = tex.GetPixels(0, 0, tex.width, 1);
                Color sum = Color.black;
                foreach (var c in px) sum += c;
                return sum / Mathf.Max(1, px.Length);
            }
            catch (UnityException) { return new Color(0.26f, 0.18f, 0.12f); }
        }

        SpriteRenderer Make(string name, Sprite s, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Call after the camera moves: the sky layer scrolls at half speed.</summary>
        public void Parallax(float scrollXPx)
        {
            if (sky != null) sky.transform.localPosition = new Vector3(scrollXPx * 0.5f * Space.Scale, 0, 0);
        }
    }
}
