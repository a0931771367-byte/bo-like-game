using System.IO;
using BoLike.Data;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    public class SaveSystemTests
    {
        // Export code produced by the Phaser build's exportCode() for a v1 save.
        const string PhaserV1Code =
            "eyJ2ZXJzaW9uIjoxLCJjbGFzc2VzIjp7InN3b3Jkc21hbiI6eyJsZXZlbCI6NywiZXhwIjoxMiwic3RhdHMiOnsic3RyIjo5LCJhZ2kiOjMsInZpdCI6NywiaW50IjoxLCJkZXgiOjMsImx1ayI6MX0sInN0YXRQb2ludHMiOjIsInNraWxsUG9pbnRzIjoxLCJza2lsbEx2Ijp7ImJhc2giOjMsIm1hZ251bSI6MSwicHJvdm9rZSI6MCwiZW5kdXJlIjowfX19LCJnb2xkIjozMjEsInBvdGlvbnMiOnsicmVkIjo0LCJibHVlIjoxfSwiY2xlYXJlZCI6WyIxLTEiLCIxLTIiXX0=";

        string dir;

        [SetUp]
        public void MakeDir() => dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bolike-save-" + System.Guid.NewGuid())).FullName;

        [TearDown]
        public void RemoveDir() => Directory.Delete(dir, true);

        [Test]
        public void ImportsPhaserExportCodeAndMigrates()
        {
            var s = SaveSystem.ImportCode(PhaserV1Code);
            Assert.NotNull(s);
            Assert.AreEqual(SaveData.CurrentVersion, s.version);
            Assert.AreEqual(321, s.gold);
            Assert.AreEqual(4, s.potions.red);
            CollectionAssert.AreEqual(new[] { "1-1", "1-2" }, s.cleared);
            var sw = s.classes["swordsman"];
            Assert.AreEqual(7, sw.level);
            Assert.AreEqual(9, sw.stats.str);
            Assert.AreEqual(3, sw.skillLv["bash"]);
            Assert.AreEqual(0, s.stardust);
            Assert.NotNull(s.unlocks);
        }

        [Test]
        public void RejectsGarbage()
        {
            Assert.IsNull(SaveSystem.ImportCode("not a code"));
            Assert.IsNull(SaveSystem.ImportCode(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"version\":99}"))));
        }

        [Test]
        public void WriteThenLoadRoundTrips()
        {
            string path = Path.Combine(dir, "save.json");
            var s = new SaveData { stardust = 42, gold = 7 };
            s.unlocks.Add("upgrade_fire_split");
            s.ProgressFor(GameDb.Class("mage")).level = 5;
            SaveSystem.WriteTo(path, s);
            SaveSystem.WriteTo(path, s); // second write goes through File.Replace and leaves a .bak

            var back = SaveSystem.LoadFrom(path);
            Assert.AreEqual(42, back.stardust);
            Assert.AreEqual(7, back.gold);
            Assert.AreEqual(5, back.classes["mage"].level);
            CollectionAssert.Contains(back.unlocks, "upgrade_fire_split");
            Assert.IsTrue(File.Exists(path + ".bak"));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void FallsBackToBackupWhenMainFileIsCorrupt()
        {
            string path = Path.Combine(dir, "save.json");
            SaveSystem.WriteTo(path, new SaveData { stardust = 1 });
            SaveSystem.WriteTo(path, new SaveData { stardust = 2 });
            File.WriteAllText(path, "{ truncated");
            Assert.AreEqual(1, SaveSystem.LoadFrom(path).stardust);
        }

        [Test]
        public void ExportCodeRoundTrips()
        {
            var s = new SaveData { stardust = 9 };
            Assert.AreEqual(9, SaveSystem.ImportCode(SaveSystem.ExportCode(s)).stardust);
        }
    }
}
