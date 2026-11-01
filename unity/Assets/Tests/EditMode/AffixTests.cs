using System.Linq;
using BoLike.Data;
using BoLike.Game;
using BoLike.Sim;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    /// <summary>Room affixes and elites (design doc 08 §2.6): data, roll rules, determinism, and the elite modifiers in the sim.</summary>
    public class AffixTests
    {
        static World NewWorld()
        {
            var w = new World();
            var cls = GameDb.Class("swordsman");
            w.Player = new Player(w, 200, 500, cls, ClassProgress.New(cls));
            return w;
        }

        [Test]
        public void AffixDataIsConsistent()
        {
            Assert.AreEqual(5, GameDb.Affixes.Count);
            foreach (var a in GameDb.Affixes) Assert.AreEqual(a.id, a.effect.kind, a.id);
            foreach (var st in GameDb.RunStages)
                foreach (var id in st.affixes) Assert.IsTrue(GameDb.HasAffix(id), $"{st.id}: unknown affix {id}");
            var elite = GameDb.Elite;
            Assert.AreEqual(3, elite.modifiers.Count);
            Assert.AreEqual(0f, elite.ChanceFor(1), "no elites in chapter 1");
            Assert.Greater(elite.ChanceFor(2), 0f);
            Assert.IsFalse(elite.Eligible(GameDb.Enemy("jellyKing")), "bosses never elite");
            Assert.IsTrue(elite.Eligible(GameDb.Enemy("wolf")));
            Assert.IsTrue(GameDb.Rules.affixes.skipFirstRoom);
        }

        [Test]
        public void AffixesOnlyRollOnStagesClearedBeforeAndNeverInRoomOne()
        {
            var fresh = new SaveData();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var r = RunState.New("swordsman", fresh, "grassland-3", seed);
                Assert.IsTrue(r.roomAffixes.All(a => a == null), "no affixes before the stage is cleared");
                Assert.IsTrue(r.roomElite.All(e => !e), "chapter 1 has no elites");
            }
            var cleared = new SaveData();
            cleared.stagesCleared.Add("grassland-3");
            int withAffix = 0, rooms = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var r = RunState.New("swordsman", cleared, "grassland-3", seed);
                Assert.IsNull(r.roomAffixes[0], "room 1 never has an affix");
                Assert.AreEqual(r.roomOrder.Count, r.roomAffixes.Count);
                for (int i = 1; i < r.roomAffixes.Count; i++)
                {
                    rooms++;
                    if (r.roomAffixes[i] == null) continue;
                    withAffix++;
                    Assert.IsTrue(GameDb.RunStage("grassland-3").affixes.Contains(r.roomAffixes[i]), r.roomAffixes[i]);
                }
                // 精英房 forces an elite even in chapter 1 rooms (grassland has no 'elite' affix, so this is a no-op check there).
                for (int i = 0; i < r.roomAffixes.Count; i++)
                    if (r.roomAffixes[i] != null && GameDb.Affix(r.roomAffixes[i]).effect.forceElite) Assert.IsTrue(r.roomElite[i]);
            }
            float rate = (float)withAffix / rooms;
            Assert.That(rate, Is.InRange(0.38f, 0.62f), $"about half of the later rooms carry an affix, got {rate:0.00}");
        }

        [Test]
        public void RollsAreDeterministicAndDoNotShiftRoomsOrPerks()
        {
            var save = new SaveData();
            save.stagesCleared.Add("grassland-3");
            var a = RunState.New("swordsman", save, "grassland-3", 777);
            var b = RunState.New("swordsman", save, "grassland-3", 777);
            CollectionAssert.AreEqual(a.roomAffixes, b.roomAffixes);
            CollectionAssert.AreEqual(a.roomElite, b.roomElite);
            CollectionAssert.AreEqual(a.eliteSpawnPick, b.eliteSpawnPick);
            // The same seed on a fresh save (no affixes) still draws the same rooms and perks: the affix stream is separate.
            var c = RunState.New("swordsman", new SaveData(), "grassland-3", 777);
            CollectionAssert.AreEqual(a.roomOrder, c.roomOrder);
            CollectionAssert.AreEqual(a.waveSetByRoom, c.waveSetByRoom);
            CollectionAssert.AreEqual(a.DrawPerks(3, 0).Select(p => p.id).ToList(), c.DrawPerks(3, 0).Select(p => p.id).ToList());
        }

        [Test]
        public void ElitesAppearInLaterChaptersAtAboutTheirChance()
        {
            var save = new SaveData();
            int elites = 0, rooms = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var r = RunState.New("swordsman", save, "mistForest-3", seed);
                foreach (var e in r.roomElite) { rooms++; if (e) elites++; }
            }
            float rate = (float)elites / rooms, want = GameDb.Elite.ChanceFor(2);
            Assert.That(rate, Is.InRange(want - 0.06f, want + 0.06f), $"elite rate {rate:0.00} vs {want}");
        }

        [Test]
        public void ShieldEliteBlocksTheFirstHitsThenTakesDamage()
        {
            var w = NewWorld();
            var rules = GameDb.Elite;
            var wolf = w.SpawnEnemy(GameDb.Enemy("wolf"), 600, 500, 1f, 1f, e => e.MakeElite(rules, "shield"));
            Assert.IsTrue(wolf.elite);
            Assert.AreEqual(rules.sizeMult, wolf.sizeMult);
            Assert.AreEqual(UnityEngine.Mathf.Round(GameDb.Enemy("wolf").hp * rules.hpMult), wolf.maxHp);
            float hp = wolf.hp;
            for (int i = 0; i < rules.shieldHits; i++)
            {
                Assert.IsFalse(wolf.TakeHit(new HitInfo { damage = 10, dir = 1, source = w.Player }), $"hit {i + 1} is shielded");
                Assert.AreEqual(hp, wolf.hp);
            }
            Assert.IsTrue(wolf.TakeHit(new HitInfo { damage = 10, dir = 1, source = w.Player }));
            Assert.AreEqual(hp - 10, wolf.hp);
        }

        [Test]
        public void HasteEliteMovesFasterAndVolatileExplosionHurts()
        {
            var w = NewWorld();
            var rules = GameDb.Elite;
            var fast = w.SpawnEnemy(GameDb.Enemy("wolf"), 600, 500, 1f, 1f, e => e.MakeElite(rules, "haste"));
            Assert.AreEqual(rules.hasteMult, fast.SpeedMult, 1e-5);
            Assert.AreEqual(0, fast.shieldHits);
            var boom = w.SpawnEnemy(GameDb.Enemy("wolf"), 300, 500, 1f, 1f, e => e.MakeElite(rules, "volatile"));
            var hit = boom.ExplosionHit(rules.volatilePower, 1);
            Assert.Greater(hit.damage, 0);
            Assert.AreSame(boom, hit.source);
            float before = w.Player.hp;
            w.Player.TakeHit(hit);
            Assert.Less(w.Player.hp, before);
        }

        [Test]
        public void PoisonGroundHurtsThePlayerNotEnemies()
        {
            var w = NewWorld();
            var wolf = w.SpawnEnemy(GameDb.Enemy("wolf"), 200, 500);
            float wolfHp = wolf.hp, playerHp = w.Player.hp;
            w.SpawnZone(new ZoneOpts { x = 200, y = 500, radius = 130, duration = 1e8f, what = "poisonGround", hurtsPlayer = true, playerDpsFraction = 0.01f });
            for (int i = 0; i < 70; i++) w.Step(1f / 60f); // > 1 s: at least two 500 ms ticks
            Assert.Less(w.Player.hp, playerHp);
            Assert.GreaterOrEqual(playerHp - w.Player.hp, 2, "about 0.5% max HP per tick, min 1");
            Assert.AreEqual(wolfHp, wolf.hp, "enemies are not hurt by 毒沼");
            Assert.AreEqual(1, w.Zones.Count, "the patch stays for the whole room");
        }
    }
}
