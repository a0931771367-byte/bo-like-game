using System.IO;
using BoLike.Data;
using BoLike.Game;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    /// <summary>Beta 2 S1: chapter → stage → room pool data (design doc 07 §9) and the progress rules built on it.</summary>
    public class ChapterTests
    {
        [Test]
        public void EveryStageHasAFullPoolAndAValidFinale()
        {
            Assert.AreEqual(3, GameDb.Chapters.Count, "beta 2 ships three chapters");
            foreach (var st in GameDb.RunStages)
            {
                var ch = GameDb.Chapter(st.chapterId);
                Assert.Contains(st.id, ch.stages, $"{st.id} must be listed by its chapter");
                Assert.GreaterOrEqual(st.pool.Count, st.combatRooms, $"{st.id} pool");
                foreach (var id in st.pool)
                {
                    var room = GameDb.Room(id);
                    Assert.AreEqual(RoomKind.Combat, room.kind, id);
                    Assert.AreEqual(st.chapterId, room.scene, $"{id} belongs to another chapter");
                }
                if (st.finale.kind == FinaleKind.Boss)
                {
                    Assert.AreEqual(RoomKind.Boss, GameDb.Room(st.finale.room).kind, st.id);
                    Assert.IsTrue(GameDb.Enemy(st.finale.enemy).boss, $"{st.id} finale enemy is not a boss");
                }
                if (st.scaling != null)
                {
                    Assert.AreEqual(st.combatRooms, st.scaling.hp.Count, $"{st.id} hp slots");
                    Assert.AreEqual(st.combatRooms, st.scaling.atk.Count, $"{st.id} atk slots");
                    Assert.NotNull(st.scaling.boss, $"{st.id} boss multipliers");
                }
            }
        }

        [Test]
        public void ChaptersUnlockByTheBossOfTheChapterBefore()
        {
            var chapters = GameDb.Chapters;
            Assert.IsNull(chapters[0].unlock, "the first chapter is always open");
            Assert.IsTrue(chapters[0].Playable);
            string prevBoss = null;
            for (int i = 0; i < chapters.Count; i++)
            {
                var ch = chapters[i];
                Assert.AreEqual(i + 1, ch.order);
                if (i > 0) Assert.AreEqual(prevBoss, ch.unlock.bossKill, $"{ch.id} should unlock on the previous chapter's boss");
                if (ch.Playable) prevBoss = GameDb.RunStage(ch.stages[ch.stages.Count - 1]).finale.enemy;
                Assert.IsNotEmpty(ch.music, $"{ch.id} music");
            }
        }

        [Test]
        public void ARunDrawsRoomsFromItsStage()
        {
            var save = new SaveData();
            var run = RunState.New("swordsman", save, "mistForest-3", 7);
            var stage = GameDb.RunStage("mistForest-3");
            Assert.AreEqual("mistForest-3", run.stageId);
            Assert.AreEqual("mistForest", run.Chapter.id);
            Assert.AreEqual(stage.combatRooms, run.roomOrder.Count);
            foreach (var id in run.roomOrder) Assert.Contains(id, stage.pool);
            CollectionAssert.AllItemsAreUnique(run.roomOrder);
            run.roomIndex = stage.combatRooms;
            Assert.IsTrue(run.InBossRoom);
            Assert.AreEqual("heartOfTheWood", run.CurrentRoomId);
            Assert.AreEqual("treant", run.BossEnemyId);
            Assert.AreEqual(stage.scaling.boss.hp, run.Scaling.boss.hp, 1e-5);
            Assert.AreEqual(stage.combatRooms + 1, run.waveSetByRoom.Count, "one wave set per room plus the boss room");
        }

        [Test]
        public void WithoutAStageARunPlaysTheFirstChapter()
        {
            var run = RunState.New("swordsman", new SaveData(), 7);
            Assert.AreEqual(GameDb.DefaultStage.id, run.stageId);
            Assert.AreEqual("grassland", run.Chapter.id);
            Assert.AreEqual(GameDb.Rules.roomScaling.boss.hp, run.Scaling.boss.hp, 1e-5, "the first stage keeps the global table's numbers");
            var unknown = RunState.New("swordsman", new SaveData(), "no-such-stage", 7);
            Assert.AreEqual(GameDb.DefaultStage.id, unknown.stageId, "an unknown stage id (old save) falls back instead of throwing");
        }

        [Test]
        public void ProgressOpensTheNextChapterOnAFirstBossKill()
        {
            var save = new SaveData();
            var grass = GameDb.Chapter("grassland");
            var forest = GameDb.Chapter("mistForest");
            Assert.IsTrue(Progress.ChapterUnlocked(save, grass));
            Assert.IsFalse(Progress.ChapterUnlocked(save, forest));
            Assert.AreEqual(1, Progress.PlayableChapters(save).Count);
            Assert.AreEqual("grassland", Progress.DefaultChapter(save).id);

            save.bossFirstKills.Add("jellyKing");
            save.stagesCleared.Add("grassland-3");
            Assert.IsTrue(Progress.ChapterUnlocked(save, forest));
            Assert.IsTrue(Progress.ChapterCleared(save, grass));
            Assert.AreEqual("mistForest", Progress.DefaultChapter(save).id, "preselect the furthest open chapter");
            Assert.AreEqual("mistForest", Progress.ChapterUnlockedBy("jellyKing").id);
            if (!Progress.ChapterReady(GameDb.Chapter("desertRuins")))
                Assert.IsNull(Progress.ChapterUnlockedBy("treant"), "the desert's background is not in the build yet, so a treant kill opens nothing");
            else Assert.AreEqual("desertRuins", Progress.ChapterUnlockedBy("treant").id);
            Assert.AreEqual("mistForest-3", Progress.NextStage(save, forest).id);

            save.lastChapterId = "grassland";
            Assert.AreEqual("grassland", Progress.DefaultChapter(save).id, "the player's last pick wins while it is still open");
            save.lastChapterId = "desertRuins";
            Assert.AreEqual("mistForest", Progress.DefaultChapter(save).id, "a pick that is locked or not ready is ignored");
        }

        // A v2 save as build 14 wrote it (fields of SaveData v2; QA gate D asks for the real device file once pulled).
        const string V2Save = "{\"version\":2,\"classes\":{\"swordsman\":{\"level\":1,\"exp\":0,\"stats\":{\"str\":5,\"agi\":3,\"vit\":5,\"int\":1,\"dex\":3,\"luk\":1},\"statPoints\":0,\"skillPoints\":0,\"skillLv\":{\"bash\":1}}},"
            + "\"gold\":50,\"potions\":{\"red\":3,\"blue\":2},\"cleared\":[],\"stardust\":135,\"unlocks\":[\"class_mage\"],\"runsPlayed\":6,\"bestRoom\":5,\"bossCleared\":true,\"lastSeedCode\":\"K7Q2M9XA\",\"hintsSeen\":[\"hint.move\",\"hint.attack\"]}";

        [Test]
        public void AV2SaveMigratesToV3WithTheFirstBossKillKept()
        {
            var s = SaveSystem.Parse(V2Save);
            Assert.AreEqual(3, SaveData.CurrentVersion);
            Assert.AreEqual(SaveData.CurrentVersion, s.version);
            Assert.AreEqual(135, s.stardust);
            Assert.AreEqual(6, s.runsPlayed);
            CollectionAssert.Contains(s.unlocks, "class_mage");
            CollectionAssert.AreEqual(new[] { "jellyKing" }, s.bossFirstKills);
            CollectionAssert.AreEqual(new[] { "grassland-3" }, s.stagesCleared);
            Assert.IsTrue(s.bossCleared, "the v2 flag stays set for anything that still reads it");
            Assert.IsTrue(Progress.ChapterUnlocked(s, GameDb.Chapter("mistForest")), "a tester who beat the jelly king on build 14 starts beta 2 with the forest open");

            var fresh = SaveSystem.Parse("{\"version\":2,\"bossCleared\":false,\"stardust\":20}");
            Assert.IsEmpty(fresh.bossFirstKills);
            Assert.IsEmpty(fresh.stagesCleared);
        }

        [Test]
        public void MigrationLeavesAV2BackupNextToTheSave()
        {
            string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bolike-v3-" + System.Guid.NewGuid())).FullName;
            try
            {
                string path = Path.Combine(dir, "save.json");
                File.WriteAllText(path, V2Save);
                Assert.AreEqual(2, SaveSystem.VersionOf(V2Save));
                SaveSystem.BackupOlderVersion(path);
                string backup = Path.Combine(dir, "save.v2.json");
                Assert.IsTrue(File.Exists(backup));
                Assert.AreEqual(V2Save, File.ReadAllText(backup), "the backup is the untouched v2 file");
                File.WriteAllText(path, "changed");
                SaveSystem.BackupOlderVersion(path);
                Assert.AreEqual(V2Save, File.ReadAllText(backup), "an existing backup is never overwritten");
                var migrated = SaveSystem.LoadFrom(path.Replace("save.json", "none.json"));
                Assert.IsNull(migrated);
                SaveSystem.WriteTo(path, SaveSystem.Parse(V2Save));
                SaveSystem.BackupOlderVersion(path);
                Assert.AreEqual(1, Directory.GetFiles(dir, "save.v*.json").Length, "a current-version save makes no backup");
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
