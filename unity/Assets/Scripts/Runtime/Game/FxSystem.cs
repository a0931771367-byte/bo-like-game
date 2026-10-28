using System;
using BoLike.Core;
using BoLike.Sim;
using UnityEngine;

namespace BoLike.Game
{
    /// <summary>A short-lived effect driven by one curve from 0 to 1 over its lifetime, then destroyed.</summary>
    public class FxItem : MonoBehaviour
    {
        public float duration = 0.3f, delay;
        public Action<FxItem, float> tick;
        float t;
        public SpriteRenderer sr;
        public TextMesh text;
        public Vector3 start;

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01((t - delay) / duration);
            if (t >= delay) tick?.Invoke(this, k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Port of ui/Fx.ts: world-space sparks, slashes, rings, dust, numbers and screen banners.</summary>
    public class FxSystem : IFx
    {
        readonly Transform root;
        readonly GameCamera cam;
        readonly UI.Hud hud;
        readonly World world;
        readonly System.Random rand = new();

        public FxSystem(Transform root, GameCamera cam, UI.Hud hud, World world)
        {
            this.root = root; this.cam = cam; this.hud = hud; this.world = world;
        }

        static float EaseOutCubic(float k) => 1 - Mathf.Pow(1 - k, 3);
        static float EaseOutBack(float k) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2); }
        float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);

        FxItem Make(string name, Sprite sprite, float px, float py, int color, float alpha, int order, float duration, float delay = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = Space.ToWorld(px, py);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Colors.Rgb(color, alpha);
            sr.sortingOrder = order;
            var item = go.AddComponent<FxItem>();
            item.sr = sr;
            item.duration = duration;
            item.delay = delay;
            item.start = go.transform.position;
            return item;
        }

        public void Number(float x, float y, string text, int color, bool big = false)
        {
            var go = new GameObject("Number");
            go.transform.SetParent(root, false);
            go.transform.position = Space.ToWorld(x + R(-10, 10), y);
            var tm = go.AddComponent<TextMesh>();
            tm.font = UI.UiKit.Font;
            tm.fontSize = 64;
            tm.characterSize = (big ? 38f : 26f) / 64f * 0.1f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = text;
            tm.color = Colors.Rgb(color);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingOrder = Space.OrderFxText;
            mr.sharedMaterial = UI.UiKit.Font.material;
            // Fake outline: a dark copy offset behind the text.
            var shadow = UnityEngine.Object.Instantiate(go, go.transform);
            UnityEngine.Object.Destroy(shadow.GetComponent<FxItem>());
            shadow.transform.localPosition = new Vector3(0.02f, -0.02f, 0);
            shadow.GetComponent<TextMesh>().color = Colors.Rgb(0x1a1020);
            shadow.GetComponent<MeshRenderer>().sortingOrder = Space.OrderFxText - 1;
            var item = go.AddComponent<FxItem>();
            item.text = tm;
            item.duration = 0.9f;
            item.start = go.transform.position;
            float popScale = big ? 1.4f : 1.15f;
            item.tick = (it, k) =>
            {
                float pop = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(k / 0.13f));
                it.transform.localScale = Vector3.one * pop;
                float rise = Mathf.Clamp01((k - 0.28f) / 0.72f);
                it.transform.position = it.start + new Vector3(0, EaseOutCubic(rise) * 0.7f, 0);
                var c = tm.color; c.a = 1 - rise; tm.color = c;
                var sc = shadow.GetComponent<TextMesh>(); var cc = sc.color; cc.a = 1 - rise; sc.color = cc;
            };
        }

        public void Spark(float x, float y, int color = 0xffffff, float size = 1f)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = R(0, Mathf.PI * 2);
                var it = Make("Spark", SpriteBank.Pixel, x, y, color, 1, Space.OrderFx, 0.22f);
                it.transform.localScale = new Vector3(10 * size / 4f, 3 * size / 4f, 1);
                it.transform.rotation = Quaternion.Euler(0, 0, -a * Mathf.Rad2Deg);
                var end = it.start + new Vector3(Mathf.Cos(a) * 0.55f * size, -Mathf.Sin(a) * 0.55f * size, 0);
                it.tick = (o, k) =>
                {
                    float e = EaseOutCubic(k);
                    o.transform.position = Vector3.Lerp(o.start, end, e);
                    o.transform.localScale = new Vector3(10 * size / 4f * Mathf.Lerp(1, 0.2f, e), 3 * size / 4f, 1);
                    var c = o.sr.color; c.a = 1 - e; o.sr.color = c;
                };
            }
            var flash = Make("Flash", SpriteBank.Circle, x, y, 0xffffff, 0.9f, Space.OrderFx, 0.14f);
            float r0 = 16 * size / 32f;
            flash.tick = (o, k) => { o.transform.localScale = Vector3.one * r0 * Mathf.Lerp(1, 1.8f, k); var c = o.sr.color; c.a = 0.9f * (1 - k); o.sr.color = c; };
        }

        public void Slash(float x, float y, int facing, int color, float radius, float depthY)
        {
            var it = Make("Slash", SpriteBank.Arc, x, y, color, 0.9f, Space.Order(depthY) + 1, 0.16f);
            it.transform.localScale = new Vector3(radius / 64f * facing, radius / 64f, 1);
            it.tick = (o, k) => { var c = o.sr.color; c.a = 0.9f * (1 - k); o.sr.color = c; };
        }

        public void Ring(float x, float y, float radius, int color, float depthY)
        {
            var it = Make("Ring", SpriteBank.Ring, x, y, color, 0.8f, Space.Order(depthY), 0.36f);
            it.tick = (o, k) =>
            {
                float e = EaseOutCubic(k);
                float wpx = Mathf.Lerp(20, radius * 2, e), hpx = Mathf.Lerp(9, radius * 0.9f, e);
                o.transform.localScale = new Vector3(wpx / 128f, hpx / 128f, 1);
                var c = o.sr.color; c.a = 0.8f * (1 - k); o.sr.color = c;
            };
        }

        public void Pillar(float x, float y, int color)
        {
            var r = Make("Pillar", SpriteBank.Pixel, x, y, color, 0.9f, Space.Order(y) + 2, 0.26f);
            r.transform.localScale = new Vector3(26 / 4f, 520 / 4f, 1);
            r.transform.position = Space.ToWorld(x, y - 260);
            r.tick = (o, k) => { o.transform.localScale = new Vector3(26 / 4f * Mathf.Lerp(1, 0.3f, k), 520 / 4f, 1); var c = o.sr.color; c.a = 0.9f * (1 - k); o.sr.color = c; };
            var g = Make("PillarGlow", SpriteBank.SoftCircle, x, y, color, 0.6f, Space.Order(y) + 1, 0.26f);
            g.transform.localScale = new Vector3(140 / 64f, 46 / 64f, 1);
            g.tick = (o, k) => { var c = o.sr.color; c.a = 0.6f * (1 - k); o.sr.color = c; };
        }

        public void Dust(float x, float y, int n = 5, float size = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                float px = x + R(-12, 12), py = y - R(0, 6);
                float rad = (6 + R(0, 6)) * size;
                var it = Make("Dust", SpriteBank.Circle, px, py, 0xf2ead8, 0.7f, Space.Order(y) + 1, 0.32f + R(0, 0.12f));
                float dir = (i % 2 == 1 ? 1 : -1) * (0.5f + R(0, 1));
                var end = it.start + new Vector3(dir * 0.34f * size, (10 + R(0, 18)) * size * Space.Scale, 0);
                it.tick = (o, k) =>
                {
                    float e = EaseOutCubic(k);
                    o.transform.position = Vector3.Lerp(o.start, end, e);
                    o.transform.localScale = Vector3.one * (rad / 32f) * Mathf.Lerp(1, 1.8f, e);
                    var c = o.sr.color; c.a = 0.7f * (1 - e); o.sr.color = c;
                };
            }
        }

        /// <summary>A fading copy of the fighter's sprite (dash afterimage).</summary>
        public void Ghost(Fighter f)
        {
            if (!views.TryGetValue(f, out var v) || v.Body.sprite == null) return;
            int tint = f is Player p ? p.cls.accent : 0xffffff;
            var it = Make("Ghost", v.Body.sprite, f.x, f.y - f.z, tint, 0.45f, Space.Order(f.y) - 1, 0.26f);
            it.transform.rotation = v.View.rotation;
            it.transform.localScale = new Vector3(v.BodyScale * f.facing, v.BodyScale, 1);
            it.sr.material = SpriteBank.FlashMaterial;
            var mpb = new MaterialPropertyBlock();
            mpb.SetFloat("_Flash", 1f);
            mpb.SetColor("_FlashColor", Colors.Rgb(tint));
            it.sr.SetPropertyBlock(mpb);
            it.tick = (o, k) => { var c = o.sr.color; c.a = 0.45f * (1 - k); o.sr.color = c; };
        }

        [System.NonSerialized] public System.Collections.Generic.Dictionary<Fighter, FighterView> views = new();

        public void Flash(float alpha = 0.8f, float ms = 260f) => hud.ScreenFlash(alpha, ms / 1000f);

        public void Aura(float x, float y, int color)
        {
            for (int i = 0; i < 10; i++)
            {
                var it = Make("Aura", SpriteBank.Pixel, x + R(-30, 30), y - R(0, 30), color, 0.9f, Space.OrderFx, 0.5f, i * 0.03f);
                it.transform.localScale = new Vector3(6 / 4f, 14 / 4f, 1);
                it.tick = (o, k) => { o.transform.position = o.start + new Vector3(0, 0.9f * k, 0); var c = o.sr.color; c.a = 0.9f * (1 - k); o.sr.color = c; };
            }
        }

        public void Banner(string text, int color = 0xffffff, int size = 64) => hud.Banner(text, color, size);

        public void Shake(float ms, float intensity) => cam.Shake(ms / 1000f, intensity);
    }
}
