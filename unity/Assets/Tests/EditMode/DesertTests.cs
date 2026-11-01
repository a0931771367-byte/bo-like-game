using BoLike.Data;
using BoLike.Sim;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    /// <summary>Desert ruins enemy behaviours (design doc 08 §2.5): burrower, reviver, crab facing, turret volley, guardian phases.</summary>
    public class DesertTests
    {
        static World NewWorld(float playerX = 200)
        {
            var w = new World();
            var cls = GameDb.Class("swordsman");
            w.Player = new Player(w, playerX, 500, cls, ClassProgress.New(cls));
            return w;
        }

        static void Run(World w, float seconds) { int n = (int)(seconds * 60); for (int i = 0; i < n; i++) w.Step(1f / 60f); }

        [Test]
        public void DesertDataHasTheBehaviourFields()
        {
            Assert.IsNotNull(GameDb.Enemy("sandworm").burrow);
            Assert.IsNotNull(GameDb.Enemy("mummy").revive);
            Assert.IsNotNull(GameDb.Enemy("sandCrab").facingDef);
            Assert.AreEqual(3, GameDb.Enemy("cactusTurret").volley);
            Assert.AreEqual(2, GameDb.Enemy("ruinGuardian").phases.Count);
            Assert.AreEqual(EnemyBehavior.BossGuardian, GameDb.Enemy("ruinGuardian").behavior);
        }

        [Test]
        public void SandwormHidesUntilThePlayerComesCloseThenEmerges()
        {
            var w = NewWorld(200);
            var worm = w.SpawnEnemy(GameDb.Enemy("sandworm"), 600, 500);
            Assert.IsTrue(worm.Burrowed);
            Assert.IsFalse(worm.CanBeHit);
            Run(w, 1f);
            Assert.IsTrue(worm.Burrowed, "far away: stays under the sand");
            Assert.AreEqual(0, w.StrikeCircle(w.Player, w.Enemies, 600, 500, 200, _ => new HitInfo { damage = 50, dir = 1, source = w.Player }), "cannot be targeted");
            Assert.IsFalse(worm.TakeHit(new HitInfo { damage = 50, dir = 1, source = w.Player }));
            w.Player.x = 500;
            w.Step(1f / 60f);
            Assert.Greater(worm.EmergeAt, 0, "the pile starts shaking");
            Assert.IsTrue(worm.Burrowed);
            Run(w, worm.def.burrow.emergeDelay / 1000f + 0.1f);
            Assert.IsFalse(worm.Burrowed, "bursts out after emergeDelay");
            Assert.IsTrue(worm.CanBeHit);
        }

        [Test]
        public void MummyLeavesAPileThatRevivesOnceUnlessBroken()
        {
            var w = NewWorld(200);
            var mummy = w.SpawnEnemy(GameDb.Enemy("mummy"), 600, 500);
            Assert.IsTrue(mummy.TakeHit(new HitInfo { damage = 999, dir = 1, source = w.Player }));
            Assert.IsTrue(mummy.dead);
            Run(w, 2f);
            Assert.IsTrue(mummy.Pile, "lands and becomes a bandage pile");
            Assert.Contains(mummy, w.Enemies);
            Assert.IsTrue(mummy.CanBeHit);
            Assert.AreEqual(0, w.AliveEnemies, "a pile does not hold the room");
            mummy.TakeHit(new HitInfo { damage = 10, dir = 1, source = w.Player });
            Assert.AreEqual(mummy.def.revive.pileHp - 10, mummy.pileHp);
            Run(w, mummy.def.revive.delay / 1000f + 0.2f);
            Assert.IsFalse(mummy.Pile);
            Assert.IsFalse(mummy.dead, "revived");
            Assert.AreEqual(UnityEngine.Mathf.Round(mummy.maxHp * mummy.def.revive.fraction), mummy.hp);
            Assert.AreEqual(1, w.AliveEnemies);
            // The second death is final.
            mummy.TakeHit(new HitInfo { damage = 999, dir = 1, source = w.Player });
            Run(w, 2.5f);
            Assert.IsFalse(mummy.Pile);
            CollectionAssert.DoesNotContain(w.Enemies, mummy);

            var other = w.SpawnEnemy(GameDb.Enemy("mummy"), 600, 500);
            other.TakeHit(new HitInfo { damage = 999, dir = 1, source = w.Player });
            Run(w, 2f);
            Assert.IsTrue(other.Pile);
            other.TakeHit(new HitInfo { damage = 999, dir = 1, source = w.Player });
            Assert.IsFalse(other.Pile, "broken pile");
            Run(w, other.def.revive.delay / 1000f + 1f);
            Assert.IsTrue(other.dead, "never revives");
            CollectionAssert.DoesNotContain(w.Enemies, other);
        }

        [Test]
        public void CrabTakesHalfDamageFromTheFrontAndMoreFromBehind()
        {
            var w = NewWorld(200);
            var crab = w.SpawnEnemy(GameDb.Enemy("sandCrab"), 400, 500);
            Assert.AreEqual(-1, crab.facing, "faces the player on spawn");
            float hp = crab.hp;
            crab.TakeHit(new HitInfo { damage = 100, dir = 1, source = w.Player });
            Assert.AreEqual(50, hp - crab.hp, "front: shell halves it");
            hp = crab.hp;
            w.Player.x = 600; // behind the crab (it still faces left)
            crab.TakeHit(new HitInfo { damage = 100, dir = -1, source = w.Player });
            Assert.AreEqual(160, hp - crab.hp, "back: soft spot");
        }

        [Test]
        public void CactusTurretFiresAVolleyOfThree()
        {
            var w = NewWorld(200);
            int shots = 0;
            w.ProjectileSpawned += _ => shots++;
            w.SpawnEnemy(GameDb.Enemy("cactusTurret"), 500, 500);
            Run(w, 1.3f); // windup 500 ms + 3 shots 120 ms apart
            Assert.AreEqual(3, shots);
        }

        [Test]
        public void GuardianPetrifiesAtHalfHealthWithHelpersThenEnrages()
        {
            var w = NewWorld(200);
            var boss = w.SpawnEnemy(GameDb.Enemy("ruinGuardian"), 700, 500);
            float atk = boss.atkMult;
            boss.TakeHit(new HitInfo { damage = 1800, dir = 1, source = w.Player });
            Assert.IsTrue(boss.PetrifiedBoss, "50% phase");
            Assert.AreEqual(3, w.Enemies.Count, "two mummies join");
            float hp = boss.hp;
            Assert.IsFalse(boss.TakeHit(new HitInfo { damage = 100, dir = 1, source = w.Player }), "invulnerable while petrified");
            Assert.AreEqual(hp, boss.hp);
            Run(w, 4.2f);
            Assert.IsFalse(boss.PetrifiedBoss);
            Assert.IsFalse(boss.Enraged);
            boss.TakeHit(new HitInfo { damage = 1000, dir = 1, source = w.Player });
            Assert.IsTrue(boss.Enraged, "20% phase");
            Assert.AreEqual(atk * 1.3f, boss.atkMult, 1e-4);
            Assert.IsFalse(boss.dead);
        }
    }
}
