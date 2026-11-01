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
        public float atkMult;
        public double attackReadyAt;
        public float defDownMult = 1;
        public double defDownUntil;
        /// <summary>Sticky blob (果凍王冠): cannot move until this time.</summary>
        public double rootedUntil;
        double slowUntil; float slowMult = 1f;
        public bool Rooted => Now < rootedUntil;
        public float SpeedMult => (Now < slowUntil ? slowMult : 1f) * hasteMult;
        // ---- elite (design doc 08 §2.6) ----
        public bool elite;
        /// <summary>shield | haste | volatile, null when not elite.</summary>
        public string eliteMod;
        public int shieldHits;
        public float sizeMult = 1f, hasteMult = 1f;

        /// <summary>Turns a freshly spawned enemy into an elite: more HP/ATK, bigger, one modifier. Call before the view exists.</summary>
        public void MakeElite(EliteRules rules, string mod)
        {
            elite = true;
            eliteMod = mod;
            maxHp = hp = Mathf.Round(maxHp * rules.hpMult);
            atkMult *= rules.atkMult;
            sizeMult = rules.sizeMult;
            if (mod == "shield") shieldHits = rules.shieldHits;
            if (mod == "haste") hasteMult = rules.hasteMult;
        }

        // ---- desert ruins behaviours (design doc 08 §2.5) ----
        /// <summary>沙蟲 underground: untargetable, drawn as a sand pile; <see cref="EmergeAt"/> > 0 while the pile shakes before it bursts out.</summary>
        public bool Burrowed;
        public double EmergeAt;
        double lastNearAt;
        /// <summary>木乃伊 bandage pile: dead but about to revive; the pile itself can be broken.</summary>
        public bool Pile;
        public float pileHp;
        double reviveAt;
        bool revived;
        /// <summary>石像 phases: petrified (invulnerable, still) and enraged (ATK up, red).</summary>
        public double PetrifiedUntil;
        public bool Enraged;
        public bool PetrifiedBoss => Now < PetrifiedUntil;
        readonly HashSet<int> phasesDone = new();
        int beamDir = 1;
        bool beamFired;
        public override bool CanBeHit => Pile || (!dead && !Burrowed);

        /// <summary>爆裂 elite: the death explosion's hit on the player.</summary>
        public HitInfo ExplosionHit(float powerMult, int dir) => MakeHit(powerMult, 240, 0, 0, dir);
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
            if (def.behavior == EnemyBehavior.Burrower && def.burrow != null) Burrowed = true;
        }

        public Defender Defender() => new()
        {
            def = def.def, mdef = def.mdef, element = def.element, defMult = Now < defDownUntil ? defDownMult : 1,
        };

        public bool Weakened => Now < defDownUntil;
        public bool Poisoned => Now < poisonUntil;

        public void ApplyRoot(float ms)
        {
            if (dead || def.boss) return;
            rootedUntil = Math.Max(rootedUntil, Now + ms);
            Fx.Number(x, y - h - 40, "黏住", 0xffc6e0);
        }

        /// <summary>Movement × mult while inside a zone; refreshed every tick, so it fades as soon as the enemy leaves.</summary>
        public void ApplySlow(float mult, float ms)
        {
            slowMult = Math.Min(slowMult < 1f && Now < slowUntil ? slowMult : 1f, mult);
            slowUntil = Now + ms;
        }

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
            if (Pile)
            {
                // 繃帶堆: lies still until it revives or is broken.
                vx = vy = 0;
                if (Now >= reviveAt) Revive();
                return;
            }
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
                        if (def.behavior == EnemyBehavior.BossGuardian)
                        {
                            // 砸地震波: a wide ground wave that launches; airborne players (z > 90) are missed.
                            Fx.Shake(260, 0.016f);
                            Fx.Ring(x, y, 320, 0xc9b28a, y - 1);
                            Fx.Dust(x, y, 18, 2.2f);
                            world.Sfx.Play("guardianQuake");
                            TryHitPlayer(300, 1.3f, 460, 520, 500, true);
                        }
                        else
                        {
                            Fx.Shake(220, 0.014f);
                            Fx.Ring(x, y, 240, 0x9be15d, y - 1);
                            Fx.Dust(x, y, 14, 2);
                            world.Sfx.Play("thunder", 0.8f);
                            TryHitPlayer(230, 1.4f, 420, 480, 500, true);
                        }
                        SetState(FighterState.Recover);
                    }
                    break;
                case FighterState.Beam:
                    vx = vy = 0;
                    if (stateTime < 700)
                    {
                        // Telegraph: a red line of sparks along the lane, refreshed every 80 ms.
                        if ((int)(stateTime / 80) != (int)((stateTime - ms) / 80))
                            for (int k = 1; k <= 7; k++) Fx.Spark(x + beamDir * (60 + k * 95), y - 60, 0xff4d4d, 0.6f);
                    }
                    else if (!beamFired)
                    {
                        beamFired = true;
                        world.SpawnProjectile(new ProjectileOpts
                        {
                            owner = this, targets = Team.Player, x = x + beamDir * 60, y = y, z = 60, dir = beamDir, speed = 2600, color = 0xff4d4d,
                            range = 760, pierce = 9, make = _ => MakeHit(1.6f, 300, 0, 600, beamDir),
                        });
                        world.Sfx.Play("guardianBeam");
                        Fx.Shake(200, 0.01f);
                    }
                    if (stateTime >= 1000) SetState(FighterState.Recover);
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
            if (!landed && state == FighterState.Knock && z <= 0 && vz <= 0 && stateTime > 150) landed = true; // knocked while grounded: resolve instead of sliding
            if (landed) OnLand();
            if (def.behavior == EnemyBehavior.Flyer && !dead && state != FighterState.Knock && state != FighterState.Down)
            {
                // (A knocked bee falls and lands like everyone else; hovering through Knock left it sliding forever.)
                // Bees hover instead of falling; a dive drops them to the player's height.
                float hover = state == FighterState.Attack ? 10 : 58 + Mathf.Sin((float)((Now + phase) / 180)) * 9;
                z += (hover - z) * Math.Min(1, dt * 10);
                vz = 0;
            }
        }

        /// <summary>True when moving in direction <paramref name="dir"/> would leave the strip the player can reach.</summary>
        bool Cornered(float dir)
        {
            var (pmin, pmax) = world.PlayerBounds();
            return (dir < 0 && x <= pmin + 70) || (dir > 0 && x >= pmax - 70);
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
            bool decoy = world.DecoyActive;
            if (p == null || p.dead || (p.Hidden && !decoy))
            {
                // Nothing to chase: stand around (hidden players are invisible to enemies).
                vx = vy = 0;
                if (state != FighterState.Idle) SetState(FighterState.Idle);
                return;
            }
            // A decoy (影分身) draws the chase; attacks still check the real player's position, so they swing at air.
            float dx = (decoy ? world.DecoyX : p.x) - x;
            float dy = (decoy ? world.DecoyY : p.y) - y;
            float dist = Math.Abs(dx);
            if (PetrifiedBoss)
            {
                // 石化 phase: a statue again — still and invulnerable while its mummies come out.
                vx = vy = 0;
                if (state != FighterState.Idle) SetState(FighterState.Idle);
                return;
            }
            facing = dx >= 0 ? 1 : -1;
            float sp = def.speed * SpeedMult;
            if (Rooted && !def.boss) sp = 0;
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
                case EnemyBehavior.Reviver:
                    if (dist < def.attackRange && aligned && canAttack) { SetState(FighterState.Windup); return; }
                    vx = dist > def.attackRange * 0.7f ? sdx * sp : 0;
                    vy = Clamp1(dy / 20) * sp * 0.7f;
                    break;
                case EnemyBehavior.Burrower:
                {
                    var bd = def.burrow;
                    if (Burrowed)
                    {
                        vx = vy = 0;
                        if (state != FighterState.Idle) SetState(FighterState.Idle);
                        if (bd == null) { Burrowed = false; break; }
                        if (EmergeAt <= 0 && dist < bd.triggerRange && Math.Abs(dy) < 90)
                        {
                            // The pile starts shaking; the worm bursts out emergeDelay later.
                            EmergeAt = Now + bd.emergeDelay;
                            Fx.Number(x, y - 40, "!", 0xe0c070, true);
                            world.RequestHint("hint.burrow");
                        }
                        if (EmergeAt > 0 && Now >= EmergeAt)
                        {
                            Burrowed = false; EmergeAt = 0; lastNearAt = Now;
                            vz = 380; z = 1;
                            Fx.Dust(x, y, 12, 1.5f);
                            Fx.Ring(x, y, 110, 0xe0c070, y - 1);
                            world.Sfx.Play("wormEmerge");
                            attackReadyAt = Now + 500;
                        }
                        return;
                    }
                    if (bd == null || dist < bd.triggerRange * 1.6f) lastNearAt = Now;
                    else if (Now - lastNearAt > bd.rest && !Airborne)
                    {
                        // Nobody near for a while: back under the sand.
                        Burrowed = true; vx = vy = 0; struck.Clear();
                        Fx.Dust(x, y, 8, 1.2f);
                        if (state != FighterState.Idle) SetState(FighterState.Idle);
                        return;
                    }
                    goto case EnemyBehavior.Melee;
                }
                case EnemyBehavior.Crab:
                    // Sidles: mostly horizontal, drifting onto the player's lane only once close.
                    if (dist < def.attackRange && aligned && canAttack) { SetState(FighterState.Windup); return; }
                    vx = dist > def.attackRange * 0.7f ? sdx * sp : 0;
                    vy = dist < def.attackRange * 2.5f ? Clamp1(dy / 20) * sp * 0.35f : 0;
                    break;
                case EnemyBehavior.BossGuardian:
                    if (canAttack)
                    {
                        if (dist < def.attackRange && aligned) { SetState(FighterState.Windup); return; }
                        if (dist > 150 && dist < 460 && Rng.Chance(0.012)) { StartQuake(); return; }
                        if (dist > 220 && dist < 720 && Math.Abs(dy) < 60 && Rng.Chance(0.014)) { StartBeam(sdx); return; }
                    }
                    vx = sdx * sp * (dist > 130 ? 1 : 0);
                    vy = Clamp1(dy / 20) * sp * 0.5f;
                    break;
                case EnemyBehavior.Lunger:
                {
                    // Never back off past where the player can follow: a wolf pinned against the room edge
                    // used to retreat forever and become unreachable.
                    bool cornered = Cornered(-sdx);
                    if (Now < retreatUntil && !cornered)
                    {
                        vx = -sdx * sp * 0.8f;
                        vy = Clamp1(-dy / 20) * sp * 0.4f;
                        break;
                    }
                    if (cornered) retreatUntil = 0;
                    if (dist < def.attackRange && (dist > 90 || cornered) && aligned && canAttack) { lungeDir = sdx == 0 ? 1 : sdx; SetState(FighterState.Windup); return; }
                    vx = sdx * sp * (dist > 150 ? 1 : cornered ? 0 : -0.6f);
                    vy = Clamp1(dy / 20) * sp * 0.7f;
                    break;
                }
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
                    vx = sdx * sp * (dist > 70 ? 1 : Cornered(-sdx) ? 0 : -0.4f);
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

        void StartQuake()
        {
            Fx.Number(x, y - h - 40, "!", 0xc9b28a, true);
            attackReadyAt = Now + def.attackCd + 1400;
            world.RequestHint("hint.guardianQuake");
            SetState(FighterState.Stomp);
        }

        void StartBeam(int sdx)
        {
            beamDir = sdx == 0 ? facing : sdx;
            facing = beamDir;
            beamFired = false;
            attackReadyAt = Now + def.attackCd + 1600;
            Fx.Number(x, y - h - 40, "!!", 0xff4d4d, true);
            world.Sfx.Play("guardianBeamWarn");
            world.RequestHint("hint.guardianBeam");
            SetState(FighterState.Beam);
        }

        /// <summary>Boss HP-fraction phases (ruinGuardian): petrify spawns helpers and makes the boss invulnerable for a while; enrage raises ATK.</summary>
        void CheckPhases()
        {
            if (def.phases == null || hp <= 0) return;
            for (int i = 0; i < def.phases.Count; i++)
            {
                var ph = def.phases[i];
                if (phasesDone.Contains(i) || hp > maxHp * ph.at) continue;
                phasesDone.Add(i);
                if (ph.action == "petrify")
                {
                    PetrifiedUntil = Now + (ph.duration > 0 ? ph.duration : 4000);
                    vx = vy = 0;
                    SetState(FighterState.Idle);
                    Fx.Ring(x, y, 200, 0xc9b28a, y - 1);
                    Fx.Number(x, y - h - 60, "石化！", 0xc9b28a, true);
                    world.Sfx.Play("guardianPetrify");
                    if (ph.spawn != null)
                        for (int k = 0; k < ph.spawn.Count; k++)
                        {
                            float sx = x + (k % 2 == 0 ? -1 : 1) * (160 + 40 * (k / 2));
                            var helper = world.SpawnEnemy(GameDb.Enemy(ph.spawn[k]), Mathf.Clamp(sx, 60, world.StageWidth - 60), y, 1f, atkMult);
                            helper.attackReadyAt = Now + 800;
                            Fx.Dust(helper.x, helper.y, 8, 1.2f);
                        }
                }
                else if (ph.action == "enrage")
                {
                    Enraged = true;
                    atkMult *= ph.mult > 0 ? ph.mult : 1.3f;
                    Fx.Number(x, y - h - 60, "狂怒！", 0xff4d4d, true);
                    Fx.Aura(x, y, 0xff4d4d);
                    world.Sfx.Play("bossGrowl");
                }
            }
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
                int shots = Math.Max(1, def.volley); // 仙人掌哨: a volley of needles 120 ms apart
                for (int i = 0; i < shots; i++)
                {
                    world.Delay(i * 120, () =>
                    {
                        if (dead) return;
                        world.SpawnProjectile(new ProjectileOpts
                        {
                            owner = this, targets = Team.Player, x = x + dir * 30, y = y, z = 70, dir = dir, speed = 430, color = 0x9bd45a,
                            range = def.attackRange + 80, make = _ => MakeHit(1, 240, 0, 400, dir),
                        });
                        world.Sfx.Play("arrow", 0.6f);
                    });
                }
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
            int dmg = Math.Max(1, (int)StatFormulas.JsRound(BaseDamage(def.atk * atkMult, powerMult, pdef, GameDb.Rules.enemyDamage) * (0.9 + Rng.NextDouble() * 0.2)));
            return new HitInfo { damage = dmg, crit = false, mult = 1, element = def.element, dir = dir, knock = knock, launch = launch, stun = stun, source = this };
        }

        /// <summary>
        /// Pre-roll enemy damage: a plain atk − def×coef subtraction with a floor of atk × minFraction, so high-DEF
        /// classes still take pressure late in a run (growth curve v2 E1).
        /// </summary>
        public static double BaseDamage(double atk, double powerMult, double def, EnemyDamage rule)
        {
            double floor = atk * (rule?.minFraction ?? 0);
            double raw = atk * powerMult - def * (rule?.defCoef ?? 0.6);
            return Math.Max(floor, raw);
        }

        public override bool TakeHit(HitInfo h)
        {
            if (Pile)
            {
                // 繃帶堆: break it before the mummy gets back up.
                pileHp -= h.damage;
                Fx.Number(x, y - 40, $"{h.damage}", 0xe0d6b0);
                Fx.Spark(x, y - 20, 0xe0d6b0, 0.8f);
                Flash(Colors.White, 60);
                world.Sfx.Play("hit");
                if (h.source == world.Player) world.RegisterHit();
                if (pileHp <= 0)
                {
                    Pile = false; revived = true; dyingSince = Now;
                    Fx.Dust(x, y, 10, 1.4f);
                    Fx.Number(x, y - 60, "打碎！", 0xe0d6b0, true);
                    world.Sfx.Play("pileBreak");
                    world.Delay(DyingMs, () => world.RemoveEnemy(this));
                }
                return true;
            }
            if (dead || Burrowed) return false;
            if (PetrifiedBoss)
            {
                Fx.Number(x, y - this.h - 30 - z, "石化", 0xc9b28a);
                Fx.Spark(x, y - this.h / 2 - z, 0xc9b28a, 0.8f);
                world.Sfx.Play("guard");
                return false;
            }
            if (def.behavior == EnemyBehavior.Crab && def.facingDef != null && h.source != null)
            {
                // 沙蟹: the shell in front halves damage, the soft back takes extra (design doc 08 §2.5).
                bool front = Math.Sign(h.source.x - x) == facing || h.source.x == x;
                float k = front ? def.facingDef.front : def.facingDef.back;
                h.damage = Math.Max(1, (int)StatFormulas.JsRound(h.damage * k));
                if (front) { Fx.Number(x, y - this.h - 60 - z, "硬殼", 0xa0a0ff); world.Sfx.Play("crabShellHit"); world.RequestHint("hint.crab"); }
                else Fx.Number(x, y - this.h - 60 - z, "弱點！", 0xff9f43);
            }
            if (shieldHits > 0 && h.damage > 0)
            {
                // 護盾 elite: the first hits bounce off.
                shieldHits--;
                Fx.Number(x, y - this.h - 30 - z, GameDb.Str("elite.shield") + (shieldHits > 0 ? $" {shieldHits}" : ""), 0xffd36b);
                Fx.Spark(x, y - this.h / 2 - z, 0xffd36b, 1.2f);
                Flash(0xffd36b, 70);
                world.Sfx.Play("guard");
                return false;
            }
            hp -= h.damage;
            hitCount = Now - lastHitAt < 1500 ? hitCount + 1 : 1;
            lastHitAt = Now;
            var fx = Fx;
            if (h.source == world.Player) world.RegisterHit();
            world.Sfx.Play(h.crit ? "crit" : h.launch > 0 ? "hitHeavy" : "hit");
            int color = h.crit ? Colors.Crit : h.mult > 1 ? 0xff9f43 : h.mult < 1 ? 0xa0a0ff : Colors.White;
            fx.Number(x, y - this.h - 30 - z, h.crit ? $"{h.damage}!" : $"{h.damage}", color, h.crit || h.mult > 1);
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
            CheckPhases();
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

        /// <summary>Pushed aside by a dash (shield dash, jet dash): no damage, a short stagger. Bosses hold their ground.</summary>
        public void Shove(int dir, float knock)
        {
            if (dead || def.boss || state == FighterState.Dying || state == FighterState.Knock) return;
            vx = dir * knock;
            stunUntil = Now + 240;
            SetState(FighterState.Hurt);
            Flash(Colors.White, 60);
            Fx.Dust(x, y, 3, 0.8f);
        }

        /// <summary>Corpse time: down 120 ms, flat 120 ms, fade 200 ms (the view draws the art poses over this).</summary>
        public const int DyingMs = 440;

        void Die()
        {
            SetState(FighterState.Dying);
            dyingSince = Now;
            Fx.Spark(x, y - h / 2, def.color, def.boss ? 3 : 1.6f);
            Fx.Dust(x, y, def.boss ? 14 : 6, def.boss ? 2 : 1);
            if (def.boss) { Fx.Flash(0.7f, 400); Fx.Shake(400, 0.012f); }
            if (def.behavior == EnemyBehavior.Reviver && def.revive != null && !revived)
            {
                // 木乃伊: leaves a bandage pile that revives once unless the player breaks it.
                Pile = true;
                pileHp = def.revive.pileHp;
                reviveAt = Now + def.revive.delay;
                vx = vy = 0; z = 0; vz = 0;
                Fx.Number(x, y - 50, "繃帶堆", 0xe0d6b0);
                return;
            }
            world.Delay(DyingMs, () => world.RemoveEnemy(this));
        }

        void Revive()
        {
            Pile = false; revived = true; dead = false; dyingSince = -1;
            hp = Mathf.Max(1, Mathf.Round(maxHp * def.revive.fraction));
            frozenUntil = 0; poisonUntil = 0; struck.Clear();
            z = 0; vz = 0;
            attackReadyAt = Now + 600;
            SetState(FighterState.Idle);
            Fx.Ring(x, y, 90, 0xe0d6b0, y - 1);
            Fx.Dust(x, y, 8, 1.2f);
            Fx.Number(x, y - h - 40, "復活！", 0xe0d6b0, true);
            world.Sfx.Play("mummyRevive");
        }
    }
}
