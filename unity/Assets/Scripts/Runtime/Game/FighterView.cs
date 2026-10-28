using BoLike.Core;
using BoLike.Data;
using BoLike.Sim;
using UnityEngine;

namespace BoLike.Game
{
    /// <summary>
    /// Draws one fighter: root at the lane position, a "view" child that carries height, lean and squash,
    /// a ground shadow and (for enemies) a small HP bar. Port of the syncView() methods in Player.ts and Enemy.ts.
    /// </summary>
    public class FighterView : MonoBehaviour
    {
        public Fighter fighter;
        Transform view;
        SpriteRenderer body, shadow, hpBg, hpBar, aura;
        MaterialPropertyBlock mpb;
        float bodyScale = 1f;
        AnimSet anim;
        string classId;
        Color baseTint = Color.white;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        public SpriteRenderer Body => body;
        public float BodyScale => bodyScale;
        public Transform View => view;

        public static FighterView Create(Fighter f, Transform parent)
        {
            var go = new GameObject(f is Player ? "Player" : "Enemy");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<FighterView>();
            v.Bind(f);
            return v;
        }

        void Bind(Fighter f)
        {
            fighter = f;
            mpb = new MaterialPropertyBlock();
            shadow = MakeSprite("Shadow", transform, SpriteBank.SoftCircle);
            shadow.color = new Color(0, 0, 0, 0.28f);
            shadow.transform.localScale = new Vector3(f.w * 1.1f / 64f, Mathf.Max(12f, f.w * 0.28f) / 64f, 1);
            view = new GameObject("View").transform;
            view.SetParent(transform, false);

            if (f is Player p)
            {
                classId = p.cls.id;
                anim = GameDb.Anim(classId);
                if (anim != null && !SpriteBank.Has(SpriteBank.AnimKey(classId, "idle", 0))) anim = null;
                aura = MakeSprite("Aura", view, SpriteBank.Ring);
                aura.transform.localPosition = new Vector3(0, 0.6f, 0);
                aura.transform.localScale = new Vector3(110f / 128f, 150f / 128f, 1);
                aura.color = new Color(0, 0, 0, 0);
                body = MakeSprite("Body", view, anim != null ? SpriteBank.Get(SpriteBank.AnimKey(classId, "idle", 0)) : SpriteBank.Get($"{classId}_side"));
                // Animation frames share one canvas with the feet on the bottom edge, so one scale fits every frame.
                float hPx = body.sprite != null ? body.sprite.rect.height : Player.PlayerH;
                bodyScale = (anim != null ? Player.PlayerH * 1.08f : Player.PlayerH) / hPx;
            }
            else if (f is Enemy e)
            {
                body = MakeSprite("Body", view, SpriteBank.Get($"{e.def.SpriteId}_side"));
                if (e.def.tint.HasValue) baseTint = Colors.Rgb(e.def.tint.Value);
                float hPx = body.sprite != null ? body.sprite.rect.height : e.def.spriteH;
                bodyScale = e.def.spriteH / hPx;
                if (!e.def.boss)
                {
                    hpBg = MakeSprite("HpBg", view, SpriteBank.Pixel);
                    hpBg.color = new Color(0, 0, 0, 0.5f);
                    hpBg.transform.localPosition = new Vector3(0, (e.def.spriteH + 14) * Space.Scale, 0);
                    hpBg.transform.localScale = new Vector3(e.def.w / 4f, 6f / 4f, 1);
                    hpBar = MakeSprite("HpBar", view, SpriteBank.Pixel);
                    hpBar.color = Colors.Rgb(0xff4d4d);
                }
            }
            body.material = SpriteBank.FlashMaterial;
            body.color = baseTint;
            SetBodyScale(1, 1);
            Sync();
        }

        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            return sr;
        }

        void SetBodyScale(float sx, float sy)
        {
            // Sprites have a bottom-centre pivot (set by the importer), so scaling keeps the feet planted.
            body.transform.localScale = new Vector3(bodyScale * sx, bodyScale * sy, 1);
        }

        public void Sync()
        {
            var f = fighter;
            transform.position = Space.ToWorld(f.x, f.y);
            int order = Space.Order(f.y);
            float shadowScale = Mathf.Max(0.45f, 1 - f.z / 400f);
            shadow.transform.localScale = new Vector3(f.w * 1.1f / 64f * shadowScale, Mathf.Max(12f, f.w * 0.28f) / 64f * shadowScale, 1);
            shadow.sortingOrder = order - 1;
            body.sortingOrder = order;
            if (aura != null) aura.sortingOrder = order - 1;
            if (hpBg != null) { hpBg.sortingOrder = order + 1; hpBar.sortingOrder = order + 2; }

            float lean = 0, sx = 1, sy = 1, bob = 0, alpha = 1;
            if (f is Player p) SyncPlayer(p, ref lean, ref sx, ref sy, ref bob, ref alpha);
            else if (f is Enemy e) SyncEnemy(e, ref lean, ref sx, ref sy, ref alpha);

            view.localPosition = new Vector3(0, (f.z - bob) * Space.Scale, 0);
            view.localScale = new Vector3(f.facing, 1, 1);
            // Phaser angles are clockwise; Unity's z rotation is counter-clockwise.
            view.localRotation = Quaternion.Euler(0, 0, -lean * f.facing);
            SetBodyScale(sx, sy);

            var c = baseTint;
            c.a = alpha;
            body.color = c;
            bool flashing = f.Now < f.flashUntil;
            body.GetPropertyBlock(mpb);
            mpb.SetFloat(FlashId, flashing ? 1f : 0f);
            mpb.SetColor(FlashColorId, Colors.Rgb(f.flashColor));
            body.SetPropertyBlock(mpb);
        }

        // ---------- player ----------

        Sprite FrameFor(Player p)
        {
            var a = anim;
            string id = classId;
            float t = p.stateTime;
            string key;
            switch (p.state)
            {
                case FighterState.Walk: key = SpriteBank.AnimKey(id, "walk", (int)(t / 80) % a.walk); break;
                case FighterState.Attack:
                {
                    var h = p.Timing(p.cls.combo[p.combo]);
                    int i = t < h.startup ? 0 : t < h.startup + h.active ? 1 : t < h.startup + h.active + h.recovery * 0.45f ? 2 : 3;
                    key = SpriteBank.AnimKey(id, "attack", Mathf.Min(i, a.attack - 1));
                    break;
                }
                case FighterState.AirAttack: key = SpriteBank.AnimKey(id, "attack", Mathf.Min(t < p.cls.air.startup ? 0 : 1, a.attack - 1)); break;
                case FighterState.Jump: key = SpriteBank.AnimKey(id, "walk", Mathf.Min(1, a.walk - 1)); break;
                case FighterState.Dodge: key = SpriteBank.AnimKey(id, "walk", Mathf.Min(2, a.walk - 1)); break;
                case FighterState.Cast:
                case FighterState.CastEnd: key = SpriteBank.AnimKey(id, "attack", Mathf.Min(t < 200 ? 0 : 3, a.attack - 1)); break;
                default: key = SpriteBank.AnimKey(id, "idle", (int)(p.Now / 170) % a.idle); break;
            }
            return SpriteBank.Get(key);
        }

        void SyncPlayer(Player p, ref float lean, ref float sx, ref float sy, ref float bob, ref float alpha)
        {
            float t = p.stateTime;
            double now = p.Now;
            if (anim != null)
            {
                var s = FrameFor(p);
                if (s != null && body.sprite != s) body.sprite = s;
            }
            float k = anim != null ? 0.35f : 1f; // frames carry most of the motion; keep a little squash on top
            float breath = Mathf.Sin((float)(now / 420));
            switch (p.state)
            {
                case FighterState.Idle: sy = 1 + breath * 0.028f * k; sx = 1 - breath * 0.014f * k; lean = Mathf.Sin((float)(now / 840)) * 1.5f * k; break;
                case FighterState.Walk:
                {
                    float ph = t / 75f;
                    bob = Mathf.Abs(Mathf.Sin(ph)) * 6 * k;
                    sy = 1 + Mathf.Sin(ph) * 0.04f * k;
                    sx = 1 - Mathf.Sin(ph) * 0.03f * k;
                    lean = (7 + Mathf.Sin(ph * 2) * 2.5f) * k;
                    break;
                }
                case FighterState.Guard: sx = 0.92f; sy = 1 + breath * 0.015f; lean = -6; break;
                case FighterState.Attack:
                {
                    var h = p.Timing(p.cls.combo[p.combo]);
                    if (t < h.startup) { sx = 1 - 0.1f * k * (1 - t / h.startup); lean = -8 * k; }
                    else if (t < h.startup + h.active) { sx = 1 + 0.15f * k; sy = 1 - 0.05f * k; lean = 10 * k; }
                    else { sx = 1 + 0.05f * k; lean = 6 * k; }
                    break;
                }
                case FighterState.AirAttack: lean = 18; sx = 1.1f; break;
                case FighterState.Jump: sy = 1.08f; sx = 0.95f; lean = 5; break;
                case FighterState.Cast: sy = 1.05f + Mathf.Sin(t / 50f) * 0.02f; lean = -4; break;
                case FighterState.CastEnd: sx = 1.1f; lean = 8; break;
                case FighterState.Dodge: lean = 22; sx = 1.1f; sy = 0.9f; break;
                case FighterState.Hurt: lean = -14; sx = 0.92f; break;
                case FighterState.Knock: lean = -45; break;
                case FighterState.Down:
                case FighterState.Dead: lean = -90; break;
            }
            ActiveBuff glow = null;
            foreach (var b in p.buffs)
                if (b.eff.endure || (b.hits ?? 0) > 0 || b.eff.atkMult.HasValue || b.eff.allStats.HasValue || b.eff.aspdMult.HasValue) { glow = b; break; }
            aura.color = glow != null ? Colors.Rgb(glow.color, 0.6f + Mathf.Sin((float)(now / 90)) * 0.3f) : new Color(0, 0, 0, 0);
            bool blink = now < p.invulnUntil && p.state != FighterState.Dodge;
            alpha = p.Hidden ? 0.3f : blink ? ((int)(now / 60) % 2 == 1 ? 0.35f : 1f) : p.state == FighterState.Dodge ? 0.6f : 1f;
        }

        // ---------- enemy ----------

        void SyncEnemy(Enemy e, ref float lean, ref float sx, ref float sy, ref float alpha)
        {
            if (hpBar != null)
            {
                float frac = Mathf.Max(0, e.hp / e.maxHp);
                hpBar.transform.localScale = new Vector3(e.def.w * frac / 4f, 6f / 4f, 1);
                hpBar.transform.localPosition = new Vector3((-e.def.w / 2f + e.def.w * frac / 2f) * Space.Scale, (e.def.spriteH + 14) * Space.Scale, 0);
            }
            float t = e.stateTime;
            double now = e.Now;
            var b = e.def.behavior;
            float wob = Mathf.Sin((float)((now + e.phase) / (b == EnemyBehavior.Hopper ? 260 : e.def.boss ? 520 : 380)));
            switch (e.state)
            {
                case FighterState.Idle:
                    if (b == EnemyBehavior.Hopper || b == EnemyBehavior.BossSlime) { sy = 1 + wob * 0.07f; sx = 1 - wob * 0.05f; }
                    else if (b == EnemyBehavior.Lunger) { sy = 1 + wob * 0.03f; lean = wob * 2; }
                    else if (b == EnemyBehavior.Turret) { lean = wob * 4; sy = 1 + wob * 0.02f; }
                    else { sx = 1 + wob * 0.03f; sy = 1 - wob * 0.02f; lean = wob * 1.5f; }
                    break;
                case FighterState.Walk:
                    sy = 1 + Mathf.Sin(t / 90f) * 0.05f; sx = 1 - Mathf.Sin(t / 90f) * 0.04f;
                    lean = b == EnemyBehavior.Lunger ? 6 + Mathf.Sin(t / 90f) * 3 : Mathf.Sin(t / 90f) * 4;
                    break;
                case FighterState.Windup: sx = 1.15f; sy = 0.85f; lean = -8; break;
                case FighterState.Attack:
                case FighterState.Lunge: lean = 14; sx = 1.1f; break;
                case FighterState.Slam: sy = 1.25f; sx = 0.85f; break;
                case FighterState.Stomp: sy = t < 520 ? 1 + t / 520f * 0.18f : 0.85f; sx = t < 520 ? 1 - t / 520f * 0.08f : 1.15f; break;
                case FighterState.Hurt: lean = -10; sx = 0.92f; break;
                case FighterState.Knock: lean = -60; break;
                case FighterState.Down: lean = -90; sy = 0.6f; break;
                case FighterState.Frozen: lean = 0; break;
                case FighterState.Dying:
                {
                    // Squash into the ground and fade over 260 ms.
                    float k = Mathf.Clamp01((float)(now - e.dyingSince) / 260f);
                    sy = Mathf.Lerp(1f, 0.1f, k);
                    alpha = 1 - k;
                    shadow.color = new Color(0, 0, 0, 0.28f * (1 - k));
                    break;
                }
            }
            if (b == EnemyBehavior.Flyer) sy *= 1 + Mathf.Sin((float)(now / 45)) * 0.02f;
            else if (e.Airborne && e.state != FighterState.Knock && e.state != FighterState.Slam) { sy = 1.1f; sx = 0.92f; }
            baseTint = e.Frozen ? Colors.Rgb(Colors.Ice) : e.Weakened ? Colors.Rgb(0xffb0b0) : e.Poisoned ? Colors.Rgb(0xc9a0ff)
                : e.def.tint.HasValue ? Colors.Rgb(e.def.tint.Value) : Color.white;
        }
    }
}
