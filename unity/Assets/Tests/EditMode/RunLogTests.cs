using System.IO;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    public class RunLogTests
    {
        [Test]
        public void ReadAllSkipsBrokenLinesAndKeepsRooms()
        {
            string path = Path.Combine(Path.GetTempPath(), "bolike-runlog-test.jsonl");
            File.WriteAllText(path,
                "{\"classId\":\"swordsman\",\"result\":\"win\",\"totalMs\":231000,\"rooms\":[{\"roomId\":\"meadow\",\"clearMs\":42000,\"hpAtClear\":80,\"maxHp\":120}]}\n" +
                "not json\n" +
                "{\"classId\":\"mage\",\"result\":\"lose\",\"rooms\":[]}\n");
            var runs = RunLog.ReadAll(path);
            File.Delete(path);
            Assert.AreEqual(2, runs.Count);
            Assert.AreEqual("win", runs[0].result);
            Assert.AreEqual(1, runs[0].rooms.Count);
            Assert.AreEqual(42000, runs[0].rooms[0].clearMs);
            Assert.AreEqual("mage", runs[1].classId);
        }

        [Test]
        public void FormatMsIsMinutesAndSeconds()
        {
            Assert.AreEqual("3:51", RunLog.FormatMs(231000));
            Assert.AreEqual("0:05", RunLog.FormatMs(5400));
        }
    }
}
