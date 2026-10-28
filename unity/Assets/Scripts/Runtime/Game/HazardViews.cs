using BoLike.Core;
using BoLike.Sim;
using UnityEngine;

namespace BoLike.Game
{
    /// <summary>Draws a projectile: an arrow shaft or a glowing orb. Destroys itself when the sim marks it done.</summary>
    public class ProjectileView : MonoBehaviour
    {
        Projectile p;
        Transform g;

        public static ProjectileView Create(Projectile p, Transform parent)
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ProjectileView>();
            v.p = p;
            v.Build();
            return v;
        }

        SpriteRenderer Part(Sprite s, float xPx, float yPx, float wPx, float hPx, int color, float alpha, int order, float sx = 1)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(g, false);
            go.transform.localPosition = new Vector3(xPx * Space.Scale, -yPx * Space.Scale, 0);
            float unit = s == SpriteBank.Pixel ? 4f : 64f;
            go.transform.localScale = new Vector3(wPx / unit, hPx / unit, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.color = Colors.Rgb(color, alpha); sr.sortingOrder = order;
            return sr;
        }

        void Build()
        {
            g = new GameObject("G").transform;
            g.SetParent(transform, false);
            int order = Space.Order(p.o.y) + 1;
            if (p.o.arrow)
            {
                Part(SpriteBank.Pixel, -6, 0, 52, 4, 0xd9c7a0, 1, order);
                Part(SpriteBank.Circle, 24, 0, 14, 8, 0xf4f4f4, 1, order + 1);
                Part(SpriteBank.Circle, -30, 0, 12, 12, p.o.color, 1, order);
                Part(SpriteBank.Pixel, -4, 0, 60, 10, p.o.color, 0.18f, order - 1);
            }
            else
            {
                Part(SpriteBank.SoftCircle, -10, 0, 46, 24, p.o.color, 0.35f, order);
                Part(SpriteBank.Circle, 0, 0, 34, 18, p.o.color, 1, order + 1);
                Part(SpriteBank.Circle, 6, 0, 16, 8, 0xffffff, 0.9f, order + 2);
            }
            Sync();
        }

        public void Sync()
        {
            transform.position = Space.ToWorld(p.o.x, p.o.y + p.Bob, p.o.z);
            g.localScale = new Vector3(p.o.dir, 1, 1);
        }

        void LateUpdate()
        {
            if (p.done) { Destroy(gameObject); return; }
            Sync();
        }
    }

    /// <summary>Draws a fire wall: five flickering flame triangles (approximated with stretched soft circles).</summary>
    public class WallView : MonoBehaviour
    {
        Wall w;
        readonly SpriteRenderer[] flames = new SpriteRenderer[5];
        float fadeStart = -1;

        public static WallView Create(Wall w, Transform parent)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<WallView>();
            v.w = w;
            v.Build();
            return v;
        }

        void Build()
        {
            transform.position = Space.ToWorld(w.o.x, w.o.y);
            int order = Space.Order(w.o.y);
            for (int i = -2; i <= 2; i++)
            {
                var go = new GameObject("Flame");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteBank.SoftCircle;
                sr.color = Colors.Rgb(w.o.color, 0.9f);
                sr.sortingOrder = order + (i == 0 ? 1 : 0);
                float h = 110 + Mathf.Abs(i) * 20;
                go.transform.localPosition = new Vector3(i * 20 * Space.Scale, h / 2 * Space.Scale, 0);
                go.transform.localScale = new Vector3(60 / 64f, h / 64f, 1);
                flames[i + 2] = sr;
            }
        }

        void LateUpdate()
        {
            if (w.done)
            {
                if (fadeStart < 0) fadeStart = Time.time;
                float k = (Time.time - fadeStart) / 0.2f;
                if (k >= 1) { Destroy(gameObject); return; }
                foreach (var f in flames) { var c = f.color; c.a = 0.9f * (1 - k); f.color = c; }
                return;
            }
            for (int i = 0; i < flames.Length; i++)
            {
                float ph = Time.time * (1000f / (180f + (i - 2) * 30f)) * Mathf.PI;
                float h = 110 + Mathf.Abs(i - 2) * 20;
                float sy = Mathf.Lerp(1f, 0.75f, (Mathf.Sin(ph) + 1) / 2);
                flames[i].transform.localScale = new Vector3(60 / 64f, h / 64f * sy, 1);
                flames[i].transform.localPosition = new Vector3((i - 2) * 20 * Space.Scale, h * sy / 2 * Space.Scale, 0);
            }
        }
    }
}
