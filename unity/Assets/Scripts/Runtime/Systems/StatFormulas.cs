using System;
using BoLike.Data;

namespace BoLike.Systems
{
    public struct Derived
    {
        public double maxHp, maxSp, atk, matk, def, mdef, crit, flee, aspd, move, castMult, spRegen, hpRegen;
    }

    public struct DamageResult
    {
        public int damage;
        public bool crit;
        public double mult;
    }

    /// <summary>
    /// Port of src/systems/Stats.ts. Kept in doubles with JS rounding so results match the Phaser build
    /// exactly; Tests/EditMode/StatFormulasTests checks this against values exported from the TypeScript.
    /// </summary>
    public static class StatFormulas
    {
        public const int MaxLevel = 50;

        /// <summary>JavaScript Math.round (halves round up), unlike Math.Round's banker's rounding.</summary>
        public static double JsRound(double x) => Math.Floor(x + 0.5);

        public static int ExpToNext(int level) => (int)Math.Floor(28 * Math.Pow(level, 1.65));

        public static Derived Derive(ClassDef cls, int level, Stats s)
        {
            int L = level;
            return new Derived
            {
                maxHp = JsRound(cls.hpBase + cls.hpPerLv * (L - 1) + s.vit * 12),
                maxSp = JsRound(cls.spBase + cls.spPerLv * (L - 1) + s.@int * 4),
                atk = cls.weaponAtk + s.str * 2 + s.dex / 3 + s.luk / 5 + L,
                matk = s.@int * 3 + (s.@int * s.@int) / 20 + L,
                def = s.vit + L / 2,
                mdef = s.@int / 2 + s.vit / 4,
                crit = 1 + s.luk * 0.35,
                flee = Math.Min(35, s.agi * 0.5 + s.luk * 0.15),
                aspd = 1 + s.agi * 0.012 + s.dex * 0.003,
                move = 230 + s.agi * 1.2,
                castMult = 1 - Math.Min(0.5, s.dex * 0.012),
                spRegen = 1 + s.@int * 0.18,
                hpRegen = s.vit * 0.12,
            };
        }

        public static Derived Derive(ClassDef cls, ClassProgress p) => Derive(cls, p.level, p.stats);

        /// <summary>Adds exp, returns the number of levels gained.</summary>
        public static int AddExp(ClassProgress p, int amount)
        {
            int gained = 0;
            p.exp += amount;
            while (p.level < MaxLevel && p.exp >= ExpToNext(p.level))
            {
                p.exp -= ExpToNext(p.level);
                p.level++;
                p.statPoints += 3;
                p.skillPoints += 1;
                gained++;
            }
            if (p.level >= MaxLevel) p.exp = 0;
            return gained;
        }

        static Element? Strong(Element e) => e switch
        {
            Element.Fire => Element.Earth,
            Element.Water => Element.Fire,
            Element.Wind => Element.Water,
            Element.Earth => Element.Wind,
            Element.Holy => Element.Dark,
            Element.Dark => Element.Holy,
            _ => null,
        };

        public static double ElementMult(Element atk, Element def)
        {
            if (atk == Element.Neutral || def == Element.Neutral) return 1;
            if (Strong(atk) == def) return atk == Element.Holy || atk == Element.Dark ? 1.75 : 1.5;
            if (Strong(def) == atk) return 0.75;
            if (atk == def) return 0.5;
            return 1;
        }

        /// <param name="rng">Returns a value in [0, 1); pass the run's seeded RNG so fights are reproducible.</param>
        public static DamageResult CalcDamage(double atk, double matk, double critChance,
            double def, double mdef, Element defElement, double defMult,
            double power, Element element, bool magic, Func<double> rng)
        {
            bool crit = !magic && rng() * 100 < critChance;
            double dmg = (magic ? matk : atk) * power;
            if (!crit) dmg -= (magic ? mdef : def) * defMult;
            double mult = ElementMult(element, defElement);
            dmg = Math.Max(1, dmg) * mult * (0.9 + rng() * 0.2);
            if (crit) dmg *= 1.5;
            return new DamageResult { damage = (int)Math.Max(1, JsRound(dmg)), crit = crit, mult = mult };
        }
    }
}
