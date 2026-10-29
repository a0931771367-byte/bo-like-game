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
        readonly System.Collections.Generic.List<(Image cover, Image icon, Image locked, int skill)> skillCovers = new();
        World world;

        public static TouchControls Create(World world)
        {
            var go = new GameObject("TouchControls");
            var t = go.AddComponent<TouchControls>();
            t.world = world;
            t.Build();
            return t;
        }

        /// <summary>Also true in the editor so the on-screen controls can be checked with the mouse before a device build.</summary>
        public static bool IsTouchDevice => Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null || Application.isEditor;

        void Build()
        {
            UiKit.EnsureEventSystem();
            canvas = UiKit.Canvas("TouchCanvas", 20);
            canvas.transform.SetParent(transform, false);
            layer = UiKit.Layer(canvas, "Layer");
            float H = UiKit.H;

            // Floating stick: touch anywhere on the left 45 % of the screen and the stick appears under the finger.
            var zone = UiKit.Rect(layer, 0, 0, 10, 10, 0xffffff, 0f);
            var zrt = zone.rectTransform; zrt.anchorMin = Vector2.zero; zrt.anchorMax = new Vector2(0.45f, 1f); zrt.offsetMin = zrt.offsetMax = Vector2.zero;
            zone.raycastTarget = true;
            var stickBase = Circle(180, -170, 78, 0xffffff, 0.12f, UiKit.BottomLeft);
            stickBase.raycastTarget = false;
            var knob = Circle(0, 0, 38, 0xffffff, 0.35f, UiKit.Center, stickBase.transform);
            knob.raycastTarget = false;
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 60;
            var floating = zone.gameObject.AddComponent<FloatingStickZone>();
            floating.stick = stick;
            floating.stickBase = stickBase.rectTransform;
            floating.layer = layer;
            floating.restPosition = stickBase.rectTransform.anchoredPosition;
            floating.restAnchor = stickBase.rectTransform.anchorMin;
            floating.Init();

            // Action buttons (right).
            // Layout checked so no two circles overlap (centre distance > r1 + r2): attack under the thumb, jump
            // below-left, guard above-left, the skill row above all of them, potions moved up under the pause button.
            Button(-130, -130, 62, 0xff6a2b, "攻擊", "<Gamepad>/buttonWest", 22);
            Button(-275, -80, 44, 0x4da3ff, "跳", "<Gamepad>/buttonSouth", 20);
            Button(-255, -200, 40, 0x9be15d, "防", "<Gamepad>/rightTrigger", 20);
            Button(-50, 110, 30, 0xff5d5d, "紅", "<Gamepad>/leftTrigger", 14, UiKit.TopRight);
            Button(-120, 110, 30, 0x5d8bff, "藍", "<Gamepad>/select", 14, UiKit.TopRight);

            // Skill row above the cluster, with icons and cooldown covers.
            string[] paths = { "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast", "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder" };
            var cls = world.Player.cls;
            for (int i = 0; i < cls.skills.Count && i < 4; i++)
            {
                float x = -340 + i * 80, y = -310;
                var b = Circle(x, y, 34, 0x000000, 0.5f, UiKit.BottomRight);
                b.raycastTarget = true;
                var icon = UiKit.Sprite(b.transform, Game.SpriteBank.Icon(cls.id, i), 0, 0, 60, 60, UiKit.Center, UiKit.Center);
                var cover = Circle(0, 0, 34, 0x000000, 0.65f, UiKit.Center, b.transform);
                cover.type = Image.Type.Filled;
                cover.fillMethod = Image.FillMethod.Radial360;
                cover.fillOrigin = (int)Image.Origin360.Top;
                cover.fillClockwise = false;
                var lockSprite = Resources.Load<Sprite>("Icons/skill_locked");
                Image lockImg = lockSprite != null ? UiKit.Sprite(b.transform, lockSprite, 0, 0, 44, 44, UiKit.Center, UiKit.Center) : null;
                skillCovers.Add((cover, icon, lockImg, i));
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
            var img = Circle(x, y, r, color, 0.45f, anchor ?? UiKit.BottomRight);
            img.raycastTarget = true;
            UiKit.Text(img.transform, 0, 0, label, size, 0xffffff, UiKit.Center, UiKit.Center, TextAnchor.MiddleCenter, true);
            var btn = img.gameObject.AddComponent<OnScreenButton>();
            btn.controlPath = path;
        }

        /// <summary>Moves the stick under the first touch in the zone and forwards the drag to the OnScreenStick.</summary>
        public class FloatingStickZone : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IPointerUpHandler
        {
            public OnScreenStick stick;
            public RectTransform stickBase, layer;
            public Vector2 restPosition, restAnchor;
            CanvasGroup group;
            int pointerId = int.MinValue;

            // Fields are assigned after AddComponent (so after Awake): Build() calls Init() once they are set.
            // Unity's "fake null" means `?? AddComponent` would not work here; compare with == null instead.
            public void Init()
            {
                var g = stickBase.GetComponent<CanvasGroup>();
                if (g == null) g = stickBase.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0.6f;
                g.blocksRaycasts = false;
                group = g;
            }

            CanvasGroup Group
            {
                get
                {
                    if (group == null) Init();
                    return group;
                }
            }

            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
            {
                if (pointerId != int.MinValue) return;
                pointerId = e.pointerId;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, e.position, e.pressEventCamera, out var local))
                {
                    stickBase.anchorMin = stickBase.anchorMax = new Vector2(0.5f, 0.5f);
                    stickBase.anchoredPosition = local;
                }
                Group.alpha = 1f;
                stick.OnPointerDown(e);
            }

            public void OnDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                if (e.pointerId == pointerId) stick.OnDrag(e);
            }

            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e)
            {
                if (e.pointerId != pointerId) return;
                pointerId = int.MinValue;
                stick.OnPointerUp(e);
                stickBase.anchorMin = stickBase.anchorMax = restAnchor;
                stickBase.anchoredPosition = restPosition;
                Group.alpha = 0.6f;
            }
        }

        void Update()
        {
            var p = world.Player;
            if (p == null) return;
            foreach (var (cover, icon, lockImg, i) in skillCovers)
            {
                var s = p.cls.skills[i];
                float left = p.CooldownLeft(s);
                bool learned = p.SkillLv(s) > 0;
                // Locked skills show the lock over a dimmed icon (art spec: icon at 40%); the cooldown cover is for learned ones.
                cover.fillAmount = learned ? (left > 0 ? Mathf.Min(1, left / p.CooldownTotal(s)) : 0) : (lockImg != null ? 0f : 0.85f);
                UiKit.SetAlpha(icon, learned ? 1f : 0.4f);
                if (lockImg != null) UiKit.SetAlpha(lockImg, learned ? 0f : 0.9f);
            }
        }
    }
}
