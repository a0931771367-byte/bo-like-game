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
    }
}
