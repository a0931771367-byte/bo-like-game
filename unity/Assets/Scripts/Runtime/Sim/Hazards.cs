using System;
using System.Collections.Generic;

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
                if (t.dead || hit.Contains(t) || Math.Abs(t.y - o.y) > 50f) continue;
                if (Math.Abs(t.x - o.x) < t.w / 2f + 14f && t.z < o.z + 40f)
                {
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
                if (t.dead || Math.Abs(t.x - o.x) > 45f + t.w / 2f || Math.Abs(t.y - o.y) > 80f || t.z > 60f) continue;
                if (lastTick.TryGetValue(t, out var last) && now - last < 350) continue;
                lastTick[t] = now;
                var h = o.make(t);
                h.dir = Math.Sign(t.x - o.x) == 0 ? 1 : Math.Sign(t.x - o.x);
                t.TakeHit(h);
            }
        }
    }
}
