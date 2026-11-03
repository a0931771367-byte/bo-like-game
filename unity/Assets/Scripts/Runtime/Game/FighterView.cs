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
        [System.NonSerialized] public Fighter fighter;
        Transform view;
        SpriteRenderer body, shadow, hpBg, hpBar, aura;
        /// <summary>Second body renderer for frame cross-fades: shows the upcoming walk/idle frame fading in over the current one.</summary>
        SpriteRenderer bodyNext;
        MaterialPropertyBlock mpbNext;
        /// <summary>Art's hit poses for enemies (<id>_hurt/_knock/_down/_flat); null when the monster has none yet.</summary>
        Sprite sideSprite, hurtSprite, knockSprite, downSprite, flatSprite, pileSprite;
        /// <summary>ruinGuardian extras (art dept 11/1): _slam 砸地, _beam 雷射起手, _petrify 石化, _phase2 狂怒碎甲.</summary>
        Sprite slamSprite, beamSprite, petrifySprite, phase2Sprite;
        /// <summary>Gold silhouette copies behind an elite's body (design doc 08 §2.6: 金色輪廓).</summary>
        SpriteRenderer[] outlines;
        /// <summary>Placeholder art (SpriteBank.PlaceholderTint): borrowed poses, tinted so testers can tell the desert enemies apart.</summary>
        bool placeholder; int placeholderTint;
        MaterialPropertyBlock mpb;
        float bodyScale = 1f;
        /// <summary>Extra lift (px) applied this frame so a rotation turns around the body centre (roll).</summary>
        float pivotUp;
        AnimSet anim;
        string classId;
        Color baseTint = Color.white;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int ShadeId = Shader.PropertyToID("_Shade");

        // ---- motion smoothing (罐子 11/4: 動畫要像參考影片一樣順、有重量) ----
        // Every procedural pose value runs through a damped spring, so a state change flows over a few frames with
        // follow-through, and landings / hits kick the springs for a squash that rebounds on its own.
        Spring leanS = new(0), sxS = new(1), syS = new(1);
        /// <summary>Walk cycle position in frames (fractional); advances with the fighter's actual speed so the feet don't slide.</summary>
        float walkPhase;
        /// <summary>Idle cycle position in frames (fractional) for the idle cross-fade.</summary>
        float idlePhase;
        int lastWalkFrame = -1;
        FighterState prevState;
        float prevZ;
        bool attackActiveKicked;
        /// <summary>Set by the state code when the springs must track the target exactly this frame (the roll's 360° turn).</summary>
        bool snapPose;
        /// <summary>Cross-fade request for this frame: the sprite to fade in and how far along (0 = none).</summary>
        Sprite nextSprite; float nextBlend;
        const float ShadeAmount = 0.10f;
        const float WalkFrameMs = 75f, IdleFrameMs = 170f;

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
                sideSprite = SpriteBank.Get($"{e.def.SpriteId}_side");
                body = MakeSprite("Body", view, sideSprite);
                string poseId = e.def.SpriteId;
                if (SpriteBank.PlaceholderTint(e.def.SpriteId, out placeholderTint)) { placeholder = true; poseId = null; }
                hurtSprite = poseId != null ? PoseOrNull(poseId, "hurt") : null;
                knockSprite = poseId != null ? PoseOrNull(poseId, "knock") : null;
                downSprite = poseId != null ? PoseOrNull(poseId, "down") : null;
                flatSprite = poseId != null ? PoseOrNull(poseId, "flat") : null;
                pileSprite = poseId != null ? PoseOrNull(poseId, "pile") : null; // 沙蟲 sand pile / 木乃伊 bandage pile (art dept keys)
                if (e.def.behavior == EnemyBehavior.BossGuardian && poseId != null)
                {
                    slamSprite = PoseOrNull(poseId, "slam");
                    beamSprite = PoseOrNull(poseId, "beam");
                    petrifySprite = PoseOrNull(poseId, "petrify");
                    phase2Sprite = PoseOrNull(poseId, "phase2");
                }
                if (e.def.tint.HasValue) baseTint = Colors.Rgb(e.def.tint.Value);
                else if (placeholder) baseTint = Colors.Rgb(placeholderTint);
                float hPx = body.sprite != null ? body.sprite.rect.height : e.def.spriteH;
                bodyScale = e.def.spriteH / hPx * e.sizeMult;
                if (e.elite)
                {
                    outlines = new SpriteRenderer[4];
                    var offs = new[] { new Vector2(3, 0), new Vector2(-3, 0), new Vector2(0, 3), new Vector2(0, -3) };
                    for (int i = 0; i < 4; i++)
                    {
                        var o = MakeSprite("Outline" + i, view, sideSprite);
                        o.material = SpriteBank.FlashMaterial;
                        o.transform.localPosition = new Vector3(offs[i].x * Space.Scale, offs[i].y * Space.Scale, 0);
                        var pb = new MaterialPropertyBlock();
                        pb.SetFloat(FlashId, 1f);
                        pb.SetColor(FlashColorId, Colors.Rgb(0xffd36b));
                        o.SetPropertyBlock(pb);
                        outlines[i] = o;
                    }
                }
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
            bodyNext = MakeSprite("BodyNext", view, null);
            bodyNext.material = SpriteBank.FlashMaterial;
            bodyNext.enabled = false;
            mpbNext = new MaterialPropertyBlock();
            prevState = f.state;
            prevZ = f.z;
            SetBodyScale(1, 1);
            Sync();
        }

        static Sprite PoseOrNull(string spriteId, string pose)
        {
            // Quiet lookup: monsters without the pose art yet fall back to the old rotate/squash, without a warning per pose.
            return Resources.Load<Sprite>($"Sprites/{spriteId}_{pose}");
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
            body.sortingOrder = order;
            if (aura != null) aura.sortingOrder = order - 1;
            if (hpBg != null) { hpBg.sortingOrder = order + 1; hpBar.sortingOrder = order + 2; }

            // The sim's scaled dt: the springs hold still through hit-stop like everything else.
            float dt = Mathf.Clamp(f.world.LastDt, 0f, 0.05f);
            bool stateChanged = f.state != prevState;
            bool landed = prevZ > 0f && f.z <= 0f;

            float lean = 0, sx = 1, sy = 1, bob = 0, alpha = 1;
            pivotUp = 0;
            snapPose = false;
            nextSprite = null; nextBlend = 0;
            if (f is Player p) SyncPlayer(p, ref lean, ref sx, ref sy, ref bob, ref alpha, stateChanged);
            else if (f is Enemy e) SyncEnemy(e, ref lean, ref sx, ref sy, ref alpha, stateChanged);

            // Landing: squash that springs back by itself (any state; Knock lands into Down which adds its own pose).
            if (landed && f.state != FighterState.Dodge)
            {
                float k = Mathf.Clamp(-prevVz / 900f, 0.6f, 1.6f);
                syS.velocity -= 3.2f * k;
                sxS.velocity += 2.2f * k;
            }

            // Springs. Lean is unwrapped first so the end of a 360° roll settles to 0 without unwinding.
            if (snapPose) { leanS.Snap(lean); sxS.Snap(sx); syS.Snap(sy); }
            else
            {
                leanS.value = lean - AnimCurves.DeltaAngle(leanS.value, lean);
                leanS.Step(lean, dt, 13f, 0.72f);
                sxS.Step(sx, dt, 15f, 0.7f);
                syS.Step(sy, dt, 15f, 0.7f);
            }
            float L = leanS.value, SX = Mathf.Clamp(sxS.value, 0.5f, 1.8f), SY = Mathf.Clamp(syS.value, 0.4f, 1.8f);

            // Hit-stop: the striker and the struck shiver 1.5 px and swell 4% while the sim is slowed.
            float jitter = 0;
            bool struck = f.Now < f.flashUntil;
            bool striking = f is Player pp && pp.state == FighterState.Attack;
            if (f.world.InHitStop && (struck || striking))
            {
                jitter = (Time.frameCount & 1) == 0 ? 1.5f : -1.5f;
                SX *= 1.04f; SY *= 0.97f;
            }

            // `bob` lifts the body (positive = up; the Phaser port had the sign inverted, so the walk bob and the roll's
            // arc used to push the sprite into the ground).
            // Sprites pivot at the feet, so `lean` tilts around the ground contact. A roll must turn about the body's
            // centre instead: the body is dropped by half its (squashed) height inside the view and the view raised by
            // the same amount, so the rotation revolves the sprite around its middle while the feet still land on y.
            view.localPosition = new Vector3(0, (f.z + bob + pivotUp) * Space.Scale, 0);
            body.transform.localPosition = new Vector3(jitter * Space.Scale, -pivotUp * Space.Scale, 0);
            view.localScale = new Vector3(f.facing, 1, 1);
            // Phaser angles are clockwise; Unity's z rotation is counter-clockwise.
            view.localRotation = Quaternion.Euler(0, 0, -L * f.facing);
            SetBodyScale(SX, SY);

            // Ground shadow: shrinks and fades with height (jump, roll arc and the walk bob alike); darkest on contact.
            float lift = Mathf.Max(0f, f.z + bob);
            float shadowScale = Mathf.Max(0.45f, 1 - lift / 400f) * shadowMul;
            shadow.transform.localScale = new Vector3(f.w * 1.1f / 64f * shadowScale * Mathf.Lerp(1f, SX, 0.5f), Mathf.Max(12f, f.w * 0.28f) / 64f * shadowScale, 1);
            shadow.sortingOrder = order - 1;
            if (!(f is Enemy en && (en.state == FighterState.Dying || en.Pile)))
                shadow.color = new Color(0, 0, 0, 0.3f * Mathf.Clamp(1 - lift / 300f, 0.45f, 1f) * alpha);

            var c = baseTint;
            c.a = alpha;
            body.color = c;
            bool flashing = struck;
            body.GetPropertyBlock(mpb);
            mpb.SetFloat(FlashId, flashing ? 1f : 0f);
            mpb.SetColor(FlashColorId, Colors.Rgb(f.flashColor));
            mpb.SetFloat(ShadeId, ShadeAmount);
            body.SetPropertyBlock(mpb);

            // Frame cross-fade: the next frame sits on top of the current one and fades in over the frame's duration.
            if (nextSprite != null && nextBlend > 0.001f && nextSprite != body.sprite)
            {
                bodyNext.enabled = true;
                bodyNext.sprite = nextSprite;
                bodyNext.sortingOrder = order + 1;
                bodyNext.transform.localPosition = body.transform.localPosition;
                bodyNext.transform.localScale = body.transform.localScale;
                var cn = c; cn.a = alpha * nextBlend;
                bodyNext.color = cn;
                bodyNext.GetPropertyBlock(mpbNext);
                mpbNext.SetFloat(FlashId, flashing ? 1f : 0f);
                mpbNext.SetColor(FlashColorId, Colors.Rgb(f.flashColor));
                mpbNext.SetFloat(ShadeId, ShadeAmount);
                bodyNext.SetPropertyBlock(mpbNext);
            }
            else if (bodyNext.enabled) bodyNext.enabled = false;

            if (outlines != null)
                foreach (var o in outlines)
                {
                    o.sprite = body.sprite;
                    o.transform.localScale = body.transform.localScale;
                    o.sortingOrder = order - 1;
                    o.color = new Color(1, 1, 1, alpha);
                }

            prevState = f.state;
            prevZ = f.z;
            prevVz = f.vz;
        }
        float prevVz;
        /// <summary>Extra shadow shrink set by the enemy death pose (1 = none).</summary>
        float shadowMul = 1f;

        // ---------- player ----------

        /// <summary>
        /// Picks the frame for this state. Walk and idle are cyclic and come with a cross-fade into the following
        /// frame (the art is 8 frames at 75 ms ≈ 13 fps; the fade fills the gaps at 60 fps). Attack frames are
        /// key poses held by the hit timing and stay crisp.
        /// </summary>
        Sprite FrameFor(Player p, float dt, bool stateChanged)
        {
            var a = anim;
            string id = classId;
            float t = p.stateTime;
            string key;
            switch (p.state)
            {
                case FighterState.Walk:
                {
                    if (stateChanged) { walkPhase = 0; lastWalkFrame = -1; }
                    // Cycle speed follows the real speed (slow stick = slow steps); the lane axis counts 1/0.6 like the sim.
                    float move = Mathf.Max(1f, (float)p.d.move);
                    float speed = Mathf.Sqrt(p.vx * p.vx + (p.vy / 0.6f) * (p.vy / 0.6f));
                    walkSpeedK = Mathf.Clamp(speed / move, 0.5f, 1.4f);
                    walkPhase += dt * 1000f / WalkFrameMs * walkSpeedK;
                    int fi = (int)walkPhase % a.walk;
                    float frac = walkPhase - Mathf.Floor(walkPhase);
                    key = SpriteBank.AnimKey(id, "walk", fi);
                    nextSprite = SpriteBank.Get(SpriteBank.AnimKey(id, "walk", (fi + 1) % a.walk));
                    nextBlend = AnimCurves.CrossFade(frac);
                    // Footfalls on frame 0 and the opposite frame (two steps per cycle).
                    if (fi != lastWalkFrame)
                    {
                        if (lastWalkFrame >= 0 && (fi == 0 || fi == a.walk / 2) && p.z <= 0f) p.Fx.Step(p.x, p.y, p.facing);
                        lastWalkFrame = fi;
                    }
                    break;
                }
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
                default:
                {
                    idlePhase += dt * 1000f / IdleFrameMs;
                    int fi = (int)idlePhase % a.idle;
                    key = SpriteBank.AnimKey(id, "idle", fi);
                    nextSprite = SpriteBank.Get(SpriteBank.AnimKey(id, "idle", (fi + 1) % a.idle));
                    nextBlend = AnimCurves.CrossFade(idlePhase - Mathf.Floor(idlePhase));
                    break;
                }
            }
            return SpriteBank.Get(key);
        }
        float walkSpeedK = 1f;

        void SyncPlayer(Player p, ref float lean, ref float sx, ref float sy, ref float bob, ref float alpha, bool stateChanged)
        {
            float t = p.stateTime;
            double now = p.Now;
            float dt = Mathf.Clamp(p.world.LastDt, 0f, 0.05f);
            if (anim != null)
            {
                var s = FrameFor(p, dt, stateChanged);
                if (s != null && body.sprite != s) body.sprite = s;
            }
            else if (p.state == FighterState.Walk) { walkPhase += dt * 1000f / WalkFrameMs; walkSpeedK = 1f; }
            float k = anim != null ? 1f : 1.5f; // without frames the procedural motion has to carry everything
            float breath = Mathf.Sin((float)(now / 420));
            if (stateChanged)
            {
                attackActiveKicked = false;
                // Entry impulses: the pose snaps toward the new state and the springs add the follow-through.
                switch (p.state)
                {
                    case FighterState.Hurt: leanS.velocity -= 260f; sxS.velocity -= 1.6f; break;
                    case FighterState.Knock: leanS.velocity -= 400f; break;
                    case FighterState.Idle: if (prevState == FighterState.Walk) { syS.velocity -= 1.0f; leanS.velocity -= 60f; } break; // stops short
                    case FighterState.Jump: syS.velocity += 2.4f; sxS.velocity -= 1.2f; break; // take-off stretch
                    case FighterState.Attack: break;
                }
            }
            switch (p.state)
            {
                case FighterState.Idle:
                    sy = 1 + breath * 0.022f * k; sx = 1 - breath * 0.011f * k;
                    lean = Mathf.Sin((float)(now / 840)) * 1.2f * k;
                    break;
                case FighterState.Walk:
                {
                    // Gait tied to the frames: two steps per cycle, lowest on contact, 6 px (≈5% of the body) at the top,
                    // a touch of squash into the contact and stretch over the top, a forward lean that grows with speed.
                    int framesPerStep = Mathf.Max(1, (anim != null ? anim.walk : 8) / 2);
                    float stepPhase = walkPhase / framesPerStep;
                    float lift = AnimCurves.GaitLift(stepPhase);
                    bob = lift * 6f * k;
                    sy = 0.975f + 0.045f * lift * k;
                    sx = 1.015f - 0.03f * lift * k;
                    lean = (5f + 3.5f * walkSpeedK + Mathf.Sin(stepPhase * Mathf.PI * 2f) * 1.2f) * (anim != null ? 1f : 1.2f);
                    break;
                }
                case FighterState.Guard: sx = 0.92f; sy = 1 + breath * 0.015f; lean = -6; break;
                case FighterState.Attack:
                {
                    var h = p.Timing(p.cls.combo[p.combo]);
                    if (t < h.startup)
                    {
                        // Anticipation: gathers back quickly, then holds the wind-up.
                        float u = AnimCurves.EaseOutQuad(t / Mathf.Max(1f, h.startup));
                        sx = 1 - 0.08f * u; sy = 1 + 0.04f * u; lean = -7f * u;
                    }
                    else if (t < h.startup + h.active)
                    {
                        // Hit: a smear kicked into the springs on the first active frame, then a long, low lunge.
                        if (!attackActiveKicked) { attackActiveKicked = true; sxS.velocity += 3.2f; syS.velocity -= 1.2f; leanS.velocity += 320f; }
                        float u = (t - h.startup) / Mathf.Max(1f, h.active);
                        sx = 1.12f - 0.06f * u; sy = 0.96f; lean = 9f;
                    }
                    else { sx = 1.02f; lean = 5f; }
                    break;
                }
                case FighterState.AirAttack: lean = 18; sx = 1.1f; break;
                case FighterState.Jump:
                {
                    // Stretch while rising, neutral at the apex, a slight lean-in while falling.
                    float up = Mathf.Clamp01(p.vz / 700f), down = Mathf.Clamp01(-p.vz / 900f);
                    sy = 1 + 0.08f * up + 0.02f * down; sx = 1 - 0.05f * up - 0.01f * down; lean = 4 + 3 * down;
                    break;
                }
                case FighterState.Cast: sy = 1.05f + Mathf.Sin(t / 50f) * 0.02f; lean = -4; break;
                case FighterState.CastEnd: sx = 1.1f; lean = 8; break;
                case FighterState.Dodge:
                {
                    float u = Mathf.Clamp01(t / p.dodgeMs);
                    switch (p.Dodge.kind)
                    {
                        case DodgeKind.Blink: sx = 1 - 0.25f * u; sy = 1 + 0.15f * u; break;                     // mage: gathers, then vanishes (alpha below)
                        case DodgeKind.Roll: Roll(u, ref lean, ref sx, ref sy, ref bob); snapPose = true; break;    // archer: forward roll (below); the turn is exact, no spring lag
                        case DodgeKind.JetDash: lean = 24; sx = 1.15f; sy = 0.92f; break;                         // merchant: jet slide
                        case DodgeKind.ShadowStep: lean = 32; sx = 1.15f; sy = 0.88f; break;                      // thief: low dash
                        case DodgeKind.GraceStep: lean = 8; bob = 10 + Mathf.Sin(u * Mathf.PI) * 8; sy = 1.04f; break; // acolyte: glides
                        default: lean = -8; sx = 1.12f; sy = 0.96f; break;                                        // swordsman: shield forward
                    }
                    break;
                }
                case FighterState.Hurt: lean = -12; sx = 0.94f; sy = 1.03f; break;
                case FighterState.Knock: lean = -45; break;
                case FighterState.Down:
                case FighterState.Dead: lean = -90; break;
            }
            ActiveBuff glow = null;
            foreach (var b in p.buffs)
                if (b.eff.endure || (b.hits ?? 0) > 0 || b.eff.atkMult.HasValue || b.eff.allStats.HasValue || b.eff.aspdMult.HasValue) { glow = b; break; }
            aura.color = glow != null ? Colors.Rgb(glow.color, 0.6f + Mathf.Sin((float)(now / 90)) * 0.3f) : new Color(0, 0, 0, 0);
            if (now < p.glowUntil)
            {
                // Level-up: golden aura that swells and fades over the glow time.
                float g = Mathf.Clamp01((float)(p.glowUntil - now) / 1500f);
                aura.color = Colors.Rgb(Colors.Crit, 0.35f + 0.5f * g + Mathf.Sin((float)(now / 70)) * 0.15f);
                aura.transform.localScale = new Vector3(110f / 128f * (1.1f + 0.3f * (1 - g)), 150f / 128f * (1.1f + 0.3f * (1 - g)), 1);
            }
            else aura.transform.localScale = new Vector3(110f / 128f, 150f / 128f, 1);
            bool blink = now < p.invulnUntil && p.state != FighterState.Dodge;
            alpha = p.Hidden ? 0.3f : blink ? ((int)(now / 60) % 2 == 1 ? 0.35f : 1f)
                : p.state == FighterState.Dodge ? (p.Dodge.kind == DodgeKind.Blink ? Mathf.Clamp01(1 - t / p.dodgeMs)
                    : p.Dodge.kind == DodgeKind.ShadowStep ? 0.5f : p.Dodge.kind == DodgeKind.ShieldDash || p.Dodge.kind == DodgeKind.Roll ? 1f : 0.6f) : 1f;
        }

        /// <summary>
        /// Archer's roll (罐子 11/3 #7). Three beats over the dodge: a crouch (the body gathers, no turn yet), the tumble
        /// (one full turn about the body centre with ease-in/out so it carries momentum, tucked into a ball, lifting
        /// along a small arc) and the landing (a squash that springs back to standing). The old version spun the upright
        /// sprite 360° around its feet, which read as a cartwheel of the whole character.
        /// </summary>
        void Roll(float u, ref float lean, ref float sx, ref float sy, ref float bob)
        {
            const float crouchEnd = 0.14f, tumbleEnd = 0.82f;
            float h = body.sprite != null ? body.sprite.rect.height * bodyScale : Player.PlayerH;
            if (u < crouchEnd)
            {
                float c = u / crouchEnd;
                sx = 1 + 0.10f * c; sy = 1 - 0.20f * c;
                lean = 12f * c;
            }
            else if (u < tumbleEnd)
            {
                float r = (u - crouchEnd) / (tumbleEnd - crouchEnd);
                float e = 0.5f - 0.5f * Mathf.Cos(r * Mathf.PI); // ease-in-out: slow off the ground, fast over the top, slow into the landing
                lean = 12f + 348f * e;
                float tuck = Mathf.Sin(r * Mathf.PI);          // tightest mid-roll
                sx = 1.06f - 0.10f * tuck; sy = 0.80f - 0.14f * tuck;
                bob = Mathf.Sin(r * Mathf.PI) * 26f;
                pivotUp = h * sy * 0.5f;
            }
            else
            {
                float l = (u - tumbleEnd) / (1 - tumbleEnd);
                float spring = Mathf.Sin(l * Mathf.PI);
                sx = 1 + 0.14f * spring; sy = 1 - 0.16f * spring;
                lean = 360f + 6f * (1 - l);
            }
        }

        // ---------- enemy ----------

        void SyncEnemy(Enemy e, ref float lean, ref float sx, ref float sy, ref float alpha, bool stateChanged)
        {
            if (stateChanged)
            {
                switch (e.state)
                {
                    case FighterState.Hurt: leanS.velocity -= 220f; sxS.velocity -= 1.4f; break;
                    case FighterState.Knock: leanS.velocity -= 380f; break;
                    case FighterState.Attack:
                    case FighterState.Lunge: sxS.velocity += 2.4f; leanS.velocity += 260f; break;
                }
            }
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
            baseTint = e.Frozen ? Colors.Rgb(Colors.Ice) : e.Weakened ? Colors.Rgb(0xffb0b0) : e.Poisoned ? Colors.Rgb(0xc9a0ff)
                : e.PetrifiedBoss ? Colors.Rgb(0xc9b28a) : e.Enraged ? Colors.Rgb(0xff9090)
                : e.def.tint.HasValue ? Colors.Rgb(e.def.tint.Value) : placeholder ? Colors.Rgb(placeholderTint) : Color.white;
            if (sideSprite != null) body.sprite = sideSprite;
            if (e.Enraged && phase2Sprite != null) body.sprite = phase2Sprite; // the hit poses below still override while they last
            if (e.PetrifiedBoss && petrifySprite != null) { body.sprite = petrifySprite; return; }
            if (e.state == FighterState.Stomp && slamSprite != null) { body.sprite = slamSprite; sy = t < 520 ? 1 + t / 520f * 0.06f : 0.96f; return; }
            if (e.state == FighterState.Beam && beamSprite != null) { body.sprite = beamSprite; lean = t < 700 ? -3 : 4; return; }
            if (e.Burrowed)
            {
                // Sand pile: the pile pose when the art has one, else the body squashed flat; it shakes while about to emerge.
                if (pileSprite != null) body.sprite = pileSprite; else { sy = 0.3f; sx = 1.25f; }
                if (e.EmergeAt > 0) { float sh = Mathf.Sin((float)(now / 25)); lean = sh * 6; sx *= 1 + sh * 0.05f; }
                return;
            }
            if (e.Pile)
            {
                if (pileSprite != null) body.sprite = pileSprite;
                else if (flatSprite != null) body.sprite = flatSprite;
                else sy = 0.35f;
                shadow.color = new Color(0, 0, 0, 0.28f);
                return;
            }
            // Art-drawn hit poses (art dept 10/30): no rotation tricks when the pose exists.
            if (e.state == FighterState.Hurt && hurtSprite != null) { body.sprite = hurtSprite; sx = 0.96f; return; }
            if (e.state == FighterState.Knock && knockSprite != null)
            {
                body.sprite = knockSprite;
                lean = Mathf.Clamp(-e.vz / 40f, -15f, 15f); // leans back while rising, forward while falling
                return;
            }
            if (e.state == FighterState.Down && downSprite != null)
            {
                body.sprite = downSprite;
                sy = Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(t / 120f)); // landing rebound
                return;
            }
            if (e.state == FighterState.Dying && flatSprite != null)
            {
                // down 120 ms → flat 120 ms → fade 200 ms; the shadow fades with the body.
                float ms = (float)(now - e.dyingSince);
                body.sprite = ms < 120 && downSprite != null ? downSprite : flatSprite;
                float k = Mathf.Clamp01((ms - 240f) / 200f);
                alpha = 1 - k;
                shadow.color = new Color(0, 0, 0, 0.28f * (1 - k));
                shadowMul = 1 - 0.5f * k;
                return;
            }
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
                case FighterState.Beam: sx = 1.08f; lean = t < 700 ? -4 : 6; break;
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
        }
    }
}
