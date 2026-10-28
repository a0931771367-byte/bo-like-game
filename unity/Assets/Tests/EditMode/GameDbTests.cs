using System.Linq;
using BoLike.Data;
using NUnit.Framework;

namespace BoLike.Tests
{
    public class GameDbTests
    {
        [Test]
        public void LoadsAllTables()
        {
            Assert.AreEqual(6, GameDb.Classes.Count);
            Assert.IsNotEmpty(GameDb.Enemies);
            Assert.IsNotEmpty(GameDb.Stages);
            Assert.NotNull(GameDb.Story("1-1"));
            Assert.NotNull(GameDb.Anim("swordsman"));
        }

        [Test]
        public void SwordsmanMatchesSource()
        {
            var sw = GameDb.Class("swordsman");
            Assert.AreEqual(Weapon.Sword, sw.weapon);
            Assert.AreEqual(3, sw.combo.Count);
            Assert.AreEqual(1.7f, sw.combo[2].power, 1e-5);
            var bash = sw.skills.First(s => s.id == "bash");
            Assert.AreEqual(SkillType.Melee, bash.type);
            Assert.AreEqual(2.2f + 0.45f * 2, bash.power.At(3), 1e-5);
            Assert.IsTrue(bash.stunChance.HasValue);
            Assert.IsTrue(sw.skills.First(s => s.id == "endure").BuffAt(1).endure);
        }

        [Test]
        public void BuffSkillsHaveOneEntryPerLevel()
        {
            foreach (var s in GameDb.Classes.SelectMany(c => c.skills).Where(s => s.type == SkillType.Buff))
                Assert.AreEqual(s.maxLv, s.buffByLevel?.Count, s.id);
        }

        [Test]
        public void EverySpawnedEnemyExists()
        {
            var ids = GameDb.Enemies.Select(e => e.id).ToHashSet();
            foreach (var st in GameDb.Stages)
                foreach (var sp in st.waves.SelectMany(w => w.spawns))
                    Assert.IsTrue(ids.Contains(sp.enemy), $"{st.id}: {sp.enemy}");
        }

        [Test]
        public void ColoursParseAsRgb()
        {
            Assert.AreEqual(0x4a7bd1, GameDb.Class("swordsman").color);
            Assert.AreEqual(0xff8a5a, GameDb.Enemy("fireJelly").tint);
        }

        [Test]
        public void RunDataLoads()
        {
            Assert.AreEqual(15, GameDb.Perks.Count);
            Assert.AreEqual(GameDb.Rules.combatRooms, GameDb.RoomPool.Count);
            Assert.AreEqual(RoomKind.Boss, GameDb.Room(GameDb.BossRoom).kind);
            Assert.AreEqual(3, GameDb.Rules.perkChoices);
            Assert.AreEqual(60f, GameDb.Rules.rarityWeights[PerkRarity.Common]);
            Assert.AreEqual(1.35f, GameDb.Rules.roomScaling.AtkAt(3), 1e-5);
            CollectionAssert.Contains(GameDb.StartingClasses, "swordsman");
        }

        [Test]
        public void PerkEffectsParse()
        {
            var tough = GameDb.Perk("toughBody").effects[0];
            Assert.AreEqual(PerkEffectKind.Mult, tough.kind);
            Assert.AreEqual(MultTarget.MaxHp, tough.target);
            Assert.AreEqual(1.2f, tough.value, 1e-5);
            var phoenix = GameDb.Perk("phoenix").effects[0];
            Assert.AreEqual(PerkEffectKind.Revive, phoenix.kind);
            Assert.AreEqual(1500f, phoenix.invuln);
            Assert.IsTrue(GameDb.Perk("greatSwing").OfferedTo("swordsman"));
            Assert.IsFalse(GameDb.Perk("greatSwing").OfferedTo("mage"));
            Assert.IsTrue(GameDb.Perk("toughBody").OfferedTo("archer"));
        }

        [Test]
        public void RoomsAndUnlocksReferenceRealData()
        {
            var enemies = GameDb.Enemies.Select(e => e.id).ToHashSet();
            foreach (var r in GameDb.Rooms)
                foreach (var sp in r.waveSets.SelectMany(set => set).SelectMany(w => w.spawns))
                    Assert.IsTrue(enemies.Contains(sp.enemy), $"{r.id}: {sp.enemy}");
            foreach (var id in GameDb.RoomPool)
                Assert.AreEqual(RoomKind.Combat, GameDb.Room(id).kind, id);
            foreach (var u in GameDb.Unlocks.Where(u => u.kind == UnlockKind.Class))
                Assert.NotNull(GameDb.Class(u.ValueId), u.id);
            Assert.AreEqual(0.1f, GameDb.Unlocks.First(u => u.kind == UnlockKind.RareChance).ValueNumber, 1e-5);
            foreach (var p in GameDb.Perks)
                foreach (var c in p.classes)
                    Assert.NotNull(GameDb.Class(c), p.id);
        }
    }
}
