using System.Collections.Generic;
using BoLike.Data;

namespace BoLike.Systems
{
    /// <summary>Class talent lines (design doc 08 §2.3): bought in tier order with stardust, reset for free, applied at run start.</summary>
    public static class Talents
    {
        static TalentRules Rules => GameDb.Rules.talents ?? new TalentRules();

        public static bool Owned(SaveData save, TalentDef t) => save.talents.Contains(t.id);

        public static List<TalentDef> OwnedFor(SaveData save, string classId) => GameDb.TalentsFor(classId).FindAll(t => Owned(save, t));

        /// <summary>The next node to buy on a class's line, or null when the line is complete.</summary>
        public static TalentDef Next(SaveData save, string classId)
        {
            foreach (var t in GameDb.TalentsFor(classId)) if (!Owned(save, t)) return t;
            return null;
        }

        /// <summary>Why a node cannot be bought right now (a strings.json key), or null when it can.</summary>
        public static string Blocker(SaveData save, TalentDef t)
        {
            if (Owned(save, t)) return "talent.owned";
            if (Rules.linear)
                foreach (var prev in GameDb.TalentsFor(t.classId))
                    if (prev.tier < t.tier && !Owned(save, prev)) return "talent.needPrev";
            if (save.stardust < t.cost) return "unlock.notEnough";
            return null;
        }

        public static bool Buy(SaveData save, TalentDef t)
        {
            if (Blocker(save, t) != null) return false;
            save.stardust -= t.cost;
            save.talents.Add(t.id);
            return true;
        }

        public static int Spent(SaveData save, string classId)
        {
            int n = 0;
            foreach (var t in OwnedFor(save, classId)) n += t.cost;
            return n;
        }

        /// <summary>Clears a class's line and refunds every stardust spent on it (罐子 11/1: resets are free).</summary>
        public static int Reset(SaveData save, string classId)
        {
            int refund = Rules.resetFree ? Spent(save, classId) : 0;
            save.talents.RemoveAll(id => GameDb.TalentsFor(classId).Exists(t => t.id == id));
            save.stardust += refund;
            return refund;
        }

        /// <summary>Effects a run of this class starts with.</summary>
        public static List<PerkEffect> ActiveEffects(SaveData save, string classId)
        {
            var list = new List<PerkEffect>();
            foreach (var t in OwnedFor(save, classId)) list.AddRange(t.effects);
            return list;
        }
    }
}
