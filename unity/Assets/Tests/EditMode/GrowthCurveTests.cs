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
        public void SkillPointsAreSpentByThePlayerBehindLevelGates()
        {
            var cls = GameDb.Class("swordsman");
            var p = new Player(new World(), 200, 500, cls, ClassProgress.New(cls));
            p.prog.skillPoints = 0;
            int Lv(int i) => GameController.BaseSkillLv(p, cls.skills[i]);

            Assert.AreEqual(1, Lv(0), "skill 1 is known from the start");
            Assert.AreEqual("沒有技能點", GameController.SkillUpBlocker(p, 0));
            Assert.IsFalse(GameController.SpendSkillPoint(p, 0));

            p.prog.skillPoints = 2;
            Assert.AreEqual($"Lv{GameController.SkillGate(1)} 解鎖", GameController.SkillUpBlocker(p, 1), "slot 2 waits for its level gate");
            Assert.IsTrue(GameController.SpendSkillPoint(p, 0));
            Assert.AreEqual(2, Lv(0));
            Assert.AreEqual(1, p.prog.skillPoints);

            p.prog.level = GameController.SkillGate(1);
            Assert.IsNull(GameController.SkillUpBlocker(p, 1));
            Assert.IsTrue(GameController.SpendSkillPoint(p, 1), "learning a skill costs one point");
            Assert.AreEqual(1, Lv(1));
            Assert.AreEqual(0, p.prog.skillPoints);

            p.prog.skillPoints = 10;
            p.prog.skillLv[cls.skills[0].id] = cls.skills[0].maxLv;
            Assert.AreEqual(GameDb.Str("skill.max"), GameController.SkillUpBlocker(p, 0));
        }

        [Test]
        public void EveryClassHasItsOwnDodgeFromData()
        {
            Assert.AreEqual(DodgeKind.Blink, GameDb.Class("mage").Dodge.kind);
            Assert.AreEqual(DodgeKind.Roll, GameDb.Class("archer").Dodge.kind);
            Assert.AreEqual(DodgeKind.ShieldDash, GameDb.Class("swordsman").Dodge.kind);
            Assert.Greater(GameDb.Class("swordsman").Dodge.knock, 0);
            Assert.Greater(GameDb.Class("acolyte").Dodge.onAvoidHeal, 0);
            foreach (var c in GameDb.Classes)
            {
                var d = c.Dodge;
                Assert.IsNotNull(c.dodge, $"{c.id} has no dodge in classes.json");
                Assert.Greater(d.duration, 0, c.id);
                Assert.GreaterOrEqual(d.invuln, 260, $"{c.id}: invulnerability must not drop below the old 260 ms");
                Assert.Greater(d.cooldown, 0, c.id);
            }
            Assert.IsNotEmpty(GameDb.SkillUi.statRows);
            Assert.AreEqual("近戰", GameDb.SkillUi.typeLabel["melee"]);
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
