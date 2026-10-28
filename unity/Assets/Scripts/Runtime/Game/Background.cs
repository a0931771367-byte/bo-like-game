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

            ground = Make("Ground", groundSprite, Space.OrderGround);
            ground.drawMode = SpriteDrawMode.Tiled;
            float gh = Units.ViewHeightPx - (Units.GroundTopPx - 30);
            float srcH = tex.height - groundYPx;
            ground.size = new Vector2((stageWidth + W) * Space.Scale, srcH * Space.Scale);
            ground.transform.localScale = new Vector3(1, gh / srcH, 1);
            ground.transform.localPosition = new Vector3(0, -(Units.GroundTopPx - 30) * Space.Scale, 0);

            if (tint.HasValue) { sky.color = Colors.Rgb(tint.Value); ground.color = Colors.Rgb(tint.Value); }
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
