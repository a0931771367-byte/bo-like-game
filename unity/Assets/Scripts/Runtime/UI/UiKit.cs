using BoLike.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BoLike.UI
{
    /// <summary>
    /// Small helpers for building uGUI from code in the 1280×720 design space with a top-left origin,
    /// so HUD coordinates can be copied from the Phaser build. Text uses the OS CJK font (PingFang on
    /// iOS/macOS) so no font asset needs shipping for the beta.
    /// </summary>
    public static class UiKit
    {
        public const float W = Units.ViewWidthPx, H = Units.ViewHeightPx;
        static Font font;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                string[] names = { "PingFang TC", "PingFang SC", "Noto Sans CJK TC", "Noto Sans TC", "Microsoft JhengHei", "Heiti TC", "Hiragino Sans", "Arial Unicode MS" };
                try { font = Font.CreateDynamicFontFromOSFont(names, 32); } catch { font = null; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Object.DontDestroyOnLoad(go);
        }

        public static Canvas Canvas(string name, int sortOrder)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // height is fixed at 720 like the game camera; width grows with the aspect
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>A full-canvas child whose children use top-left coordinates. Width follows the real aspect.</summary>
        public static RectTransform Layer(Canvas canvas, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Design-space width of the canvas for the current aspect (≥ 1280 on phones).</summary>
        public static float CanvasWidth(Canvas c) => ((RectTransform)c.transform).rect.width;

        static RectTransform Place(RectTransform rt, float x, float y, float w, float h, Vector2 pivot, Vector2 anchor)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        public static readonly Vector2 TopLeft = new(0, 1), TopRight = new(1, 1), BottomLeft = new(0, 0), BottomRight = new(1, 0), Center = new(0.5f, 0.5f), TopCenter = new(0.5f, 1), MiddleRight = new(1, 0.5f);

        /// <summary>Solid rectangle. x,y are measured from the anchor corner, downwards positive.</summary>
        public static Image Rect(Transform parent, float x, float y, float w, float h, int color, float alpha = 1f, Vector2? anchor = null, Vector2? pivot = null)
        {
            var go = new GameObject("Rect", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, x, y, w, h, pivot ?? TopLeft, anchor ?? TopLeft);
            var img = go.AddComponent<Image>();
            img.color = Colors.Rgb(color, alpha);
            img.raycastTarget = false;
            return img;
        }

        public static Image Sprite(Transform parent, Sprite sprite, float x, float y, float w, float h, Vector2? anchor = null, Vector2? pivot = null)
        {
            var img = Rect(parent, x, y, w, h, 0xffffff, 1f, anchor, pivot);
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        public static Text Text(Transform parent, float x, float y, string text, int size, int color, Vector2? anchor = null, Vector2? pivot = null, TextAnchor align = TextAnchor.UpperLeft, bool bold = false, int outline = 2)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, x, y, 900, size * 1.6f, pivot ?? TopLeft, anchor ?? TopLeft);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.color = Colors.Rgb(color);
            t.text = text;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline > 0)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.9f);
                o.effectDistance = new Vector2(outline, -outline);
            }
            return t;
        }

        public static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        /// <summary>A clickable rounded-ish button with a label. Works with touch and mouse.</summary>
        public static Button Button(Transform parent, float x, float y, float w, float h, string label, int color, System.Action onClick, int size = 22, Vector2? anchor = null, Vector2? pivot = null)
        {
            var img = Rect(parent, x, y, w, h, color, 0.9f, anchor, pivot);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1);
            btn.colors = colors;
            Text(img.transform, 0, 0, label, size, 0xffffff, Center, Center, TextAnchor.MiddleCenter, true);
            btn.onClick.AddListener(() => { BoLike.Game.AudioManager.Instance.Play("select"); onClick?.Invoke(); });
            return btn;
        }

        /// <summary>Full-screen dim panel that blocks clicks underneath.</summary>
        public static Image Dim(Transform parent, float alpha = 0.7f)
        {
            var img = Rect(parent, 0, 0, 10, 10, 0x05060a, alpha);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            img.raycastTarget = true;
            return img;
        }
    }
}
