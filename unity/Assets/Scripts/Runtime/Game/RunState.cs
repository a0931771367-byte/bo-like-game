using System.Collections.Generic;
using BoLike.Core;
using BoLike.Data;
using BoLike.Sim;
using BoLike.Systems;

namespace BoLike.Game
{
    /// <summary>
    /// One Roguelite run: the seed, the drawn room order, the perks taken and the stardust earned so far.
    /// All randomness comes from <see cref="Rng"/> so the same seed replays the same run.
    /// </summary>
    public class RunState
    {
        public ulong seed;
        public Rng rng;
        public string classId;
        public readonly List<string> roomOrder = new();
        public int roomIndex = -1;
        public readonly List<PerkDef> perks = new();
        public readonly Dictionary<string, int> perkStacks = new();
        public int stardustEarned;
        public int roomsCleared;
        public bool bossKilled, abandoned;
        public RunMods mods = new();
        public readonly Inventory inv = new();
        public double startedAt;

        public string SeedCode => Rng.SeedToCode(seed);
        public RunRules Rules => GameDb.Rules;
        public bool InBossRoom => roomIndex >= Rules.combatRooms;
        public string CurrentRoomId => InBossRoom ? GameDb.BossRoom : roomOrder[roomIndex];
        public int CurrentSlot => roomIndex;

        public static RunState New(string classId, SaveData save, ulong? seed = null)
        {
            var r = new RunState { seed = seed ?? Rng.NewSeed(), classId = classId };
            r.rng = new Rng(r.seed);
            var pool = new List<string>(GameDb.RoomPool);
            // Fisher–Yates with the run rng, then take the first combatRooms entries.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = r.rng.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            for (int i = 0; i < r.Rules.combatRooms && i < pool.Count; i++) r.roomOrder.Add(pool[i]);
            r.inv.gold = save.gold;
            r.inv.red = r.Rules.startPotions.red + UnlockValue(save, UnlockKind.StartPotion);
            r.inv.blue = r.Rules.startPotions.blue;
            return r;
        }

        static int UnlockValue(SaveData save, UnlockKind kind)
        {
            int v = 0;
            foreach (var u in GameDb.Unlocks) if (u.kind == kind && save.unlocks.Contains(u.id)) v += (int)u.ValueNumber;
            return v;
        }

        public static float RareChanceBonus(SaveData save)
        {
            float v = 0;
            foreach (var u in GameDb.Unlocks) if (u.kind == UnlockKind.RareChance && save.unlocks.Contains(u.id)) v += u.ValueNumber;
            return v;
        }

        /// <summary>Draws perk choices for this class: weighted by rarity, no duplicates, stacks respected.</summary>
        public List<PerkDef> DrawPerks(int count, float rareBonus)
        {
            var candidates = new List<PerkDef>();
            foreach (var p in GameDb.Perks)
            {
                if (!p.OfferedTo(classId)) continue;
                if (perkStacks.TryGetValue(p.id, out var n) && n >= p.maxStacks) continue;
                candidates.Add(p);
            }
            var weights = Rules.rarityWeights;
            var result = new List<PerkDef>();
            while (result.Count < count && candidates.Count > 0)
            {
                float total = 0;
                foreach (var p in candidates) total += Weight(p, weights, rareBonus);
                float roll = rng.Value * total;
                PerkDef pick = candidates[candidates.Count - 1];
                foreach (var p in candidates)
                {
                    roll -= Weight(p, weights, rareBonus);
                    if (roll <= 0) { pick = p; break; }
                }
                result.Add(pick);
                candidates.Remove(pick);
            }
            return result;
        }

        static float Weight(PerkDef p, Dictionary<PerkRarity, float> w, float rareBonus)
        {
            float v = w.TryGetValue(p.rarity, out var x) ? x : 1;
            if (p.rarity != PerkRarity.Common) v *= 1 + rareBonus;
            return v;
        }

        public void TakePerk(PerkDef p)
        {
            perks.Add(p);
            perkStacks[p.id] = (perkStacks.TryGetValue(p.id, out var n) ? n : 0) + 1;
            mods = RunMods.From(perks);
            foreach (var e in p.effects)
                if (e.kind == PerkEffectKind.Potion) { inv.red += e.red; inv.blue += e.blue; }
        }
    }
}
