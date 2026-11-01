using System.Linq;
using BoLike.Data;
using BoLike.Sim;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    /// <summary>S3 class talent lines (design doc 08 §2.3): data shape, ordered purchase, free reset and the folded run effects.</summary>
    public class TalentTests
    {
        [Test]
        public void ThreeClassesHaveTenTieredNodesWithThreeKeystones()
        {
            Assert.AreEqual(30, GameDb.Talents.Count);
            foreach (var cid in new[] { "swordsman", "mage", "archer" })
            {
                var line = GameDb.TalentsFor(cid);
                Assert.AreEqual(GameDb.Rules.talents.nodesPerClass, line.Count, cid);
                CollectionAssert.AreEqual(Enumerable.Range(1, 10), line.Select(t => t.tier), cid + " tiers");
                Assert.AreEqual(3, line.Count(t => t.keystone), cid + " keystones");
                foreach (var t in line)
                {
                    Assert.Greater(t.cost, 0, t.id);
                    Assert.IsNotEmpty(t.effects, t.id);
                    Assert.IsNotEmpty(t.name, t.id);
                    Assert.IsTrue(t.id.StartsWith(cid + "."), t.id);
                }
                // Costs rise along the line so the last node is the big purchase.
                for (int i = 1; i < line.Count; i++) Assert.GreaterOrEqual(line[i].cost, line[i - 1].cost, line[i].id);
            }
        }

        [Test]
        public void NodesAreBoughtInOrderAndStardustIsSpent()
        {
            var save = new SaveData { stardust = 100 };
            var line = GameDb.TalentsFor("swordsman");
            Assert.AreEqual("talent.needPrev", Talents.Blocker(save, line[1]));
            Assert.IsFalse(Talents.Buy(save, line[1]));
            Assert.IsNull(Talents.Blocker(save, line[0]));
            Assert.AreEqual(line[0].id, Talents.Next(save, "swordsman").id);
            Assert.IsTrue(Talents.Buy(save, line[0]));
            Assert.AreEqual(70, save.stardust);
            Assert.AreEqual("talent.owned", Talents.Blocker(save, line[0]));
            Assert.IsTrue(Talents.Buy(save, line[1]));
            Assert.AreEqual(30, save.stardust);
            Assert.AreEqual("unlock.notEnough", Talents.Blocker(save, line[2]));
            Assert.AreEqual(line[2].id, Talents.Next(save, "swordsman").id);
            Assert.AreEqual(2, Talents.OwnedFor(save, "swordsman").Count);
            Assert.AreEqual(0, Talents.OwnedFor(save, "mage").Count);
        }

        [Test]
        public void ResetRefundsEverythingSpentOnThatClassOnly()
        {
            var save = new SaveData { stardust = 1000 };
            foreach (var t in GameDb.TalentsFor("mage").Take(3)) Assert.IsTrue(Talents.Buy(save, t));
            Assert.IsTrue(Talents.Buy(save, GameDb.TalentsFor("archer")[0]));
            int before = save.stardust;
            int spent = Talents.Spent(save, "mage");
            Assert.AreEqual(120, spent);
            Assert.AreEqual(spent, Talents.Reset(save, "mage"));
            Assert.AreEqual(before + spent, save.stardust);
            Assert.AreEqual(0, Talents.OwnedFor(save, "mage").Count);
            Assert.AreEqual(1, Talents.OwnedFor(save, "archer").Count);
            Assert.AreEqual(GameDb.TalentsFor("mage")[0].id, Talents.Next(save, "mage").id, "line starts over");
        }

        [Test]
        public void OwnedTalentsFoldIntoRunMods()
        {
            var save = new SaveData { stardust = 10000 };
            foreach (var t in GameDb.TalentsFor("swordsman")) Assert.IsTrue(Talents.Buy(save, t));
            var mods = RunMods.From(new System.Collections.Generic.List<PerkDef>(), Talents.ActiveEffects(save, "swordsman"));
            Assert.AreEqual(1, mods.startSkillPoints, "headStart");
            Assert.Less(mods.guardDamageMult, 0.25f, "ironGuard lowers guard damage");
            Assert.Greater(mods.guardInvulnMs, 0, "unyielding");
            Assert.IsTrue(mods.reflectProjectile, "reflect");
            Assert.Greater(mods.roomStartBuffMs, 0, "warcry");
            Assert.AreEqual(1.1f, mods.maxHp, 0.001f, "vigor");

            var empty = RunMods.From(new System.Collections.Generic.List<PerkDef>(), Talents.ActiveEffects(new SaveData(), "swordsman"));
            Assert.AreEqual(0, empty.startSkillPoints);
            Assert.IsFalse(empty.reflectProjectile);

            var mage = new SaveData { stardust = 10000 };
            foreach (var t in GameDb.TalentsFor("mage")) Assert.IsTrue(Talents.Buy(mage, t));
            var mm = RunMods.From(new System.Collections.Generic.List<PerkDef>(), Talents.ActiveEffects(mage, "mage"));
            Assert.AreEqual(1.12f, mm.skillPower, 0.001f, "arcanePower");
            Assert.AreEqual(0.9f, mm.cooldown, 0.001f, "quickCast");
            Assert.Greater(mm.spEmptyFreeMult, 1f, "overload");
            Assert.Greater(mm.resonanceMult, 1f, "resonance");
        }

        [Test]
        public void EveryTalentEffectKindIsKnownToTheSim()
        {
            // Every kind the data uses must have a fold rule; an unknown kind would silently do nothing in a run.
            foreach (var t in GameDb.Talents)
                foreach (var e in t.effects)
                    Assert.IsTrue(RunMods.Handles(e.kind), $"{t.id}: {e.kind} has no fold rule");
        }
    }
}
