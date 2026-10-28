using System;
using BoLike.Core;
using BoLike.Data;
using BoLike.Systems;
using UnityEngine;

namespace BoLike.Sim
{
    public struct Defender
    {
        public float def, mdef, defMult;
        public Element element;
    }

    /// <summary>Port of src/entities/Enemy.ts: the seven behaviours, attacks and damage intake.</summary>
    public class Enemy : Fighter
    {
        public override Team Team => Team.Enemy;
        public readonly EnemyDef def;
        public readonly float atkMult;
        public double attackReadyAt;
        public float defDownMult = 1;
        public double defDownUntil;
        public int hitCount;
        public double lastHitAt;
        public float poisonDps;
        public double poisonUntil;
        double poisonNext;
        int lungeDir = 1;
        double retreatUntil;
        /// <summary>Per-instance wobble offset so a crowd does not bounce in sync.</summary>
        public readonly float phase;
        /// <summary>Set once death animation starts; the view removes the enemy when it finishes.</summary>
        public double dyingSince = -1;

        public Enemy(World world, float x, float y, EnemyDef def, float hpMult = 1f, float atkMult = 1f) : base(world, x, y, def.w, def.h)
        {
            this.def = def;
            this.atkMult = atkMult;
            maxHp = hp = Mathf.Round(def.hp * hpMult);
            facing = world.Player != null && x > world.Player.x ? -1 : 1;
            phase = Rng.Value * 1000f;
        }

        public Defender Defender() => new()
        {
            def = def.def, mdef = def.mdef, element = def.element, defMult = Now < defDownUntil ? defDownMult : 1,
        };

        public bool Weakened => Now < defDownUntil;
        public bool Poisoned => Now < poisonUntil;

        public void ApplyDefDown(float amount, float duration)
        {
            defDownMult = Math.Max(0, 1 - amount);
            defDownUntil = Now + duration;
            Fx.Number(x, y - h - 40, "防禦↓", Colors.Debuff);
        }

        Player P => world.Player;

        public override void Update(float dt)
        {
            float ms = dt * 1000;
            stateTime += ms;
            var (xMin, xMax) = world.EnemyBounds();
            if (!dead && Now < poisonUntil && Now >= poisonNext)
            {
                poisonNext = Now + 500;
                ApplyDot(Math.Max(1, (int)StatFormulas.JsRound(poisonDps * 0.5)), Colors.Poison);
            }
            if (Frozen && !dead)
            {
                vx = vy = 0;
                if (state != FighterState.Knock) SetState(FighterState.Frozen);
            }
            else if (state == FighterState.Frozen) SetState(FighterState.Idle);

            switch (state)
            {
                case FighterState.Idle:
                case FighterState.Walk:
                    Think();
                    break;
                case FighterState.Windup:
                    vx = vy = 0;
                    if (stateTime >= def.windup) DoAttack();
                    break;
                case FighterState.Lunge:
                    vx = lungeDir * def.speed * 2.6f;
                    vy = 0;
                    if (stateTime > 90 && stateTime < 420) TryHitPlayer(def.attackRange * 0.35f, 1.1f, 260, 0, 500);
                    if (stateTime >= 480) { SetState(FighterState.Recover); vx = 0; }
                    break;
                case FighterState.Attack:
                    vx = vy = 0;
                    if (stateTime >= 70 && stateTime <= 190) TryHitPlayer(def.attackRange, 1, 220, 0, 420);
                    if (stateTime >= 360) SetState(FighterState.Recover);
                    break;
                case FighterState.Slam:
                    vx = 0;
                    break;
                case FighterState.Stomp:
                    vx = vy = 0;
                    if (stateTime >= 520)
                    {
                        Fx.Shake(220, 0.014f);
                        Fx.Ring(x, y, 240, 0x9be15d, y - 1);
                        Fx.Dust(x, y, 14, 2);
                        world.Sfx.Play("thunder", 0.8f);
                        TryHitPlayer(230, 1.4f, 420, 480, 500, true);
                        SetState(FighterState.Recover);
                    }
                    break;
                case FighterState.Recover:
                    vx *= 0.85f;
                    if (stateTime >= (def.boss ? 500 : 400))
                    {
                        retreatUntil = def.behavior == EnemyBehavior.Lunger ? Now + 900 : 0;
                        SetState(FighterState.Idle);
                    }
                    break;
                case FighterState.Hurt:
                    vx *= 0.86f;
                    vy = 0;
                    if (Now >= stunUntil) SetState(FighterState.Idle);
                    break;
                case FighterState.Knock:
                    break;
                case FighterState.Down:
                    vx *= 0.8f;
                    if (stateTime >= 700) SetState(FighterState.Idle);
                    break;
                case FighterState.Frozen:
                case FighterState.Dying:
                    break;
            }
            bool landed = Physics(dt, xMin, xMax);
            if (landed) OnLand();
            if (def.behavior == EnemyBehavior.Flyer && !dead)
            {
                // Bees hover instead of falling; a dive drops them to the player's height.
                float hover = state == FighterState.Attack ? 10 : 58 + Mathf.Sin((float)((Now + phase) / 180)) * 9;
                z += (hover - z) * Math.Min(1, dt * 10);
                vz = 0;
            }
        }

        void OnLand()
        {
            Fx.Dust(x, y, def.boss ? 8 : 3, def.boss ? 1.6f : 0.8f);
            if (state == FighterState.Knock)
            {
                Fx.Ring(x, y, 40, 0xffffff, y - 1);
                if (dead) Die();
                else
                {
                    vx = 0;
                    SetState(def.boss ? FighterState.Recover : FighterState.Down);
                }
            }
            else if (state == FighterState.Slam)
            {
                Fx.Shake(180, 0.012f);
                Fx.Ring(x, y, 190, 0xff9fc0, y - 1);
                TryHitPlayer(170, 1.3f, 400, 460, 500, true);
                SetState(FighterState.Recover);
            }
            else if (state == FighterState.Idle || state == FighterState.Walk)
            {
                vx = 0;
            }
        }

        /// <summary>Damage that never interrupts (poison ticks).</summary>
        void ApplyDot(int dmg, int color)
        {
            hp -= dmg;
            Fx.Number(x + Rng.Between(-10, 10), y - h - 20 - z, $"{dmg}", color);
            if (hp <= 0)
            {
                hp = 0;
                dead = true;
                frozenUntil = 0;
                vz = 420;
                z = Math.Max(z, 1);
                vx = -facing * 220;
                SetState(FighterState.Knock);
                world.OnEnemyKilled(this);
            }
        }

        static float Clamp1(float v) => Mathf.Clamp(v, -1, 1);

        void Think()
        {
            var p = P;
            if (p == null || p.dead || p.Hidden)
            {
                // Nothing to chase: stand around (hidden players are invisible to enemies).
                vx = vy = 0;
                if (state != FighterState.Idle) SetState(FighterState.Idle);
                return;
            }
            float dx = p.x - x;
            float dy = p.y - y;
            float dist = Math.Abs(dx);
            facing = dx >= 0 ? 1 : -1;
            float sp = def.speed;
            bool canAttack = Now >= attackReadyAt;
            bool aligned = Math.Abs(dy) < 36;
            int sdx = Math.Sign(dx);

            switch (def.behavior)
            {
                case EnemyBehavior.Hopper:
                    if (!Airborne)
                    {
                        if (dist < def.attackRange && aligned && canAttack) { SetState(FighterState.Windup); return; }
                        vz = 330;
                        z = 1;
                        vx = sdx * sp * (dist > 60 ? 1 : 0);
                        vy = Clamp1(dy) * sp * 0.5f;
                    }
                    return;
                case EnemyBehavior.Melee:
                    if (dist < def.attackRange && aligned && canAttack) { SetState(FighterState.Windup); return; }
                    vx = dist > def.attackRange * 0.7f ? sdx * sp : 0;
                    vy = Clamp1(dy / 20) * sp * 0.7f;
                    break;
                case EnemyBehavior.Lunger:
                    if (Now < retreatUntil)
                    {
                        vx = -sdx * sp * 0.8f;
                        vy = Clamp1(-dy / 20) * sp * 0.4f;
                        break;
                    }
                    if (dist < def.attackRange && dist > 90 && aligned && canAttack) { lungeDir = sdx == 0 ? 1 : sdx; SetState(FighterState.Windup); return; }
                    vx = sdx * sp * (dist > 150 ? 1 : -0.6f);
                    vy = Clamp1(dy / 20) * sp * 0.7f;
                    break;
                case EnemyBehavior.BossSlime:
                    if (canAttack && aligned)
                    {
                        if (dist < def.attackRange) { SetState(FighterState.Windup); return; }
                        if (dist < 420 && Rng.Chance(0.012)) { StartSlam(dx); return; }
                    }
                    vx = sdx * sp * (dist > 90 ? 1 : 0);
                    vy = Clamp1(dy / 20) * sp * 0.6f;
                    break;
                case EnemyBehavior.Flyer:
                    // Circle in from the side, then sting when lined up.
                    if (dist < def.attackRange && aligned && canAttack) { SetState(FighterState.Windup); return; }
                    vx = sdx * sp * (dist > 70 ? 1 : -0.4f);
                    vy = Clamp1(dy / 16) * sp * 0.8f;
                    break;
                case EnemyBehavior.Turret:
                    // Rooted: turn to face, spit when the player is roughly on the same lane.
                    vx = vy = 0;
                    if (dist < def.attackRange && dist > 60 && Math.Abs(dy) < 70 && canAttack) { SetState(FighterState.Windup); return; }
                    break;
                case EnemyBehavior.BossTreant:
                    if (canAttack && aligned)
                    {
                        if (dist < def.attackRange) { SetState(FighterState.Windup); return; }
                        if (dist < 300 && Rng.Chance(0.015))
                        {
                            Fx.Number(x, y - h - 40, "!", 0x9be15d, true);
                            attackReadyAt = Now + def.attackCd + 1200;
                            SetState(FighterState.Stomp);
                            return;
                        }
                    }
                    vx = sdx * sp * (dist > 120 ? 1 : 0);
                    vy = Clamp1(dy / 20) * sp * 0.5f;
                    break;
            }
            bool moving = Math.Abs(vx) + Math.Abs(vy) > 1;
            if (moving && state != FighterState.Walk) SetState(FighterState.Walk);
            if (!moving && state != FighterState.Idle) SetState(FighterState.Idle);
        }

        void StartSlam(float dx)
        {
            Fx.Number(x, y - h - 40, "!", Colors.Crit, true);
            vz = 820;
            z = 1;
            vx = Mathf.Clamp(dx / 0.9f, -520, 520);
            attackReadyAt = Now + def.attackCd + 800;
            SetState(FighterState.Slam);
        }

        void DoAttack()
        {
            attackReadyAt = Now + def.attackCd;
            if (def.behavior == EnemyBehavior.Lunger) { SetState(FighterState.Lunge); return; }
            if (def.behavior == EnemyBehavior.Turret)
            {
                int dir = facing;
                world.SpawnProjectile(new ProjectileOpts
                {
                    owner = this, targets = Team.Player, x = x + dir * 30, y = y, z = 70, dir = dir, speed = 430, color = 0x9bd45a,
                    range = def.attackRange + 80, make = _ => MakeHit(1, 240, 0, 400, dir),
                });
                world.Sfx.Play("arrow", 0.6f);
                SetState(FighterState.Attack);
                return;
            }
            SetState(FighterState.Attack);
            Fx.Slash(x + facing * 30, y - h * 0.5f, facing, 0xffffff, 40 + def.w * 0.3f, y);
        }

        void TryHitPlayer(float range, float powerMult, float knock, float launch, float stun, bool circle = false)
        {
            var p = P;
            if (p == null || struck.Contains(p) || p.dead) return;
            float dx = p.x - x;
            bool inRange = circle
                ? World.InCircle(p.x, p.y, x, y, range)
                : Math.Sign(dx) == facing && Math.Abs(dx) < range + p.w / 2 && Math.Abs(p.y - y) < 50;
            if (!inRange || p.z > 90) return;
            struck.Add(p);
            int dir = Math.Sign(dx);
            p.TakeHit(MakeHit(powerMult, knock, launch, stun, dir == 0 ? 1 : dir));
        }

        HitInfo MakeHit(float powerMult, float knock, float launch, float stun, int dir)
        {
            var p = P;
            float pdef = p != null ? (float)p.d.def : 0;
            int dmg = Math.Max(1, (int)StatFormulas.JsRound((def.atk * atkMult * powerMult - pdef * 0.6) * (0.9 + Rng.NextDouble() * 0.2)));
            return new HitInfo { damage = dmg, crit = false, mult = 1, element = def.element, dir = dir, knock = knock, launch = launch, stun = stun, source = this };
        }

        public override bool TakeHit(HitInfo h)
        {
            if (dead) return false;
            hp -= h.damage;
            hitCount = Now - lastHitAt < 1500 ? hitCount + 1 : 1;
            lastHitAt = Now;
            var fx = Fx;
            if (h.source == world.Player) world.RegisterHit();
            world.Sfx.Play(h.crit ? "crit" : h.launch > 0 ? "hitHeavy" : "hit");
            int color = h.crit ? Colors.Crit : h.mult > 1 ? 0xff9f43 : h.mult < 1 ? 0xa0a0ff : Colors.White;
            fx.Number(x, y - h - 30 - z, h.crit ? $"{h.damage}!" : $"{h.damage}", color, h.crit || h.mult > 1);
            fx.Spark(x, y - this.h / 2 - z, Colors.White, h.launch > 0 ? 1.5f : 1f);
            Flash(Colors.White, 70);
            if (h.poison.HasValue)
            {
                poisonDps = Math.Max(poisonDps, h.poison.Value.dps);
                poisonUntil = Now + h.poison.Value.duration;
                poisonNext = Now + 500;
                fx.Number(x, y - this.h - 60, "中毒", Colors.Poison);
            }
            if (h.freeze > 0)
            {
                frozenUntil = Now + h.freeze;
                fx.Number(x, y - this.h - 60, "凍結", Colors.Ice);
            }
            struck.Clear();
            if (hp <= 0)
            {
                hp = 0;
                dead = true;
                frozenUntil = 0;
                vz = 480;
                z = Math.Max(z, 1);
                vx = h.dir * 320;
                SetState(FighterState.Knock);
                world.OnEnemyKilled(this);
                return true;
            }
            if (def.boss)
            {
                // Bosses have super armor except against launchers; they flinch briefly.
                if (h.launch > 0 && hitCount % 4 == 0)
                {
                    vz = 300; z = Math.Max(z, 1); vx = h.dir * 120; SetState(FighterState.Knock);
                }
                else if (state != FighterState.Windup && state != FighterState.Slam)
                {
                    vx = h.dir * h.knock * 0.25f;
                    stunUntil = Now + Math.Min(h.stun, 220);
                    SetState(FighterState.Hurt);
                }
                return true;
            }
            if (h.launch > 0 || Airborne || state == FighterState.Knock)
            {
                vz = Math.Max(h.launch, 420);
                z = Math.Max(z, 1);
                vx = h.dir * Math.Max(200, h.knock * 0.7f);
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

        void Die()
        {
            SetState(FighterState.Dying);
            dyingSince = Now;
            Fx.Spark(x, y - h / 2, def.color, def.boss ? 3 : 1.6f);
            Fx.Dust(x, y, def.boss ? 14 : 6, def.boss ? 2 : 1);
            if (def.boss) { Fx.Flash(0.7f, 400); Fx.Shake(400, 0.012f); }
            world.Delay(260, () => world.RemoveEnemy(this));
        }
    }
}
