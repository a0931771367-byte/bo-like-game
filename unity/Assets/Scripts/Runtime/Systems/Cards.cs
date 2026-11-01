using System.Collections.Generic;
using BoLike.Core;
using BoLike.Data;

namespace BoLike.Systems
{
    /// <summary>Epic card rules (design doc 08 §2.2): drops on boss kills, shards and stars, equip slots, and the effects a run starts with.</summary>
    public static class Cards
    {
        public enum DropOutcome { New, Shard, StarUp, Nothing }

        public class DropResult
        {
            public CardDef card;
            public DropOutcome outcome;
            public CardProgress progress;
            /// <summary>Shards added by this drop (0 for a new card or a star-up).</summary>
            public int shardsAdded;
        }

        static CardRules Rules => GameDb.Rules.cards ?? new CardRules();

        /// <summary>Equip slots: the base two plus every owned cardSlot unlock, capped at maxSlots.</summary>
        public static int Slots(SaveData save)
        {
            int n = Rules.startSlots;
            foreach (var u in GameDb.Unlocks) if (u.kind == UnlockKind.CardSlot && save.unlocks.Contains(u.id)) n += (int)u.ValueNumber;
            return System.Math.Min(n, Rules.maxSlots);
        }

        public static bool Owned(SaveData save, string id) => save.Card(id) != null;
        public static bool Equipped(SaveData save, string id) => save.equippedCards.Contains(id);

        public static List<CardDef> PoolOf(string bossId)
        {
            var list = new List<CardDef>();
            foreach (var c in GameDb.Cards)
                if (c.boss == bossId && (c.rarity != CardRarity.Legendary || Rules.legendaryEnabled)) list.Add(c);
            return list;
        }

        public static CardDef SignatureOf(string bossId)
        {
            foreach (var c in GameDb.Cards) if (c.boss == bossId && c.signature) return c;
            return null;
        }

        /// <summary>
        /// Boss kill → one card from that boss's pool: the signature on the first kill, otherwise a random pool card.
        /// A duplicate adds a shard; shardsPerStar shards raise a star (max maxStars). Null when the boss has no pool.
        /// </summary>
        public static DropResult Drop(SaveData save, string bossId, bool firstKill, Rng rng)
        {
            var pool = PoolOf(bossId);
            if (pool.Count == 0) return null;
            CardDef card = firstKill && Rules.firstKillSignature ? SignatureOf(bossId) : null;
            card ??= pool[rng.Range(0, pool.Count)];
            return Grant(save, card);
        }

        public static DropResult Grant(SaveData save, CardDef card)
        {
            var r = new DropResult { card = card };
            var owned = save.Card(card.id);
            if (owned == null)
            {
                r.progress = new CardProgress { id = card.id };
                save.cards.Add(r.progress);
                r.outcome = DropOutcome.New;
                return r;
            }
            r.progress = owned;
            if (owned.stars >= Rules.maxStars) { r.outcome = DropOutcome.Nothing; return r; } // maxed: nothing left to gain
            owned.shards += Rules.shardsPerDuplicate;
            r.shardsAdded = Rules.shardsPerDuplicate;
            r.outcome = DropOutcome.Shard;
            if (owned.shards >= Rules.shardsPerStar && owned.stars < Rules.maxStars)
            {
                owned.shards -= Rules.shardsPerStar;
                owned.stars++;
                r.outcome = DropOutcome.StarUp;
            }
            return r;
        }

        /// <summary>Equips a card; false when the slots are full or the card is not owned. Equipping twice is a no-op.</summary>
        public static bool Equip(SaveData save, string id)
        {
            if (!Owned(save, id)) return false;
            if (Equipped(save, id)) return true;
            if (save.equippedCards.Count >= Slots(save)) return false;
            save.equippedCards.Add(id);
            return true;
        }

        public static void Unequip(SaveData save, string id) => save.equippedCards.Remove(id);

        /// <summary>Sets whose boss has at least <c>need</c> cards equipped.</summary>
        public static List<CardSetDef> ActiveSets(SaveData save)
        {
            var list = new List<CardSetDef>();
            foreach (var set in GameDb.CardSets)
            {
                int n = 0;
                foreach (var id in save.equippedCards) if (GameDb.HasCard(id) && GameDb.Card(id).boss == set.boss) n++;
                if (n >= set.need) list.Add(set);
            }
            return list;
        }

        /// <summary>Effects a run starts with from the equipped cards (scaled by stars) and their active sets.</summary>
        public static List<PerkEffect> ActiveEffects(SaveData save)
        {
            var list = new List<PerkEffect>();
            foreach (var id in save.equippedCards)
            {
                if (!GameDb.HasCard(id)) continue; // card removed from data: the save keeps it, the run ignores it
                var card = GameDb.Card(id);
                var prog = save.Card(id);
                foreach (var e in card.effects) list.Add(Scaled(e, prog?.stars ?? 1, card.starScale));
            }
            foreach (var set in ActiveSets(save)) foreach (var e in set.effects) list.Add(e);
            return list;
        }

        /// <summary>A copy of the effect with its numbers scaled by (1 + starScale × (stars − 1)); the effect's kind never changes.</summary>
        public static PerkEffect Scaled(PerkEffect e, int stars, float starScale)
        {
            float k = 1f + starScale * System.Math.Max(0, stars - 1);
            if (k == 1f) return e;
            var c = e.Clone();
            if (c.kind == PerkEffectKind.Mult && c.value > 0) c.value = 1f + (c.value - 1f) * k; else if (c.value > 0) c.value *= k;
            c.fraction *= k; c.power *= k; c.chance = System.Math.Min(1f, c.chance * k); c.knock *= k; c.duration *= k;
            c.chanceAdd *= k; c.damageMultAdd *= k;
            if (c.add > 0) c.add = (int)System.Math.Round(c.add * k);
            if (c.max > 0) c.max = (int)System.Math.Round(c.max * k);
            if (c.landing != null) { c.landing.duration *= k; c.landing.power *= k; }
            return c;
        }

        /// <summary>Owned count / total for the result screen and the card book.</summary>
        public static (int owned, int total) Collection(SaveData save) => (save.cards.Count, GameDb.Cards.Count);
    }
}
