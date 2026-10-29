using BoLike.Core;
using BoLike.Sim;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace BoLike.UI
{
    /// <summary>
    /// Landscape on-screen controls: a stick on the left, an action cluster and skill row on the right.
    /// They are Input System on-screen controls bound to the gamepad paths in <see cref="Systems.GameInput"/>,
    /// so touch feeds the same actions as a real controller.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        Canvas canvas;
        RectTransform layer;
        readonly System.Collections.Generic.List<(Image cover, int skill)> skillCovers = new();
        World world;

        public static TouchControls Create(World world)
        {
            var go = new GameObject("TouchControls");
            var t = go.AddComponent<TouchControls>();
            t.world = world;
            t.Build();
            return t;
        }

        public static bool IsTouchDevice => Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;

        void Build()
        {
            UiKit.EnsureEventSystem();
            canvas = UiKit.Canvas("TouchCanvas", 20);
            canvas.transform.SetParent(transform, false);
            layer = UiKit.Layer(canvas, "Layer");
            float H = UiKit.H;

            // Stick: base at bottom-left, knob drags within 60 px.
            var stickBase = Circle(180, -170, 78, 0xffffff, 0.12f, UiKit.BottomLeft);
            stickBase.raycastTarget = true;
            var knob = Circle(0, 0, 38, 0xffffff, 0.35f, UiKit.Center, stickBase.transform);
            knob.raycastTarget = true;
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 60;

            // Action buttons (right).
            Button(-110, -110, 60, 0xff6a2b, "攻擊", "<Gamepad>/buttonWest", 22);
            Button(-230, -70, 44, 0x4da3ff, "跳", "<Gamepad>/buttonSouth", 20);
            Button(-215, -195, 44, 0x9be15d, "防", "<Gamepad>/rightTrigger", 20);
            Button(-60, -300, 30, 0xff5d5d, "紅", "<Gamepad>/leftTrigger", 14);
            Button(-130, -300, 30, 0x5d8bff, "藍", "<Gamepad>/select", 14);

            // Skill row above the cluster, with icons and cooldown covers.
            string[] paths = { "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast", "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder" };
            var cls = world.Player.cls;
            for (int i = 0; i < cls.skills.Count && i < 4; i++)
            {
                float x = -330 + i * 72, y = -215;
                var b = Circle(x, y, 32, 0x000000, 0.5f, UiKit.BottomRight);
                b.raycastTarget = true;
                var icon = UiKit.Sprite(b.transform, Game.SpriteBank.Icon(cls.id, i), 0, 0, 56, 56, UiKit.Center, UiKit.Center);
                var cover = Circle(0, 0, 32, 0x000000, 0.65f, UiKit.Center, b.transform);
                cover.type = Image.Type.Filled;
                cover.fillMethod = Image.FillMethod.Radial360;
                cover.fillOrigin = (int)Image.Origin360.Top;
                cover.fillClockwise = false;
                skillCovers.Add((cover, i));
                var btn = b.gameObject.AddComponent<OnScreenButton>();
                btn.controlPath = paths[i];
            }
            Button(-40, 40, 22, 0xffffff, "II", "<Gamepad>/start", 12, UiKit.TopRight);
        }

        Image Circle(float x, float y, float r, int color, float alpha, Vector2 anchor, Transform parent = null)
        {
            // Same convention as UiKit.Rect: y grows downwards, so bottom-anchored controls pass a negative y.
            var img = UiKit.Rect(parent ?? layer, x, y, r * 2, r * 2, color, alpha, anchor, UiKit.Center);
            img.sprite = Game.SpriteBank.Circle;
            img.raycastTarget = false;
            return img;
        }

        void Button(float x, float y, float r, int color, string label, string path, int size, Vector2? anchor = null)
        {
            var img = Circle(x, y, r, color, 0.55f, anchor ?? UiKit.BottomRight);
            img.raycastTarget = true;
            UiKit.Text(img.transform, 0, 0, label, size, 0xffffff, UiKit.Center, UiKit.Center, TextAnchor.MiddleCenter, true);
            var btn = img.gameObject.AddComponent<OnScreenButton>();
            btn.controlPath = path;
        }

        void Update()
        {
            var p = world.Player;
            if (p == null) return;
            foreach (var (cover, i) in skillCovers)
            {
                var s = p.cls.skills[i];
                float left = p.CooldownLeft(s);
                cover.fillAmount = p.SkillLv(s) > 0 ? (left > 0 ? Mathf.Min(1, left / p.CooldownTotal(s)) : 0) : 0.85f;
            }
        }
    }
}
