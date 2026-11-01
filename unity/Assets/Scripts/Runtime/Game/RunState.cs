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
        /// <summary>Combat randomness (damage rolls, spawn y, enemy AI). Separate streams keep room order,
        /// wave sets and perk offers identical for the same seed no matter how the fight went (QA #9).</summary>
        public Rng rng;
        public Rng perkRng;
        /// <summary>Wave set index per room slot, drawn up front from the seed.</summary>
        public readonly List<int> waveSetByRoom = new();
        public string classId;
        /// <summary>The stage (chapter + room pool + finale) this run plays; S1 of beta 2.</summary>
        public string stageId;
        public readonly List<string> roomOrder = new();
        public int roomIndex = -1;
        public readonly List<PerkDef> perks = new();
        public readonly Dictionary<string, int> perkStacks = new();
        public int stardustEarned;
        public int roomsCleared;
        public bool bossKilled, abandoned;
        public RunMods mods = new();
        /// <summary>Account-level effects the run starts with (equipped cards and their sets; talents later), folded under the perks.</summary>
        public readonly List<PerkEffect> baseEffects = new();
        public readonly Inventory inv = new();
        public double startedAt;

        public string SeedCode => Rng.SeedToCode(seed);
        public RunRules Rules => GameDb.Rules;
        public RunStageDef Stage => GameDb.RunStage(stageId);
        public ChapterDef Chapter => GameDb.Chapter(Stage.chapterId);
        public int CombatRooms => Stage.combatRooms > 0 ? Stage.combatRooms : Rules.combatRooms;
        /// <summary>Enemy multipliers for this stage, or the global table when the stage has none.</summary>
        public RoomScaling Scaling => Stage.scaling ?? Rules.roomScaling;
        public string BossEnemyId => Stage.finale?.enemy;
        public bool InBossRoom => roomIndex >= CombatRooms && Stage.HasBossRoom;
        public string CurrentRoomId => InBossRoom ? Stage.finale.room : roomOrder[roomIndex];
        public int CurrentSlot => roomIndex;
        public int CurrentWaveSet => roomIndex < waveSetByRoom.Count ? waveSetByRoom[roomIndex] : 0;

        public static RunState New(string classId, SaveData save, ulong? seed = null) => New(classId, save, null, seed);

        public static RunState New(string classId, SaveData save, string stageId, ulong? seed = null)
        {
            var stage = GameDb.HasRunStage(stageId) ? GameDb.RunStage(stageId) : GameDb.DefaultStage;
            var r = new RunState { seed = seed ?? Rng.NewSeed(), classId = classId, stageId = stage.id };
            var roomRng = new Rng(r.seed ^ 0x526F6F6DUL);
            r.perkRng = new Rng(r.seed ^ 0x5065726BUL);
            r.rng = new Rng(r.seed ^ 0x436F6D62UL);
            var pool = new List<string>(stage.pool);
            // Fisher–Yates with the room rng, then take the first combatRooms entries.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = roomRng.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            for (int i = 0; i < r.CombatRooms && i < pool.Count; i++) r.roomOrder.Add(pool[i]);
            foreach (var id in r.roomOrder) r.waveSetByRoom.Add(roomRng.Range(0, System.Math.Max(1, GameDb.Room(id).waveSets.Count)));
            if (stage.HasBossRoom) r.waveSetByRoom.Add(roomRng.Range(0, System.Math.Max(1, GameDb.Room(stage.finale.room).waveSets.Count)));
            r.baseEffects.AddRange(Cards.ActiveEffects(save));
            r.baseEffects.AddRange(Talents.ActiveEffects(save, classId));
            r.mods = RunMods.From(r.perks, r.baseEffects);
            r.inv.gold = save.gold;
            r.inv.red = r.Rules.startPotions.red + UnlockValue(save, UnlockKind.StartPotion) + r.mods.startRed;
            r.inv.blue = r.Rules.startPotions.blue + r.mods.startBlue;
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

        /// <summary>
        /// Draws perk choices for this class: weighted by rarity, no duplicates, stacks respected, and the offer
        /// rules of growth curve v2 §2: at least <c>perkOffer.minCommon</c> commons and at most
        /// <c>perkOffer.maxEpic</c> epics per offer (an epic over the cap is redrawn as a rare, then as a common).
        /// The cards are shuffled afterwards so the guaranteed common is not always the first card.
        /// </summary>
        public List<PerkDef> DrawPerks(int count, float rareBonus) => DrawPerks(count, rareBonus, Rules.perkOffer ?? new PerkOffer());

        public List<PerkDef> DrawPerks(int count, float rareBonus, PerkOffer offer)
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
            int commons = 0, epics = 0;
            while (result.Count < count && candidates.Count > 0)
            {
                int left = count - result.Count;
                // Fill the common quota first when the remaining slots could not otherwise satisfy it.
                bool needCommon = commons < offer.minCommon && left <= offer.minCommon - commons;
                var pool = needCommon ? candidates.FindAll(p => p.rarity == PerkRarity.Common) : candidates;
                if (pool.Count == 0) pool = candidates;
                var pick = WeightedPick(pool, weights, rareBonus);
                if (pick.rarity == PerkRarity.Epic && epics >= offer.maxEpic)
                {
                    var rares = candidates.FindAll(p => p.rarity == PerkRarity.Rare);
                    if (rares.Count == 0) rares = candidates.FindAll(p => p.rarity == PerkRarity.Common);
                    if (rares.Count > 0) pick = WeightedPick(rares, weights, rareBonus);
                }
                if (pick.rarity == PerkRarity.Common) commons++;
                if (pick.rarity == PerkRarity.Epic) epics++;
                result.Add(pick);
                candidates.Remove(pick);
            }
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = perkRng.Range(0, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        PerkDef WeightedPick(List<PerkDef> pool, Dictionary<PerkRarity, float> weights, float rareBonus)
        {
            float total = 0;
            foreach (var p in pool) total += Weight(p, weights, rareBonus);
            float roll = perkRng.Value * total;
            PerkDef pick = pool[pool.Count - 1];
            foreach (var p in pool)
            {
                roll -= Weight(p, weights, rareBonus);
                if (roll <= 0) { pick = p; break; }
            }
            return pick;
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
            bool reviveUsed = mods.reviveUsed; // one phoenix per run, even after more perks rebuild the mods (QA #10)
            mods = RunMods.From(perks, baseEffects);
            mods.reviveUsed = reviveUsed;
            foreach (var e in p.effects)
                if (e.kind == PerkEffectKind.Potion) { inv.red += e.red; inv.blue += e.blue; }
        }
    }
}
