using System;
using System.Collections.Generic;
using BoLike.Core;
using BoLike.Data;

namespace BoLike.Sim
{
    /// <summary>
    /// The simulation container: owns the clock, the fighters, projectiles and walls, and the world queries
    /// the entities use (port of the entity-facing half of GameScene.ts). The game layer drives it with
    /// <see cref="Step"/> once per frame and listens to its events to create and remove views.
    /// </summary>
    public class World
    {
        /// <summary>Simulation clock in ms. Keeps running during hit stop (cooldowns and delays use it).</summary>
        public double Now { get; private set; }
        /// <summary>The scaled dt of the last step, in seconds.</summary>
        public float LastDt { get; private set; } = 0.016f;

        public Player Player;
        public readonly List<Enemy> Enemies = new();
        public readonly List<Projectile> Projectiles = new();
        public readonly List<Wall> Walls = new();
        readonly List<Fighter> playerList = new();

        public IFx Fx = new NullFx();
        public ISfx Sfx = new NullSfx();
        public Rng Rng = new(1);
        public Inventory Inv = new();
        public RunMods Mods = new();

        /// <summary>Camera scroll in px and the view width in px (depends on the device aspect).</summary>
        public float CamX, ViewW = Units.ViewWidthPx;
        public float StageWidth = 4200;
        /// <summary>Right edge the player cannot pass while a wave is active.</summary>
        public float LockRight = Units.ViewWidthPx;
        public bool Paused;

        public event Action<Enemy> EnemySpawned;
        public event Action<Enemy> EnemyKilled;
        public event Action<Enemy> EnemyRemoved;
        public event Action<Projectile> ProjectileSpawned;
        public event Action<Wall> WallSpawned;
        public event Action PlayerDied;
        /// <summary>The player lost HP to a hit (not a guard, miss or shield).</summary>
        public event Action PlayerHurt;
        public void RaisePlayerHurt() => PlayerHurt?.Invoke();
        public event Action<string> Toasted;
        public event Action<int> ComboChanged;

        double hitStopUntil;
        readonly List<(double at, Action act)> delayed = new();
        int comboHits;
        double comboUntil;

        // ---------- world queries used by entities ----------

        public (float min, float max) PlayerBounds() => (CamX + 30, Math.Min(StageWidth - 30, LockRight - 30));

        public (float min, float max) EnemyBounds() =>
            (CamX - 80, Math.Min(StageWidth + 80, Math.Max(LockRight, CamX + ViewW) + 80));

        public IReadOnlyList<Fighter> TargetsOf(Team t)
        {
            if (t == Team.Player) { playerList.Clear(); if (Player != null) playerList.Add(Player); return playerList; }
            return Enemies;
        }

        /// <summary>Front arc hit test; returns the number of new targets struck.</summary>
        public int StrikeFront(Fighter src, IReadOnlyList<Enemy> targets, float range, float depthY, Func<Fighter, HitInfo> make)
        {
            int n = 0;
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var t = targets[i];
                if (t.dead || src.struck.Contains(t)) continue;
                float dx = t.x - src.x;
                if (Math.Sign(dx) != src.facing && Math.Abs(dx) > t.w * 0.5f) continue;
                if (Math.Abs(dx) > range + t.w / 2 || Math.Abs(t.y - src.y) > depthY || Math.Abs(t.z - src.z) > 120) continue;
                src.struck.Add(t);
                if (t.TakeHit(make(t))) n++;
            }
            return n;
        }

        /// <summary>Lane-aware circle test (y counts double, like the Phaser build).</summary>
        public static bool InCircle(float tx, float ty, float x, float y, float r)
        {
            float dx = tx - x, dy = (ty - y) * 2;
            return dx * dx + dy * dy <= r * r;
        }

        public int StrikeCircle(Fighter src, IReadOnlyList<Enemy> targets, float x, float y, float r, Func<Fighter, HitInfo> make, bool ignoreStruck = false)
        {
            int n = 0;
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var t = targets[i];
                if (t.dead || (!ignoreStruck && src.struck.Contains(t))) continue;
                if (!InCircle(t.x, t.y, x, y, r + t.w / 2)) continue;
                if (!ignoreStruck) src.struck.Add(t);
                if (t.TakeHit(make(t))) n++;
            }
            return n;
        }

        public void SpawnProjectile(ProjectileOpts o)
        {
            var p = new Projectile(this, o);
            Projectiles.Add(p);
            ProjectileSpawned?.Invoke(p);
        }

        public void SpawnWall(WallOpts o)
        {
            var w = new Wall(this, o);
            Walls.Add(w);
            WallSpawned?.Invoke(w);
        }

        public Enemy SpawnEnemy(EnemyDef def, float x, float y, float hpMult = 1f, float atkMult = 1f)
        {
            var e = new Enemy(this, x, y, def, hpMult, atkMult);
            Enemies.Add(e);
            EnemySpawned?.Invoke(e);
            return e;
        }

        public void HitStop(float ms) => hitStopUntil = Math.Max(hitStopUntil, Now + ms);
        public void Toast(string msg) => Toasted?.Invoke(msg);
        public void Delay(float ms, Action act) => delayed.Add((Now + ms, act));

        /// <summary>Counts consecutive player hits for the combo display.</summary>
        public void RegisterHit()
        {
            comboHits = Now < comboUntil ? comboHits + 1 : 1;
            comboUntil = Now + 1600;
            ComboChanged?.Invoke(comboHits);
        }

        public int AliveEnemies
        {
            get { int n = 0; foreach (var e in Enemies) if (!e.dead) n++; return n; }
        }

        // ---------- entity callbacks ----------

        public void OnEnemyKilled(Enemy e) => EnemyKilled?.Invoke(e);
        public void OnPlayerDead() => PlayerDied?.Invoke();

        public void RemoveEnemy(Enemy e)
        {
            Enemies.Remove(e);
            EnemyRemoved?.Invoke(e);
        }

        public void ClearHazards()
        {
            foreach (var p in Projectiles) p.done = true;
            foreach (var w in Walls) w.done = true;
            Projectiles.Clear();
            Walls.Clear();
            delayed.Clear();
        }

        // ---------- stepping ----------

        public void Step(float realDt)
        {
            if (Paused) return;
            Now += realDt * 1000;
            float dt = Math.Min(realDt, 0.05f);
            if (Now < hitStopUntil) dt *= 0.08f;
            LastDt = dt;

            if (delayed.Count > 0)
            {
                for (int i = 0; i < delayed.Count; i++)
                {
                    if (delayed[i].at > Now) continue;
                    var act = delayed[i].act;
                    delayed.RemoveAt(i--);
                    act();
                }
            }

            Player?.Update(dt);
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Update(dt);
            for (int i = 0; i < Projectiles.Count; i++) Projectiles[i].Update(dt, TargetsOf(Projectiles[i].o.targets));
            Projectiles.RemoveAll(p => p.done);
            for (int i = 0; i < Walls.Count; i++) Walls[i].Update(Enemies);
            Walls.RemoveAll(w => w.done);
        }
    }
}
