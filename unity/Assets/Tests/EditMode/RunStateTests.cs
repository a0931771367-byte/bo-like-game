using BoLike.Core;
using BoLike.Data;
using BoLike.Game;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    public class RunStateTests
    {
        [Test]
        public void SeedCodeRoundTrips()
        {
            for (int i = 0; i < 200; i++)
            {
                ulong seed = Rng.NewSeed() ^ ((ulong)i * 0x9E3779B97F4A7C15UL & Rng.SeedMask);
                if (seed == 0) seed = 1;
                string code = Rng.SeedToCode(seed);
                Assert.AreEqual(8, code.Length);
                Assert.AreEqual(seed, Rng.CodeToSeed(code), $"code {code}");
            }
            Assert.AreEqual(Rng.CodeToSeed("abc-123x"), Rng.CodeToSeed("ABC123X"), "lower case and separators are tolerated");
        }

        [Test]
        public void SameSeedGivesSameRoomsWaveSetsAndPerks()
        {
            var save = new SaveData();
            var a = RunState.New("swordsman", save, 12345);
            var b = RunState.New("swordsman", save, 12345);
            CollectionAssert.AreEqual(a.roomOrder, b.roomOrder);
            CollectionAssert.AreEqual(a.waveSetByRoom, b.waveSetByRoom);
            Assert.AreEqual(GameDb.Rules.combatRooms + 1, a.waveSetByRoom.Count);

            // Combat consumes the combat stream only; perk offers must not change because of it.
            for (int i = 0; i < 37; i++) _ = a.rng.Value;
            var pa = a.DrawPerks(3, 0);
            var pb = b.DrawPerks(3, 0);
            CollectionAssert.AreEqual(Ids(pa), Ids(pb));
        }

        [Test]
        public void DifferentSeedsDiffer()
        {
            var save = new SaveData();
            bool anyDifferent = false;
            for (ulong s = 1; s < 20 && !anyDifferent; s++)
            {
                var a = RunState.New("swordsman", save, s);
                var b = RunState.New("swordsman", save, s + 1000);
                anyDifferent = string.Join(",", a.roomOrder) != string.Join(",", b.roomOrder) || string.Join(",", Ids(a.DrawPerks(3, 0))) != string.Join(",", Ids(b.DrawPerks(3, 0)));
            }
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void PhoenixReviveIsOncePerRun()
        {
            var save = new SaveData();
            var run = RunState.New("swordsman", save, 7);
            PerkDef revive = null, other = null;
            foreach (var p in GameDb.Perks)
                foreach (var e in p.effects)
                {
                    if (e.kind == PerkEffectKind.Revive) revive ??= p;
                    else if (e.kind == PerkEffectKind.Mult && p.OfferedTo("swordsman")) other ??= p;
                }
            Assert.IsNotNull(revive, "data has a revive perk");
            run.TakePerk(revive);
            Assert.IsFalse(run.mods.reviveUsed);
            run.mods.reviveUsed = true;
            run.TakePerk(other);
            Assert.IsTrue(run.mods.reviveUsed, "taking another perk must not refresh the revive");
        }

        static string[] Ids(System.Collections.Generic.List<PerkDef> l) { var r = new string[l.Count]; for (int i = 0; i < l.Count; i++) r[i] = l[i].id; return r; }
    }
}
