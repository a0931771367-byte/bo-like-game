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
        public static RectTransform Layer(Canvas canvas, string name) => Layer(canvas.transform, name);

        /// <summary>Safe-area layer under any full-screen parent (a modal root, for instance).</summary>
        public static RectTransform Layer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            // Keep controls and HUD inside the iPhone safe area (notch / home indicator); backgrounds use the canvas root instead.
            // The fitter re-applies the anchors whenever the safe area or screen size changes: on iOS the first frames
            // after launch (or a rotation) can report stale portrait values, which squeezed every button into the middle.
            go.AddComponent<SafeAreaFitter>().Apply();
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

        public static readonly Vector2 TopLeft = new(0, 1), TopRight = new(1, 1), BottomLeft = new(0, 0), BottomRight = new(1, 0), Center = new(0.5f, 0.5f), TopCenter = new(0.5f, 1), BottomCenter = new(0.5f, 0), MiddleRight = new(1, 0.5f);

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
            Stretch(img.rectTransform);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>Anchors a RectTransform to the four corners of its parent.</summary>
        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Sprite vignette;

        /// <summary>
        /// 256² radial gradient: clear in the middle, dark at the corners. Stretched over the screen on top of a flat dim
        /// it gives overlays a soft cinematic falloff instead of a flat grey wash.
        /// </summary>
        public static Sprite Vignette
        {
            get
            {
                if (vignette != null) return vignette;
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(u * u + v * v) / 1.25f;   // corners ≈ 1.13 → clamp
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.25f) / 0.85f));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return vignette;
            }
        }

        static Sprite verticalFade;

        /// <summary>4×64 strip, opaque at the top fading to clear at the bottom (glows under edges, text plates).</summary>
        public static Sprite VerticalFade
        {
            get
            {
                if (verticalFade != null) return verticalFade;
                const int h = 64;
                var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[4 * h];
                for (int y = 0; y < h; y++)
                {
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Pow((y + 0.5f) / h, 1.6f)); // row 0 is the bottom
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                verticalFade = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return verticalFade;
            }
        }

        /// <summary>
        /// A modal overlay that covers the *whole* screen (including the notch strips outside the safe area) and blocks
        /// clicks: a flat dim plus a vignette, with a safe-area layer on top for the content. Returns that content layer;
        /// close it with <see cref="CloseModal"/>. With <paramref name="hide"/> set, the layer underneath is faded out
        /// while the modal is open (title screen panels), so its text never shows through the dim.
        /// </summary>
        public static RectTransform Modal(Transform canvasOrLayer, float dimAlpha = 0.7f, RectTransform hide = null)
        {
            var root = new GameObject("Modal", typeof(RectTransform));
            var rrt = root.GetComponent<RectTransform>();
            // Mount on the canvas itself so the dim is not clipped to the safe area of a layer.
            Transform mount = canvasOrLayer;
            while (mount.parent != null && mount.GetComponent<Canvas>() == null) mount = mount.parent;
            rrt.SetParent(mount, false);
            Stretch(rrt);
            root.transform.SetAsLastSibling();
            var m = root.AddComponent<ModalRoot>();
            var dim = Rect(rrt, 0, 0, 10, 10, 0x05060a, Mathf.Clamp01(dimAlpha * 0.82f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var vig = Rect(rrt, 0, 0, 10, 10, 0x05060a, Mathf.Clamp01(dimAlpha * 0.75f));
            Stretch(vig.rectTransform);
            vig.sprite = Vignette;
            vig.type = Image.Type.Simple;
            vig.preserveAspect = false;
            if (hide != null)
            {
                var cg = hide.GetComponent<CanvasGroup>() ?? hide.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
                m.hidden = cg;
            }
            m.content = Layer(rrt, "Content");
            m.FadeIn();
            return m.content;
        }

        /// <summary>Destroys the modal that <paramref name="inside"/> belongs to (any transform under it works).</summary>
        public static void CloseModal(Transform inside)
        {
            var m = inside != null ? inside.GetComponentInParent<ModalRoot>() : null;
            if (m != null) Object.Destroy(m.gameObject);
        }
    }

    /// <summary>Root of a <see cref="UiKit.Modal"/>: fades in over a few frames and restores whatever it hid when destroyed.</summary>
    public class ModalRoot : MonoBehaviour
    {
        public RectTransform content;
        public CanvasGroup hidden;
        CanvasGroup group;
        float t;
        const float FadeSeconds = 0.18f;

        public void FadeIn()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
        }

        void Update()
        {
            if (group == null || group.alpha >= 1f) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FadeSeconds);
            group.alpha = k * k * (3 - 2 * k);
            if (content != null) content.localScale = Vector3.one * Mathf.Lerp(1.03f, 1f, k);
        }

        void OnDestroy()
        {
            if (hidden != null) { hidden.alpha = 1f; hidden.interactable = true; hidden.blocksRaycasts = true; }
        }
    }

    /// <summary>Anchors a full-canvas RectTransform to Screen.safeArea and keeps it in sync when the screen changes.</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect lastSafe; int lastW, lastH;

        void OnEnable() => Apply();
        void Update()
        {
            if (Screen.safeArea != lastSafe || Screen.width != lastW || Screen.height != lastH) Apply();
        }

        public void Apply()
        {
            var rt = (RectTransform)transform;
            var safe = Screen.safeArea;
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            // A safe area that doesn't fit the current screen (stale after a rotation) is worse than none: fall back to full screen.
            if (safe.width <= 0 || safe.height <= 0 || safe.xMax > sw + 0.5f || safe.yMax > sh + 0.5f || safe.width < sw * 0.5f || safe.height < sh * 0.5f)
                safe = new Rect(0, 0, sw, sh);
            lastSafe = Screen.safeArea; lastW = Screen.width; lastH = Screen.height;
            rt.anchorMin = new Vector2(safe.xMin / sw, safe.yMin / sh);
            rt.anchorMax = new Vector2(safe.xMax / sw, safe.yMax / sh);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
