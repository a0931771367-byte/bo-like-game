using System;
using System.Collections.Generic;
using BoLike.Core;
using BoLike.Data;
using BoLike.Systems;
using UnityEngine;

namespace BoLike.Sim
{
    public class ActiveBuff
    {
        public string id, name;
        public int color;
        public double until;
        public BuffEffect eff;
        public int? hits;
    }

    /// <summary>
    /// Port of src/entities/Player.ts: the state machine, normal attacks, skills, potions and damage intake.
    /// Input comes from <see cref="GameInput"/>; everything else goes through <see cref="World"/>.
    /// </summary>
    public class Player : Fighter
    {
        double nextStepAt;
        const float JumpV = 680f;
        const float DodgeMsDefault = 260f;
        const float DodgeSpeedDefault = 640f;
        public const float PlayerH = 132f;
        const float ArrowSpeed = 1150f;
        static readonly GameAction[] SkillKeys = { GameAction.Skill0, GameAction.Skill1, GameAction.Skill2, GameAction.Skill3 };

        public override Team Team => Team.Player;
        public readonly ClassDef cls;
        public readonly ClassProgress prog;
        public Derived d;
        public float sp, maxSp;
        public int combo;
        public bool queued;
        public bool airAttackUsed;
        public readonly Dictionary<string, double> cooldowns = new();
        public List<ActiveBuff> buffs = new();
        public double potionReadyAt;
        public int dodgeDir = 1;
        public float dodgeMs = DodgeMsDefault, dodgeSpeed = DodgeSpeedDefault;
        public SkillDef casting;
        bool shotFired;
        double lastStepAt, lastGhostAt;
        /// <summary>Set when the view should draw a slash this frame (consumed by the view).</summary>
        public int slashRequested;

        public Player(World world, float x, float y, ClassDef cls, ClassProgress prog) : base(world, x, y, 46, 96)
        {
            this.cls = cls;
            this.prog = prog;
            d = StatFormulas.Derive(cls, prog);
            maxHp = hp = (float)d.maxHp;
            maxSp = sp = (float)d.maxSp;
        }

        RunMods Mods => world.Mods;
        Inventory Inv => world.Inv;
        ISfx Sfx => world.Sfx;

        /// <summary>Recalculate derived stats after leveling, allocating points, buffs or perks.</summary>
        public void Recompute(bool fullHeal = false)
        {
            float hpRatio = maxHp > 0 ? hp / maxHp : 1;
            float spRatio = maxSp > 0 ? sp / maxSp : 1;
            float all = 0;
            foreach (var b in buffs) all += b.eff.allStats ?? 0;
            var stats = prog.stats.Clone();
            if (all != 0)
            {
                int add = (int)all;
                stats.str += add; stats.agi += add; stats.vit += add; stats.@int += add; stats.dex += add; stats.luk += add;
            }
            d = StatFormulas.Derive(cls, prog.level, stats);
            foreach (var b in buffs)
            {
                d.atk = StatFormulas.JsRound(d.atk * (b.eff.atkMult ?? 1));
                d.matk = StatFormulas.JsRound(d.matk * (b.eff.matkMult ?? 1));
                d.move *= b.eff.moveMult ?? 1;
                d.aspd *= b.eff.aspdMult ?? 1;
            }
            var m = Mods;
            d.maxHp = StatFormulas.JsRound(d.maxHp * m.maxHp);
            d.atk = StatFormulas.JsRound(d.atk * m.atk);
            d.matk = StatFormulas.JsRound(d.matk * m.matk);
            d.move *= m.move;
            d.aspd *= m.aspd;
            d.spRegen *= m.spRegen;
            d.crit += m.critAdd;
            maxHp = (float)d.maxHp;
            maxSp = (float)d.maxSp;
            hp = fullHeal ? maxHp : Math.Max(1, Mathf.Round(maxHp * hpRatio));
            sp = fullHeal ? maxSp : Mathf.Round(maxSp * spRatio);
        }

        public int SkillLv(SkillDef s)
        {
            int baseLv = prog.skillLv.TryGetValue(s.id, out var l) ? l : 0;
            return baseLv > 0 ? Math.Min(s.maxLv, baseLv + Mods.skillLvAdd) : 0;
        }
        public int SkillCost(SkillDef s) => (int)StatFormulas.JsRound(s.sp.At(Math.Max(1, SkillLv(s))) * Mods.spCost);
        public float CooldownLeft(SkillDef s) => (float)Math.Max(0, (cooldowns.TryGetValue(s.id, out var t) ? t : 0) - Now);
        public float CooldownTotal(SkillDef s) => (float)(s.cooldown * d.castMult * Mods.cooldown);
        public int SkillGold(SkillDef s) => s.goldCost.HasValue ? (int)StatFormulas.JsRound(s.goldCost.Value.At(Math.Max(1, SkillLv(s)))) : 0;

        /// <summary>Value of a learned passive (0 when not learned).</summary>
        public float PassiveValue(Passive kind)
        {
            foreach (var s in cls.skills)
            {
                if (s.passive != kind || !s.passiveValue.HasValue) continue;
                int L = SkillLv(s);
                return L > 0 ? s.passiveValue.Value.At(L) : 0;
            }
            return 0;
        }

        public bool Hidden { get { foreach (var b in buffs) if (b.eff.hidden) return true; return false; } }
        public bool Endure { get { foreach (var b in buffs) if (b.eff.endure) return true; return false; } }

        public void AddBuff(SkillDef s, BuffEffect eff, float duration)
        {
            buffs.RemoveAll(b => b.id == s.id);
            buffs.Add(new ActiveBuff { id = s.id, name = s.name, color = s.color, until = Now + duration, eff = eff, hits = eff.shieldHits });
            Recompute();
        }

        /// <summary>Attacking or casting reveals a hidden player.</summary>
        void BreakHidden()
        {
            if (!Hidden) return;
            buffs.RemoveAll(b => b.eff.hidden);
            Recompute();
        }

        void ExpireBuffs()
        {
            int n = buffs.Count;
            buffs.RemoveAll(b => !(Now < b.until && (b.hits == null || b.hits > 0)));
            if (buffs.Count != n) Recompute();
        }

        IReadOnlyList<Enemy> Enemies => world.Enemies;

        // Phaser's y axis points down the screen; the stick's y points up.
        static float MoveX => GameInput.Move.x;
        static float MoveY => -GameInput.Move.y;

        public override void Update(float dt)
        {
            float ms = dt * 1000;
            stateTime += ms;
            if (!dead)
            {
                sp = Math.Min(maxSp, sp + (float)d.spRegen * dt);
                hp = Math.Min(maxHp, hp + (float)d.hpRegen * dt);
                ExpireBuffs();
                TryPotions();
            }
            var (xMin, xMax) = world.PlayerBounds();
            bool grounded = !Airborne;

            switch (state)
            {
                case FighterState.Idle:
                case FighterState.Walk:
                    FreeMove();
                    if (state == FighterState.Walk && grounded && world.Now >= nextStepAt)
                    {
                        world.Sfx.Play("step");
                        nextStepAt = world.Now + 280;
                    }
                    break;
                case FighterState.Guard:
                    vx = vy = 0;
                    if (!GameInput.Guard) SetState(FighterState.Idle);
                    else if (TryDodge()) break;
                    else if (TrySkill()) break;
                    break;
                case FighterState.Attack:
                    UpdateAttack();
                    break;
                case FighterState.Jump:
                    vx = MoveX * (float)d.move * 0.85f;
                    vy = MoveY * (float)d.move * 0.4f;
                    if (MoveX != 0) facing = MoveX > 0 ? 1 : -1;
                    if (!airAttackUsed && GameInput.Consume(GameAction.Attack))
                    {
                        airAttackUsed = true;
                        struck.Clear();
                        shotFired = false;
                        BreakHidden();
                        SetState(FighterState.AirAttack);
                    }
                    break;
                case FighterState.AirAttack:
                {
                    var a = cls.air;
                    vx = MoveX * (float)d.move * 0.6f;
                    if (stateTime >= a.startup && stateTime <= a.startup + a.active)
                    {
                        if (cls.ranged) ShootArrow(a);
                        else StrikeFront(a, 30);
                    }
                    break;
                }
                case FighterState.Dodge:
                    vx = dodgeDir * dodgeSpeed * (1 - stateTime / dodgeMs * 0.6f);
                    vy = MoveY * 200;
                    if (stateTime >= dodgeMs) SetState(FighterState.Idle);
                    break;
                case FighterState.Cast:
                    vx *= 0.8f;
                    vy = 0;
                    if (casting == null) { SetState(FighterState.Idle); break; } // hit mid-cast under super armor used to strand the player here
                    if (stateTime >= casting.cast * d.castMult)
                    {
                        var s = casting;
                        casting = null;
                        if (!ExecuteSkill(s)) SetState(FighterState.CastEnd);
                    }
                    break;
                case FighterState.CastEnd:
                    vx = 0;
                    if (stateTime >= 160) SetState(FighterState.Idle);
                    break;
                case FighterState.Hurt:
                    Decel(dt);
                    if (Now >= stunUntil) SetState(FighterState.Idle);
                    break;
                case FighterState.Knock:
                    break;
                case FighterState.Down:
                    Decel(dt);
                    if (stateTime >= 650 && !dead)
                    {
                        invulnUntil = Now + 700;
                        SetState(FighterState.Idle);
                    }
                    break;
                case FighterState.Dead:
                    Decel(dt);
                    break;
            }

            bool landed = Physics(dt, xMin, xMax);
            if (landed) OnLand(grounded);
            if (state == FighterState.Walk && Now - lastStepAt > 280)
            {
                lastStepAt = Now;
                Fx.Dust(x - facing * 10, y, 2, 0.6f);
            }
            if (state == FighterState.Dodge && Now - lastGhostAt > 40)
            {
                lastGhostAt = Now;
                Fx.Ghost(this);
            }
        }

        void OnLand(bool wasGrounded)
        {
            Fx.Dust(x, y, 6);
            Sfx.Play("land", 0.7f);
            if (state == FighterState.Jump || state == FighterState.AirAttack)
            {
                SetState(FighterState.Idle);
                vx = 0;
            }
            else if (state == FighterState.Knock)
            {
                Fx.Shake(80, 0.004f);
                SetState(dead ? FighterState.Dead : FighterState.Down);
                if (dead) world.OnPlayerDead();
            }
        }

        void FreeMove()
        {
            if (GameInput.Consume(GameAction.Jump))
            {
                vz = JumpV;
                z = 1;
                airAttackUsed = false;
                SetState(FighterState.Jump);
                Sfx.Play("jump");
                Fx.Dust(x, y, 4, 0.8f);
                return;
            }
            if (GameInput.Guard)
            {
                if (TryDodge()) return;
                SetState(FighterState.Guard);
                return;
            }
            if (GameInput.Consume(GameAction.Attack))
            {
                StartAttack(0);
                return;
            }
            if (TrySkill()) return;
            float mx = MoveX, my = MoveY;
            vx = mx * (float)d.move;
            vy = my * (float)d.move * 0.6f;
            if (mx != 0) facing = mx > 0 ? 1 : -1;
            bool moving = Math.Abs(mx) > 0.1f || Math.Abs(my) > 0.1f;
            if (moving && state != FighterState.Walk) SetState(FighterState.Walk);
            if (!moving && state != FighterState.Idle) SetState(FighterState.Idle);
        }

        bool TryDodge()
        {
            float mx = MoveX;
            if (Math.Abs(mx) < 0.5f) return false;
            dodgeDir = mx > 0 ? 1 : -1;
            dodgeMs = DodgeMsDefault;
            dodgeSpeed = DodgeSpeedDefault;
            invulnUntil = Now + DodgeMsDefault;
            SetState(FighterState.Dodge);
            Fx.Aura(x, y - 20, 0xffffff);
            Fx.Dust(x, y, 5);
            Sfx.Play("dodge");
            return true;
        }

        void Decel(float dt)
        {
            float k = 1500 * dt;
            vx = Math.Abs(vx) <= k ? 0 : vx - Math.Sign(vx) * k;
            vy = 0;
        }

        // ---------- normal attacks ----------

        public (float startup, float active, float recovery) Timing(ComboHit h)
        {
            float s = (float)d.aspd;
            return (h.startup / s, h.active / s, h.recovery / s);
        }

        void StartAttack(int i)
        {
            combo = i;
            queued = false;
            struck.Clear();
            shotFired = false;
            BreakHidden();
            if (MoveX != 0) facing = MoveX > 0 ? 1 : -1;
            SetState(FighterState.Attack);
            if (!cls.ranged)
            {
                int step = i;
                world.Delay(Timing(cls.combo[i]).startup * 0.6f, () => { if (state == FighterState.Attack && combo == step) Sfx.Play("swing", 0.8f + i * 0.1f); });
            }
        }

        void UpdateAttack()
        {
            var h = cls.combo[combo];
            var t = Timing(h);
            vy = 0;
            vx = stateTime < t.startup ? facing * 70 : 0;
            float activeEnd = t.startup + t.active;
            if (stateTime >= t.startup && stateTime <= activeEnd)
            {
                if (cls.ranged) ShootArrow(h);
                else
                {
                    if (stateTime - world.LastDt * 1000 < t.startup)
                        Fx.Slash(x + facing * 40, y - 60 - z, facing, cls.accent, 60 + combo * 8, y);
                    StrikeFront(h, 40);
                }
            }
            if (stateTime > t.startup)
            {
                if (combo < cls.combo.Count - 1 && GameInput.Consume(GameAction.Attack)) queued = true;
                if (stateTime > activeEnd && TrySkill()) return;
            }
            if (stateTime >= activeEnd + t.recovery * (queued ? 0.35f : 1f))
            {
                if (queued) StartAttack(combo + 1);
                else SetState(FighterState.Idle);
            }
        }

        void StrikeFront(ComboHit h, float depth)
        {
            bool finisher = combo == cls.combo.Count - 1 && state == FighterState.Attack && Mods.comboFinisherAoeRadius > 0;
            int n;
            if (finisher)
            {
                float r = Mods.comboFinisherAoeRadius;
                if (struck.Count == 0) Fx.Ring(x + facing * 40, y, r, cls.accent, y - 1);
                n = world.StrikeCircle(this, Enemies, x + facing * 40, y, r, tt => NormalHit((Enemy)tt, h));
            }
            else n = world.StrikeFront(this, Enemies, h.range, depth, tt => NormalHit((Enemy)tt, h));
            if (n > 0) world.HitStop(h.launch > 0 ? 90 : 55);
        }

        /// <summary>Ranged classes fire one arrow per combo step instead of swinging.</summary>
        void ShootArrow(ComboHit h)
        {
            if (shotFired) return;
            shotFired = true;
            void Fire(float zOff) => world.SpawnProjectile(new ProjectileOpts
            {
                owner = this, x = x + facing * 30, y = y, z = z + 70 + zOff, dir = facing,
                speed = ArrowSpeed, color = cls.accent, range = h.range, arrow = true, pierce = Mods.pierceAdd,
                make = tt => MakeHit((Enemy)tt, h.power, Element.Neutral, false, h.knock, h.launch, h.stun),
            });
            Fire(0);
            for (int i = 0; i < Mods.projectileAdd; i++) Fire(-12 * (i + 1));
            Sfx.Play("arrow");
            Fx.Spark(x + facing * 36, y - 70 - z, cls.accent, 0.6f);
            float da = PassiveValue(Passive.DoubleAttack);
            if (da > 0 && Rng.Chance(da)) world.Delay(80, () => { if (!dead) Fire(-12); });
        }

        /// <summary>A normal-attack hit, with the thief's double-attack passive rolled in.</summary>
        HitInfo NormalHit(Enemy t, ComboHit h)
        {
            var hit = MakeHit(t, h.power, Element.Neutral, false, h.knock, h.launch, h.stun);
            float da = PassiveValue(Passive.DoubleAttack);
            if (da > 0 && Rng.Chance(da))
            {
                world.Delay(70, () =>
                {
                    if (t.dead || dead) return;
                    Fx.Number(t.x, t.y - t.h - 70 - t.z, "連擊", 0xb9f5c8);
                    Fx.Slash(t.x, t.y - 60 - t.z, facing, cls.accent, 50, t.y);
                    t.TakeHit(MakeHit(t, h.power, Element.Neutral, false, h.knock * 0.5f, 0, h.stun));
                });
            }
            return hit;
        }

        public HitInfo MakeHit(Enemy t, float power, Element element, bool magic, float knock, float launch, float stun)
        {
            var def = t.Defender();
            var r = StatFormulas.CalcDamage(d.atk, d.matk, d.crit, def.def, def.mdef, def.element, def.defMult,
                power, element, magic, Rng.NextDouble);
            int damage = r.damage;
            if (r.crit && Mods.critDamageAdd > 0) damage = (int)StatFormulas.JsRound(damage * (1.5f + Mods.critDamageAdd) / 1.5f);
            int dir = Math.Sign(t.x - x);
            if (dir == 0) dir = facing;
            return new HitInfo
            {
                damage = damage, crit = r.crit, mult = r.mult, element = element, dir = dir,
                knock = knock * Mods.knock, launch = launch, stun = stun, source = this,
            };
        }

        // ---------- skills ----------

        bool TrySkill()
        {
            for (int i = 0; i < SkillKeys.Length; i++)
            {
                if (!GameInput.Peek(SkillKeys[i])) continue;
                GameInput.Consume(SkillKeys[i]);
                if (i >= cls.skills.Count) continue;
                var s = cls.skills[i];
                if (SkillLv(s) < 1) { world.Toast($"尚未學習「{s.name}」"); continue; }
                if (s.type == SkillType.Passive) { world.Toast($"「{s.name}」是被動技能，已自動生效"); continue; }
                if (CooldownLeft(s) > 0) continue;
                int cost = SkillCost(s);
                if (sp < cost) { Fx.Number(x, y - 120, "SP 不足", Colors.Sp); continue; }
                int gold = SkillGold(s);
                if (Inv.gold < gold) { Fx.Number(x, y - 120, "金幣不足", Colors.Gold); continue; }
                if (gold > 0) { Inv.gold -= gold; Fx.Number(x, y - 150, $"-{gold} G", Colors.Gold); }
                sp -= cost;
                cooldowns[s.id] = Now + CooldownTotal(s);
                if (MoveX != 0) facing = MoveX > 0 ? 1 : -1;
                casting = s;
                struck.Clear();
                SetState(FighterState.Cast);
                Fx.Aura(x, y - 10, s.color);
                Sfx.Play(s.magic ? "cast" : "skill", 0.8f);
                return true;
            }
            return false;
        }

        static string SkillSound(SkillDef s) => s.type switch
        {
            SkillType.Melee => "hitHeavy",
            SkillType.Aoe => "fire",
            SkillType.Debuff => "buff",
            SkillType.Buff => "buff",
            SkillType.Heal => "heal",
            SkillType.Dash => "dodge",
            SkillType.Passive => "select",
            SkillType.Projectile => s.element == Element.Water ? "ice" : s.element == Element.Fire ? "fire" : s.element == Element.Holy ? "cast" : "arrow",
            SkillType.Strikes => s.element == Element.Wind ? "thunder" : "arrow",
            SkillType.Wall => "fire",
            _ => "skill",
        };

        /// <summary>Returns true when the skill took over the state machine (e.g. a dash).</summary>
        bool ExecuteSkill(SkillDef s)
        {
            int L = SkillLv(s);
            float power = s.power.At(L);
            var el = s.element;
            var fx = Fx;
            var buffEff = s.BuffAt(L);
            if (buffEff == null || !buffEff.hidden) BreakHidden();
            Sfx.Play(SkillSound(s));
            switch (s.type)
            {
                case SkillType.Melee:
                {
                    fx.Slash(x + facing * 50, y - 60, facing, s.color, 80, y);
                    float stunChance = s.stunChance.HasValue ? s.stunChance.Value.At(L) : 0;
                    int n = world.StrikeFront(this, Enemies, s.range ?? 120, 45, tt =>
                    {
                        var t = (Enemy)tt;
                        float stun = Rng.Chance(stunChance) ? 1800 : s.stun ?? 400;
                        var h = MakeHit(t, power, el, s.magic, s.knock ?? 200, s.launch ?? 0, stun);
                        if (s.poison.HasValue) h.poison = new Poison { dps = s.poison.Value.At(L), duration = 5000 };
                        if (stun > 1000) world.Delay(10, () => fx.Number(t.x, t.y - t.h - 50, "暈眩", Colors.Crit));
                        return h;
                    });
                    if (n > 0) { world.HitStop(110); fx.Shake(90, 0.006f); }
                    break;
                }
                case SkillType.Aoe:
                {
                    float r = s.radius ?? 150;
                    fx.Ring(x, y, r, s.color, y - 1);
                    fx.Spark(x, y - 40, s.color, 2);
                    fx.Shake(160, 0.008f);
                    int n = world.StrikeCircle(this, Enemies, x, y, r, tt =>
                        MakeHit((Enemy)tt, power, el, s.magic, s.knock ?? 300, s.launch ?? 0, s.stun ?? 400));
                    if (n > 0) world.HitStop(120);
                    break;
                }
                case SkillType.Debuff:
                {
                    float r = s.radius ?? 300;
                    fx.Ring(x, y, r, s.color, y - 1);
                    fx.Number(x, y - 130, s.name, Colors.Debuff);
                    float down = s.defDown.HasValue ? s.defDown.Value.At(L) : 0.3f;
                    float dur = s.duration.HasValue ? s.duration.Value.At(L) : 8000;
                    foreach (var e in Enemies)
                    {
                        if (e.dead || !World.InCircle(e.x, e.y, x, y, r * 1.4f)) continue;
                        e.ApplyDefDown(down, dur);
                    }
                    break;
                }
                case SkillType.Buff:
                {
                    float dur = s.duration.HasValue ? s.duration.Value.At(L) : 5000;
                    AddBuff(s, buffEff ?? new BuffEffect { endure = true }, dur);
                    fx.Aura(x, y, s.color);
                    fx.Number(x, y - 130, s.name, Colors.Buff);
                    break;
                }
                case SkillType.Heal:
                {
                    int v = (int)StatFormulas.JsRound(maxHp * (s.heal.HasValue ? s.heal.Value.At(L) : 0.2f) + d.matk * 0.6);
                    hp = Math.Min(maxHp, hp + v);
                    fx.Aura(x, y, s.color);
                    fx.Ring(x, y, s.radius ?? 150, s.color, y - 1);
                    fx.Number(x, y - 130, $"+{v}", Colors.Heal, true);
                    // Holy light also burns the undead nearby.
                    var undead = new List<Enemy>();
                    foreach (var e in Enemies) if (e.def.element == Element.Dark) undead.Add(e);
                    world.StrikeCircle(this, undead, x, y, s.radius ?? 150, tt =>
                        MakeHit((Enemy)tt, power, Element.Holy, true, s.knock ?? 200, 0, s.stun ?? 400));
                    break;
                }
                case SkillType.Dash:
                {
                    float dist = s.dashDistance.HasValue ? s.dashDistance.Value.At(L) : 220;
                    float dur = s.duration.HasValue ? s.duration.Value.At(L) : 260;
                    dodgeDir = -facing;
                    dodgeMs = dur;
                    dodgeSpeed = dist / (dur / 1000f) / 0.7f;
                    invulnUntil = Now + dur + 60;
                    fx.Aura(x, y - 20, s.color);
                    fx.Spark(x, y - 60, s.color, 1.2f);
                    SetState(FighterState.Dodge);
                    return true;
                }
                case SkillType.Passive:
                    break;
                case SkillType.Projectile:
                {
                    int n = (int)Math.Floor(s.count.HasValue ? s.count.Value.At(L) : 1) + Mods.projectileAdd;
                    float freeze = s.freezeChance.HasValue ? s.freezeChance.Value.At(L) : 0;
                    for (int i = 0; i < n; i++)
                    {
                        int k = i;
                        world.Delay(i * 120, () =>
                        {
                            if (dead) return;
                            world.SpawnProjectile(new ProjectileOpts
                            {
                                owner = this, x = x + facing * 40, y = y, z = 60 + (k % 2) * 14, dir = facing,
                                speed = s.speed ?? 700, color = s.color, range = 900, pierce = Mods.pierceAdd,
                                make = tt =>
                                {
                                    var h = MakeHit((Enemy)tt, power, el, s.magic, s.knock ?? 100, 0, s.stun ?? 300);
                                    if (freeze > 0 && Rng.Chance(freeze)) h.freeze = 2200;
                                    return h;
                                },
                            });
                        });
                    }
                    break;
                }
                case SkillType.Strikes:
                {
                    int n = (int)Math.Floor(s.count.HasValue ? s.count.Value.At(L) : 3);
                    float range = s.range ?? 400;
                    float r = s.radius ?? 80;
                    float ox = x;
                    int dir = facing;
                    for (int i = 0; i < n; i++)
                    {
                        world.Delay(i * 130, () =>
                        {
                            float px = ox + dir * (110 + Rng.Value * range);
                            float py = Mathf.Clamp(y + Rng.Between(-70, 70), Units.GroundTopPx, Units.GroundBottomPx);
                            fx.Pillar(px, py, s.color);
                            fx.Shake(60, 0.004f);
                            world.StrikeCircle(this, Enemies, px, py, r, tt =>
                            {
                                var h = MakeHit((Enemy)tt, power, el, s.magic, s.knock ?? 150, 0, s.stun ?? 400);
                                int hd = Math.Sign(tt.x - px);
                                h.dir = hd == 0 ? dir : hd;
                                return h;
                            }, true);
                        });
                    }
                    break;
                }
                case SkillType.Wall:
                {
                    float dur = s.duration.HasValue ? s.duration.Value.At(L) : 4000;
                    world.SpawnWall(new WallOpts
                    {
                        owner = this, x = x + facing * (s.range ?? 150), y = y, duration = dur, color = s.color,
                        make = tt => MakeHit((Enemy)tt, power, el, s.magic, s.knock ?? 300, 0, s.stun ?? 300),
                    });
                    break;
                }
            }
            return false;
        }

        // ---------- items ----------

        void TryPotions()
        {
            if (GameInput.Consume(GameAction.Pot0) && Now >= potionReadyAt)
            {
                if (Inv.red <= 0) { world.Toast("紅色藥水用完了"); return; }
                Inv.red--;
                int v = (int)StatFormulas.JsRound(maxHp * 0.35f * Mods.potionHeal);
                hp = Math.Min(maxHp, hp + v);
                potionReadyAt = Now + 800;
                Fx.Number(x, y - 120, $"+{v}", Colors.Heal);
                Fx.Aura(x, y, 0xff5d5d);
                Sfx.Play("potion");
            }
            if (GameInput.Consume(GameAction.Pot1) && Now >= potionReadyAt)
            {
                if (Inv.blue <= 0) { world.Toast("藍色藥水用完了"); return; }
                Inv.blue--;
                int v = (int)StatFormulas.JsRound(maxSp * 0.4f * Mods.potionHeal);
                sp = Math.Min(maxSp, sp + v);
                potionReadyAt = Now + 800;
                Fx.Number(x, y - 120, $"+{v} SP", Colors.Sp);
                Fx.Aura(x, y, 0x5d8bff);
                Sfx.Play("potion");
            }
        }

        // ---------- receiving damage ----------

        public override bool TakeHit(HitInfo h)
        {
            if (dead || Now < invulnUntil || state == FighterState.Dodge) return false;
            var fx = Fx;
            ActiveBuff shield = null;
            foreach (var b in buffs) if ((b.hits ?? 0) > 0) { shield = b; break; }
            if (shield != null)
            {
                shield.hits--;
                fx.Ring(x, y, 80, shield.color, y - 1);
                fx.Number(x, y - 120 - z, "結界", 0xcfe8ff);
                if (shield.hits <= 0) ExpireBuffs();
                return true;
            }
            if (Rng.NextDouble() * 100 < d.flee)
            {
                fx.Number(x, y - 110 - z, "MISS", Colors.Miss);
                return false;
            }
            bool guarding = state == FighterState.Guard && Math.Sign(h.source.x - x) == facing;
            if (guarding)
            {
                int dmg = Math.Max(1, (int)StatFormulas.JsRound(h.damage * 0.25));
                hp = Math.Max(1, hp - dmg);
                vx = h.dir * 160;
                fx.Spark(x + facing * 25, y - 60, Colors.Guard);
                fx.Number(x, y - 110, $"{dmg}", Colors.Guard);
                Sfx.Play("guard");
                return true;
            }
            hp -= h.damage;
            fx.Number(x, y - 110 - z, $"{h.damage}", Colors.Hp, h.damage > maxHp * 0.2f);
            fx.Spark(x, y - 55 - z, 0xff7070);
            Flash(0xff4444, 90);
            fx.Shake(90, 0.005f);
            Sfx.Play("hurt");
            if (casting != null) { casting = null; if (state == FighterState.Cast) SetState(FighterState.CastEnd); }
            if (hp <= 0)
            {
                if (Mods.reviveFraction > 0 && !Mods.reviveUsed)
                {
                    Mods.reviveUsed = true;
                    hp = Mathf.Max(1, Mathf.Round(maxHp * Mods.reviveFraction));
                    invulnUntil = Now + Mods.reviveInvuln;
                    fx.Flash(0.6f, 300);
                    fx.Aura(x, y, Colors.Crit);
                    fx.Ring(x, y, 200, Colors.Crit, y - 1);
                    fx.Number(x, y - 150, "不死鳥！", Colors.Crit, true);
                    Sfx.Play("levelup");
                    SetState(FighterState.Idle);
                    return true;
                }
                hp = 0;
                dead = true;
                vz = 520;
                z = 1;
                vx = h.dir * 260;
                SetState(FighterState.Knock);
                return true;
            }
            if (Endure) return true;
            if (state == FighterState.Knock)
            {
                // Already flying: take the damage but don't re-launch, so repeated hits can't juggle the player
                // across the room. Keep the current momentum and just make sure they still come down.
                vz = Math.Max(vz, 120);
                z = Math.Max(z, 1);
            }
            else if (h.launch > 0 || Airborne)
            {
                vz = Math.Max(h.launch, 380);
                z = Math.Max(z, 1);
                vx = h.dir * Math.Max(180, h.knock * 0.6f);
                SetState(FighterState.Knock);
            }
            else
            {
                vx = h.dir * h.knock;
                stunUntil = Now + h.stun;
                SetState(FighterState.Hurt);
            }
            return true;
        }

        /// <summary>Heal on kill and death explosions (perks); called by the world when an enemy dies to the player.</summary>
        public void OnKilled(Enemy e)
        {
            if (Mods.onKillHeal > 0 && !dead)
            {
                int v = (int)StatFormulas.JsRound(maxHp * Mods.onKillHeal);
                hp = Math.Min(maxHp, hp + v);
                Fx.Number(x, y - 120 - z, $"+{v}", Colors.Heal);
            }
            if (Mods.onKillAoePower > 0)
            {
                float r = Mods.onKillAoeRadius, ex = e.x, ey = e.y;
                float power = Mods.onKillAoePower;
                world.Delay(60, () =>
                {
                    Fx.Ring(ex, ey, r, 0xff8a5a, ey - 1);
                    Fx.Spark(ex, ey - 40, 0xff8a5a, 1.5f);
                    world.StrikeCircle(this, Enemies, ex, ey, r, tt =>
                    {
                        var h = MakeHit((Enemy)tt, power, Element.Neutral, false, 220, 0, 350);
                        int hd = Math.Sign(tt.x - ex);
                        h.dir = hd == 0 ? 1 : hd;
                        return h;
                    }, true);
                });
            }
        }
    }
}
