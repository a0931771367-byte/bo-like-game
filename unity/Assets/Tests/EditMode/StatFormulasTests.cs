using System.Collections.Generic;
using System.IO;
using BoLike.Data;
using BoLike.Systems;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace BoLike.Tests
{
    /// <summary>Checks the C# formulas against values computed by the TypeScript (stats_vectors.json).</summary>
    public class StatFormulasTests
    {
        class Vectors
        {
            public int maxLevel;
            public List<int> expToNext;
            public List<ElementCase> elementMult;
            public List<DerivedCase> derived;
        }

        class ElementCase { public Element atk, def; public double mult; }
        class DerivedCase { public string classId; public int level; public Stats stats; public Derived expected; }

        static Vectors vectors;

        [OneTimeSetUp]
        public void LoadVectors()
        {
            string path = Path.Combine(Application.dataPath, "Tests/EditMode/stats_vectors.json");
            vectors = JsonConvert.DeserializeObject<Vectors>(File.ReadAllText(path), GameDb.Json);
        }

        [Test]
        public void MaxLevelMatches() => Assert.AreEqual(vectors.maxLevel, StatFormulas.MaxLevel);

        [Test]
        public void ExpToNextMatches()
        {
            for (int i = 0; i < vectors.expToNext.Count; i++)
                Assert.AreEqual(vectors.expToNext[i], StatFormulas.ExpToNext(i + 1), $"level {i + 1}");
        }

        [Test]
        public void ElementMultMatches()
        {
            foreach (var c in vectors.elementMult)
                Assert.AreEqual(c.mult, StatFormulas.ElementMult(c.atk, c.def), 1e-9, $"{c.atk} vs {c.def}");
        }

        [Test]
        public void DeriveMatches()
        {
            Assert.IsNotEmpty(vectors.derived);
            foreach (var c in vectors.derived)
            {
                var d = StatFormulas.Derive(GameDb.Class(c.classId), c.level, c.stats);
                var e = c.expected;
                string at = $"{c.classId} Lv{c.level}";
                Assert.AreEqual(e.maxHp, d.maxHp, 1e-6, at + " maxHp");
                Assert.AreEqual(e.maxSp, d.maxSp, 1e-6, at + " maxSp");
                Assert.AreEqual(e.atk, d.atk, 1e-6, at + " atk");
                Assert.AreEqual(e.matk, d.matk, 1e-6, at + " matk");
                Assert.AreEqual(e.def, d.def, 1e-6, at + " def");
                Assert.AreEqual(e.mdef, d.mdef, 1e-6, at + " mdef");
                Assert.AreEqual(e.crit, d.crit, 1e-6, at + " crit");
                Assert.AreEqual(e.flee, d.flee, 1e-6, at + " flee");
                Assert.AreEqual(e.aspd, d.aspd, 1e-6, at + " aspd");
                Assert.AreEqual(e.move, d.move, 1e-6, at + " move");
                Assert.AreEqual(e.castMult, d.castMult, 1e-6, at + " castMult");
                Assert.AreEqual(e.spRegen, d.spRegen, 1e-6, at + " spRegen");
                Assert.AreEqual(e.hpRegen, d.hpRegen, 1e-6, at + " hpRegen");
            }
        }

        [Test]
        public void AddExpLevelsUpAndGrantsPoints()
        {
            var p = ClassProgress.New(GameDb.Class("swordsman"));
            int gained = StatFormulas.AddExp(p, StatFormulas.ExpToNext(1) + StatFormulas.ExpToNext(2));
            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, p.level);
            Assert.AreEqual(3 + 6, p.statPoints);
            Assert.AreEqual(1 + 2, p.skillPoints);
        }

        [Test]
        public void DamageIsAtLeastOneAndCritsOnlyPhysical()
        {
            var magic = StatFormulas.CalcDamage(10, 10, 100, 999, 999, Element.Neutral, 1, 1, Element.Fire, true, () => 0);
            Assert.AreEqual(1, magic.damage);
            Assert.IsFalse(magic.crit);
            var phys = StatFormulas.CalcDamage(100, 0, 100, 50, 0, Element.Earth, 1, 1, Element.Fire, false, () => 0);
            Assert.IsTrue(phys.crit);
            Assert.AreEqual(1.5, phys.mult);
            Assert.AreEqual((int)System.Math.Floor(100 * 1.5 * 0.9 * 1.5 + 0.5), phys.damage); // crit ignores def
        }
    }
}
