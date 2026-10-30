using System.Collections.Generic;
using BoLike.Data;
using BoLike.Game;
using BoLike.Sim;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    /// <summary>Growth curve v2 (design doc 05, items E1–E5).</summary>
    public class GrowthCurveTests
    {
        [Test]
        public void MetaCarriesTheV2Rules()
        {
            var r = GameDb.Rules;
            Assert.AreEqual(1, r.perkOffer.minCommon);
            Assert.AreEqual(1, r.perkOffer.maxEpic);
            Assert.AreEqual(0.3f, r.levelUp.heal, 1e-5);
            Assert.AreEqual(4, r.levelUp.expScalingBySlot.Count);
            Assert.AreEqual(2f, r.levelUp.bossExpMult, 1e-5);
            Assert.AreEqual(0.35f, r.enemyDamage.minFraction, 1e-5);
            Assert.AreEqual(5, r.stardust.runEnd);
            CollectionAssert.AreEqual(new[] { "swordsman" }, GameDb.StartingClasses);
        }

        [Test]
        public void EnemyDamageHasAFloorAgainstHighDef()
        {
            var rule = new EnemyDamage { minFraction = 0.35f, defCoef = 0.6f };
            Assert.AreEqual(35, Enemy.BaseDamage(100, 1, 200, rule), 1e-4, "floor = atk × 0.35 when def would zero the hit");
            Assert.AreEqual(100 - 0.6 * 20, Enemy.BaseDamage(100, 1, 20, rule), 1e-4, "plain subtraction when above the floor");
            Assert.AreEqual(150 - 12, Enemy.BaseDamage(100, 1.5, 20, rule), 1e-4, "power multiplies atk only");
        }

        [Test]
        public void EveryOfferHasACommonAndAtMostOneEpic()
        {
            var save = new SaveData();
            for (ulong seed = 1; seed <= 300; seed++)
            {
                foreach (var cls in new[] { "swordsman", "mage", "archer" })
                {
                    var run = RunState.New(cls, save, seed);
                    for (int offer = 0; offer < 4; offer++)
                    {
                        var cards = run.DrawPerks(run.Rules.perkChoices, 0.15f);
                        Assert.AreEqual(run.Rules.perkChoices, cards.Count);
                        int commons = cards.FindAll(p => p.rarity == PerkRarity.Common).Count;
                        int epics = cards.FindAll(p => p.rarity == PerkRarity.Epic).Count;
                        Assert.GreaterOrEqual(commons, 1, $"seed {seed} {cls} offer {offer}: {Ids(cards)}");
                        Assert.LessOrEqual(epics, 1, $"seed {seed} {cls} offer {offer}: {Ids(cards)}");
                        run.TakePerk(cards[0]);
                    }
                }
            }
        }

        [Test]
        public void OffersStillDependOnlyOnTheSeed()
        {
            var save = new SaveData();
            var a = RunState.New("swordsman", save, 4242);
            var b = RunState.New("swordsman", save, 4242);
            for (int i = 0; i < 11; i++) _ = a.rng.Value;
            Assert.AreEqual(Ids(a.DrawPerks(3, 0)), Ids(b.DrawPerks(3, 0)));
        }

        [Test]
        public void TheGuaranteedCommonIsNotAlwaysTheFirstCard()
        {
            var save = new SaveData();
            bool commonElsewhere = false;
            for (ulong seed = 1; seed <= 60 && !commonElsewhere; seed++)
            {
                var cards = RunState.New("swordsman", save, seed).DrawPerks(3, 0);
                commonElsewhere = cards[0].rarity != PerkRarity.Common;
            }
            Assert.IsTrue(commonElsewhere);
        }

        [Test]
        public void SkillsUnlockInOrderThenRaiseTheLowest()
        {
            var cls = GameDb.Class("swordsman");
            var world = new World();
            var p = new Player(world, 200, 500, cls, ClassProgress.New(cls));
            var skills = cls.skills;
            int Lv(int i) => p.prog.skillLv.TryGetValue(skills[i].id, out var l) ? l : 0;

            GameController.ApplySkillUnlocks(p, out var fresh);
            Assert.IsNull(fresh, "level 1 already knows skill 1");
            Assert.AreEqual(1, Lv(0));
            for (int i = 1; i < skills.Count; i++) Assert.AreEqual(0, Lv(i), $"skill {i} locked at level 1");

            p.prog.level = 2;
            GameController.ApplySkillUnlocks(p, out fresh);
            Assert.AreEqual(skills[1].id, fresh?.id);
            Assert.AreEqual(1, Lv(1));

            p.prog.level = 4;
            GameController.ApplySkillUnlocks(p, out fresh);
            Assert.AreEqual(skills[2].id, fresh?.id, "the first newly unlocked skill is reported");
            for (int i = 0; i < skills.Count && i < 4; i++) Assert.AreEqual(1, Lv(i));

            p.prog.level = 6;
            GameController.ApplySkillUnlocks(p, out fresh);
            Assert.IsNull(fresh);
            int total = 0; for (int i = 0; i < skills.Count; i++) total += Lv(i);
            Assert.AreEqual(skills.Count + 2, total, "two levels past the last unlock add two skill levels");
            int max = 0, min = 99; for (int i = 0; i < skills.Count; i++) { max = System.Math.Max(max, Lv(i)); min = System.Math.Min(min, Lv(i)); }
            Assert.LessOrEqual(max - min, 1, "points go to the lowest skill first");

            // Calling again at the same level changes nothing.
            GameController.ApplySkillUnlocks(p, out fresh);
            int again = 0; for (int i = 0; i < skills.Count; i++) again += Lv(i);
            Assert.AreEqual(total, again);
        }

        [Test]
        public void NextUnlockLineNamesTheCheapestMissingUnlock()
        {
            var save = new SaveData { stardust = 25 };
            var line = GameController.NextUnlockLine(save);
            Assert.IsTrue(line.Contains("15"), line);
            Assert.IsTrue(line.Contains("旅行藥袋"), line);
            save.stardust = 45;
            Assert.IsTrue(GameController.NextUnlockLine(save).Contains("旅行藥袋"));
            foreach (var u in GameDb.Unlocks) save.unlocks.Add(u.id);
            Assert.IsNull(GameController.NextUnlockLine(save));
        }

        [Test]
        public void RunLogRoomCarriesLevelAndOffer()
        {
            var e = new RoomLogEntry { roomId = "meadow", levelAtClear = 3, offered = new List<string> { "toughBody", "sharpen", "phoenix" }, perk = "sharpen" };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(e);
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<RoomLogEntry>(json);
            Assert.AreEqual(3, back.levelAtClear);
            CollectionAssert.AreEqual(e.offered, back.offered);
        }

        static string Ids(List<PerkDef> l) => string.Join(",", l.ConvertAll(p => p.id));
    }
}
