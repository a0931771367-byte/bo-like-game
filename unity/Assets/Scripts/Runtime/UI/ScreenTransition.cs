using System;
using BoLike.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BoLike.UI
{
    /// <summary>
    /// Room / scene change (罐子 11/3 #3 #4): instead of a flat white flash that only covered the safe area, a set of
    /// dark shutter bars drops over the whole screen with a glowing leading edge, a loading card sits in the middle
    /// (spinning stardust, eight pulsing dots, the next room's name, a tip and a progress bar), then the bars lift
    /// again. <see cref="Play"/> calls <c>onCovered</c> while the screen is fully hidden, which is when the caller
    /// swaps rooms or scenes, and <c>onDone</c> once the picture is back. Runs on unscaled time so a paused world
    /// doesn't freeze it.
    /// </summary>
    public class ScreenTransition : MonoBehaviour
    {
        const int Bars = 7;
        const float BarSeconds = 0.26f, Stagger = 0.03f;
        const float CoverSeconds = BarSeconds + Stagger * (Bars - 1);

        static ScreenTransition current;
        /// <summary>True while a transition owns the screen; input handlers use it to ignore taps.</summary>
        public static bool Playing => current != null;

        Canvas canvas;
        RectTransform[] bars;
        CanvasGroup card;
        Image spinner, progress;
        Image[] dots;
        Text loadingText;
        float hold, t;
        bool covered, revealing;
        float revealAt;
        Action onCovered, onDone;

        /// <summary>
        /// <paramref name="title"/> is the big line (room or chapter name), <paramref name="tip"/> a one-line hint under it
        /// (may be null). <paramref name="hold"/> is how long the loading card stays once the screen is covered.
        /// </summary>
        public static ScreenTransition Play(string title, string tip, Action onCovered, Action onDone = null, float hold = 0.5f, int tint = 0x0b0d14)
        {
            if (current != null) { Destroy(current.gameObject); current = null; }
            var go = new GameObject("ScreenTransition");
            DontDestroyOnLoad(go);
            var st = go.AddComponent<ScreenTransition>();
            current = st;
            st.onCovered = onCovered;
            st.onDone = onDone;
            st.hold = Mathf.Max(0.05f, hold);
            st.Build(title, tip, tint);
            return st;
        }

        void Build(string title, string tip, int tint)
        {
            canvas = UiKit.Canvas("TransitionCanvas", 100);
            canvas.transform.SetParent(transform, false);
            var root = (RectTransform)canvas.transform;
            // Shutter bars, each a full-height column; they start above the screen and drop with a stagger.
            bars = new RectTransform[Bars];
            for (int i = 0; i < Bars; i++)
            {
                var img = UiKit.Rect(root, 0, 0, 10, 10, tint, 1f);
                var rt = img.rectTransform;
                // Overlap the columns by a hair so no seam shows between them on fractional pixel widths.
                rt.anchorMin = new Vector2(i / (float)Bars, 0); rt.anchorMax = new Vector2((i + 1) / (float)Bars, 1);
                rt.offsetMin = new Vector2(-1, 0); rt.offsetMax = new Vector2(1, 0);
                img.raycastTarget = true;
                // Leading edge: an accent line with a soft glow hanging beneath it.
                var glow = HangBelow(UiKit.Rect(img.transform, 0, 0, 10, 10, 0xf28c28, 0.35f), 48);
                glow.sprite = UiKit.VerticalFade;
                HangBelow(UiKit.Rect(img.transform, 0, 0, 10, 10, 0xffb060, 0.95f), 4);
                bars[i] = rt;
            }
            // Loading card (safe-area layer so it never sits under the notch).
            var layer = UiKit.Layer(canvas, "Card");
            card = layer.gameObject.AddComponent<CanvasGroup>();
            card.alpha = 0f;
            card.blocksRaycasts = false;
            float cy = UiKit.H / 2f;
            var star = Resources.Load<Sprite>("UI/stardust");
            spinner = star != null
                ? UiKit.Sprite(layer, star, 0, cy - 46, 64, 64, UiKit.Center, UiKit.Center)
                : UiKit.Sprite(layer, Game.SpriteBank.Ring, 0, cy - 46, 64, 64, UiKit.Center, UiKit.Center);
            spinner.rectTransform.anchoredPosition = new Vector2(0, 46);
            dots = new Image[8];
            for (int i = 0; i < dots.Length; i++)
            {
                float a = i / (float)dots.Length * Mathf.PI * 2f;
                var d = UiKit.Sprite(layer, Game.SpriteBank.Circle, 0, 0, 9, 9, UiKit.Center, UiKit.Center);
                d.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a) * 54f, 46 + Mathf.Cos(a) * 54f);
                d.color = Colors.Rgb(0xffd36b, 0.3f);
                dots[i] = d;
            }
            var titleText = UiKit.Text(layer, 0, 0, title ?? "", 30, 0xfff4de, UiKit.Center, UiKit.Center, TextAnchor.MiddleCenter, true, 3);
            titleText.rectTransform.anchoredPosition = new Vector2(0, -42);
            loadingText = UiKit.Text(layer, 0, 0, "LOADING", 14, 0x9aa0b4, UiKit.Center, UiKit.Center, TextAnchor.MiddleCenter, false, 1);
            loadingText.rectTransform.anchoredPosition = new Vector2(0, -74);
            if (!string.IsNullOrEmpty(tip))
            {
                var tipText = UiKit.Text(layer, 0, 0, tip, 16, 0xcfd4e6, UiKit.Center, UiKit.Center, TextAnchor.MiddleCenter, false, 1);
                tipText.rectTransform.anchoredPosition = new Vector2(0, -106);
                tipText.rectTransform.sizeDelta = new Vector2(760, 40);
                tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            var track = UiKit.Rect(layer, 0, 0, 260, 4, 0xffffff, 0.12f, UiKit.Center, UiKit.Center);
            track.rectTransform.anchoredPosition = new Vector2(0, -138);
            progress = UiKit.Rect(track.transform, 0, 0, 0, 4, 0xf28c28, 1f, UiKit.TopLeft, UiKit.TopLeft);
            Step(0f);
        }

        /// <summary>Anchors an image to the bottom edge of its parent, extending <paramref name="h"/> px below it.</summary>
        static Image HangBelow(Image img, float h)
        {
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -h); rt.offsetMax = new Vector2(0, 0);
            return img;
        }

        static float EaseOutCubic(float k) => 1 - Mathf.Pow(1 - k, 3);
        static float EaseInCubic(float k) => k * k * k;

        /// <summary>Positions every bar for the given phase time; cover and reveal share this with a reversed stagger.</summary>
        void Step(float phaseT)
        {
            float h = UiKit.H + 60f; // the canvas scales by height, so it is always 720 design px tall (the rect is still 0 on the build frame)
            for (int i = 0; i < Bars; i++)
            {
                int order = revealing ? Bars - 1 - i : i; // reveal lifts from the far side first, so the wipe reads as one sweep
                float k = Mathf.Clamp01((phaseT - order * Stagger) / BarSeconds);
                float y = revealing ? EaseInCubic(k) * h : (1 - EaseOutCubic(k)) * h;
                bars[i].anchoredPosition = new Vector2(0, y);
            }
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (!revealing)
            {
                Step(t);
                if (!covered && t >= CoverSeconds)
                {
                    covered = true;
                    revealAt = t + hold;
                    var cb = onCovered; onCovered = null;
                    try { cb?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                }
                // The card fades in while the last bars land and out just before the reveal.
                float inK = Mathf.Clamp01((t - CoverSeconds * 0.55f) / 0.22f);
                float outK = covered ? Mathf.Clamp01((revealAt - t) / 0.16f) : 1f;
                card.alpha = Mathf.Min(inK, outK);
                if (covered) progress.rectTransform.sizeDelta = new Vector2(260f * Mathf.Clamp01(1f - (revealAt - t) / hold), 4);
                if (covered && t >= revealAt) { revealing = true; t = 0; card.alpha = 0; }
            }
            else
            {
                Step(t);
                if (t >= CoverSeconds + 0.02f)
                {
                    var cb = onDone; onDone = null;
                    if (current == this) current = null;
                    Destroy(gameObject);
                    try { cb?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                    return;
                }
            }
            // Card animation: stardust spins and breathes, the dot ring chases, the LOADING label counts its dots.
            float now = Time.unscaledTime;
            spinner.rectTransform.localRotation = Quaternion.Euler(0, 0, -now * 160f);
            spinner.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(now * 5f) * 0.06f);
            for (int i = 0; i < dots.Length; i++)
            {
                float ph = Mathf.Repeat(now * 1.4f - i / (float)dots.Length, 1f);
                float a = 0.25f + 0.75f * Mathf.Pow(1f - ph, 3);
                dots[i].color = Colors.Rgb(0xffd36b, a);
                dots[i].rectTransform.localScale = Vector3.one * (0.8f + 0.5f * Mathf.Pow(1f - ph, 3));
            }
            loadingText.text = "LOADING" + new string('.', 1 + (int)(now * 3f) % 3);
        }

        void OnDestroy() { if (current == this) current = null; }
    }
}
