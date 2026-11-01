using System;
using System.Collections.Generic;
using BoLike.Data;

namespace BoLike.Sim
{
    public class ProjectileOpts
    {
        public Fighter owner;
        public float x, y, z;
        public int dir;
        public float speed;
        public int color;
        public float range;
        /// <summary>Draw as an arrow shaft instead of a magic orb.</summary>
        public bool arrow;
        /// <summary>Who this projectile hurts.</summary>
        public Team targets = Team.Enemy;
        /// <summary>Extra targets the projectile passes through before it is spent.</summary>
        public int pierce;
        public Func<Fighter, HitInfo> make;
    }

    /// <summary>Port of Hazards.ts Projectile. Flies straight along x at a fixed lane y and height z.</summary>
    public class Projectile
    {
        public readonly ProjectileOpts o;
        public float travelled;
        public bool done;
        public int hitsLeft;
        readonly World world;
        readonly HashSet<Fighter> hit = new();

        public Projectile(World world, ProjectileOpts o)
        {
            this.world = world;
            this.o = o;
            hitsLeft = 1 + Math.Max(0, o.pierce);
        }

        /// <summary>Visual y offset in px (magic orbs bob).</summary>
        public float Bob => o.arrow ? 0f : (float)Math.Sin(travelled / 40f) * 3f;

        public void Update(float dt, IReadOnlyList<Fighter> targets)
        {
            float step = o.speed * dt;
            o.x += o.dir * step;
            travelled += step;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (!t.CanBeHit || hit.Contains(t) || Math.Abs(t.y - o.y) > 50f) continue;
                if (Math.Abs(t.x - o.x) < t.w / 2f + 14f && t.z < o.z + 40f)
                {
                    if (t is Player pl && o.targets == Team.Player && pl.ReflectsProjectiles)
                    {
                        // 反彈 (reflectProjectile): the shot turns around as the player's own, hurting its shooter and anything behind.
                        o.dir = -o.dir;
                        o.targets = Team.Enemy;
                        o.owner = pl;
                        o.color = 0xffe08a;
                        o.make = tt => pl.MakeHit((Enemy)tt, 1.2f, Element.Neutral, false, 160, 0, 300);
                        travelled = 0;
                        hit.Clear();
                        hitsLeft = Math.Max(hitsLeft, 2);
                        world.Fx.Spark(o.x, o.y - o.z, 0xffe08a, 1.3f);
                        world.Sfx.Play("guard");
                        return;
                    }
                    hit.Add(t);
                    t.TakeHit(o.make(t));
                    world.HitStop(40f);
                    if (--hitsLeft <= 0) { Finish(); return; }
                    world.Fx.Spark(o.x, o.y - o.z, o.color, 0.6f);
                }
            }
            if (travelled > o.range) Finish();
        }

        void Finish()
        {
            done = true;
            world.Fx.Spark(o.x, o.y - o.z, o.color);
        }
    }

    public class WallOpts
    {
        public Fighter owner;
        public float x, y, duration;
        public int color;
        public Func<Fighter, HitInfo> make;
    }

    /// <summary>Port of Hazards.ts Wall: a standing hazard that ticks damage on anyone inside it.</summary>
    public class Wall
    {
        public readonly WallOpts o;
        public bool done;
        readonly World world;
        readonly double until;
        readonly Dictionary<Fighter, double> lastTick = new();

        public Wall(World world, WallOpts o)
        {
            this.world = world;
            this.o = o;
            until = world.Now + o.duration;
            world.Fx.Ring(o.x, o.y, 90f, o.color, o.y - 1);
        }

        public float TimeLeft => (float)(until - world.Now);

        public void Update(IReadOnlyList<Fighter> targets)
        {
            double now = world.Now;
            if (now > until) { done = true; return; }
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (!t.CanBeHit || t.dead || Math.Abs(t.x - o.x) > 45f + t.w / 2f || Math.Abs(t.y - o.y) > 80f || t.z > 60f) continue;
                if (lastTick.TryGetValue(t, out var last) && now - last < 350) continue;
                lastTick[t] = now;
                var h = o.make(t);
                h.dir = Math.Sign(t.x - o.x) == 0 ? 1 : Math.Sign(t.x - o.x);
                t.TakeHit(h);
            }
        }
    }

    public class ZoneOpts
    {
        public Fighter owner;
        public float x, y, radius, duration;
        public int color;
        /// <summary>stickyBlob | thornWall | quicksand | iceBurst | spikeTrap (the view picks the look by this).</summary>
        public string what;
        /// <summary>Movement speed multiplier applied to enemies inside while they stay inside (1 = none).</summary>
        public float slow = 1f;
        /// <summary>Root (immobilise) applied once to each enemy that enters, ms; 0 = none.</summary>
        public float rootMs;
        /// <summary>Damage per 350 ms tick to enemies inside; 0 = none.</summary>
        public int damage;
        public Element element = Element.Neutral;
        /// <summary>毒沼 (room affix): hurts the player standing inside instead of enemies, this fraction of max HP per second.</summary>
        public bool hurtsPlayer;
        public float playerDpsFraction;
    }

    /// <summary>
    /// A ground zone left by an epic card (S2): sticky blob roots whoever steps in, a thorn wall hurts and slows,
    /// quicksand only slows. Circular, on the ground, affects enemies (not the player).
    /// </summary>
    public class Zone
    {
        public readonly ZoneOpts o;
        public bool done;
        readonly World world;
        readonly double until;
        readonly Dictionary<Fighter, double> lastTick = new();
        readonly HashSet<Fighter> rooted = new();

        public Zone(World world, ZoneOpts o)
        {
            this.world = world;
            this.o = o;
            until = world.Now + o.duration;
        }

        public float TimeLeft => (float)(until - world.Now);
        public float Progress => o.duration <= 0 ? 1f : (float)Math.Clamp(1 - TimeLeft / o.duration, 0, 1);

        public void Update(IReadOnlyList<Enemy> targets)
        {
            double now = world.Now;
            if (now > until) { done = true; return; }
            if (o.hurtsPlayer)
            {
                var p = world.Player;
                if (p == null || p.dead || p.z > 60f) return;
                float pdx = p.x - o.x, pdy = (p.y - o.y) * 1.6f;
                if (pdx * pdx + pdy * pdy > o.radius * o.radius) return;
                if (lastTick.TryGetValue(p, out var lastP) && now - lastP < 500) return;
                lastTick[p] = now;
                p.TakeGroundDamage(Math.Max(1, (int)Math.Round(p.maxHp * o.playerDpsFraction * 0.5f)), o.color);
                return;
            }
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (!t.CanBeHit || t.dead || t.z > 60f) continue;
                float dx = t.x - o.x, dy = (t.y - o.y) * 1.6f; // the ground is drawn foreshortened, so the zone is an ellipse
                if (dx * dx + dy * dy > (o.radius + t.w / 2f) * (o.radius + t.w / 2f)) continue;
                if (o.slow < 1f) t.ApplySlow(o.slow, 120);
                if (o.rootMs > 0 && !rooted.Contains(t) && !t.def.boss) { rooted.Add(t); t.ApplyRoot(o.rootMs); }
                if (o.damage > 0)
                {
                    if (lastTick.TryGetValue(t, out var last) && now - last < 350) continue;
                    lastTick[t] = now;
                    int dir = Math.Sign(t.x - o.x) == 0 ? 1 : Math.Sign(t.x - o.x);
                    t.TakeHit(new HitInfo { damage = o.damage, dir = dir, knock = 0, source = o.owner, element = o.element });
                }
            }
        }
    }
}
