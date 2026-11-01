using BoLike.Data;
using BoLike.Sim;
using NUnit.Framework;

namespace BoLike.Tests
{
    /// <summary>S2 card zones: sticky blobs root, thorn walls hurt and slow, quicksand only slows; bosses ignore roots.</summary>
    public class ZoneTests
    {
        static World NewWorld()
        {
            var w = new World();
            var cls = GameDb.Class("swordsman");
            w.Player = new Player(w, 200, 500, cls, Systems.ClassProgress.New(cls));
            return w;
        }

        [Test]
        public void StickyBlobRootsAnEnemyInsideButNotABoss()
        {
            var w = NewWorld();
            var jelly = w.SpawnEnemy(GameDb.Enemy("jelly"), 600, 500);
            var king = w.SpawnEnemy(GameDb.Enemy("jellyKing"), 620, 500);
            var far = w.SpawnEnemy(GameDb.Enemy("jelly"), 900, 500);
            w.SpawnZone(new ZoneOpts { x = 600, y = 500, radius = 70, duration = 1200, rootMs = 1200, what = "stickyBlob" });
            w.Step(1f / 60f);
            Assert.IsTrue(jelly.Rooted);
            Assert.IsFalse(king.Rooted, "bosses shrug off roots");
            Assert.IsFalse(far.Rooted);
            Assert.AreEqual(1, w.Zones.Count);
        }

        [Test]
        public void ThornWallHurtsAndSlowsThenExpires()
        {
            var w = NewWorld();
            var wolf = w.SpawnEnemy(GameDb.Enemy("wolf"), 600, 500);
            float hp = wolf.hp;
            w.SpawnZone(new ZoneOpts { owner = w.Player, x = 600, y = 500, radius = 90, duration = 600, slow = 0.5f, damage = 7, what = "thornWall" });
            w.Step(1f / 60f);
            Assert.Less(wolf.hp, hp, "first tick on entry");
            Assert.AreEqual(0.5f, wolf.SpeedMult, 1e-5);
            for (int i = 0; i < 60; i++) w.Step(1f / 60f); // 1 s: the 600 ms zone is gone
            Assert.IsEmpty(w.Zones);
            for (int i = 0; i < 10; i++) w.Step(1f / 60f);
            Assert.AreEqual(1f, wolf.SpeedMult, 1e-5, "the slow fades once the zone is gone");
            Assert.LessOrEqual(hp - wolf.hp, 7 * 2, "350 ms between ticks: at most two ticks in 600 ms");
        }

        [Test]
        public void ZonesAreClearedBetweenRooms()
        {
            var w = NewWorld();
            w.SpawnZone(new ZoneOpts { x = 600, y = 500, radius = 90, duration = 5000, what = "quicksand", slow = 0.5f });
            w.ClearHazards();
            Assert.IsEmpty(w.Zones);
        }
    }
}
