using System.Collections.Generic;
using BoLike.Core;

namespace BoLike.Sim
{
    public enum FighterState
    {
        Idle, Walk, Guard, Attack, Jump, AirAttack, Dodge, Cast, CastEnd, Hurt, Knock, Down, Dead,
        Windup, Lunge, Slam, Stomp, Recover, Frozen, Dying,
    }

    /// <summary>
    /// Port of src/entities/Fighter.ts. Pure simulation: positions in Phaser pixels (x right, y down the
    /// walkable strip, z height), times in ms. The view layer reads these fields; nothing here touches Unity.
    /// </summary>
    public abstract class Fighter
    {
        public float x, y, z, vx, vy, vz;
        public int facing = 1;
        public float hp = 1, maxHp = 1;
        public FighterState state = FighterState.Idle;
        /// <summary>ms spent in the current state.</summary>
        public float stateTime;
        public double invulnUntil, superArmorUntil, frozenUntil, stunUntil;
        public bool dead;
        /// <summary>Targets already struck by the current attack.</summary>
        public readonly HashSet<Fighter> struck = new();
        public readonly float w, h;
        public abstract Team Team { get; }
        public readonly World world;

        // Presentation requests, consumed by the view.
        public int flashColor = 0xffffff;
        public double flashUntil;

        protected Fighter(World world, float x, float y, float w, float h)
        {
            this.world = world;
            this.x = x; this.y = y; this.w = w; this.h = h;
        }

        public double Now => world.Now;
        public bool Airborne => z > 0.5f || vz > 0;
        public bool Frozen => Now < frozenUntil;
        public IFx Fx => world.Fx;
        public Rng Rng => world.Rng;

        public void SetState(FighterState s)
        {
            state = s;
            stateTime = 0;
        }

        /// <summary>Integrates motion. Returns true on the frame the fighter lands.</summary>
        public bool Physics(float dt, float xMin, float xMax)
        {
            bool landed = false;
            // Integrate whenever the fighter is off the ground at all, not only while Airborne (z > 0.5).
            // During hit-stop a fall advances ~1 px per frame, so z used to settle in (0, 0.5] with gravity off:
            // never airborne, never landed, and the player stayed in Jump/AirAttack until an enemy hit them.
            if (z > 0 || vz > 0)
            {
                vz -= Units.GravityPx * dt;
                z += vz * dt;
                if (z <= 0) { z = 0; vz = 0; landed = true; }
            }
            x = UnityEngine.Mathf.Clamp(x + vx * dt, xMin, xMax);
            y = UnityEngine.Mathf.Clamp(y + vy * dt, Units.GroundTopPx, Units.GroundBottomPx);
            return landed;
        }

        public void Flash(int color = 0xffffff, float ms = 80f)
        {
            flashColor = color;
            flashUntil = Now + ms;
        }

        public abstract void Update(float dt);
        public abstract bool TakeHit(HitInfo h);
    }
}
