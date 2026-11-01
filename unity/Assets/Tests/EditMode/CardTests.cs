using System.Linq;
using BoLike.Core;
using BoLike.Data;
using BoLike.Game;
using BoLike.Sim;
using BoLike.Systems;
using NUnit.Framework;

namespace BoLike.Tests
{
    /// <summary>S2 epic cards (design doc 08 §2.2): data integrity, drop rules, stars, slots and the effects a run starts with.</summary>
    public class CardTests
    {
        [Test]
        public void EveryCardBelongsToABossWithOneSignature()
        {
            Assert.AreEqual(12, GameDb.Cards.Count);
            foreach (var boss in GameDb.Cards.Select(c => c.boss).Distinct())
            {
                Assert.IsTrue(GameDb.Enemy(boss).boss, $"{boss} is not a boss");
                Assert.AreEqual(1, GameDb.Cards.Count(c => c.boss == boss && c.signature), $"{boss} needs exactly one signature card");
                Assert.AreEqual(4, Cards.PoolOf(boss).Count, $"{boss} pool");
            }
            foreach (var set in GameDb.CardSets)
                Assert.GreaterOrEqual(GameDb.Cards.Count(c => c.boss == set.boss), set.need, set.name);
            foreach (var c in GameDb.Cards) Assert.IsNotEmpty(c.effects, c.id);
            // The finale boss of every stage has a pool, so every boss kill drops something.
            foreach (var st in GameDb.RunStages)
                if (st.finale.kind == FinaleKind.Boss) Assert.IsNotEmpty(Cards.PoolOf(st.finale.enemy), $"{st.id} boss has no cards");
        }

        [Test]
        public void FirstKillDropsTheSignatureThenDuplicatesBecomeShardsAndStars()
        {
            var save = new SaveData();
            var rng = new Rng(5);
            var first = Cards.Drop(save, "jellyKing", true, rng);
            Assert.AreEqual(Cards.DropOutcome.New, first.outcome);
            Assert.AreEqual("jellyCrown", first.card.id);
            Assert.IsTrue(first.card.signature);
            Assert.AreEqual(1, save.cards.Count);

            // Force duplicates of the signature: shards 1, 2, then a star.
            Assert.AreEqual(Cards.DropOutcome.Shard, Cards.Grant(save, first.card).outcome);
            Assert.AreEqual(Cards.DropOutcome.Shard, Cards.Grant(save, first.card).outcome);
            var up = Cards.Grant(save, first.card);
            Assert.AreEqual(Cards.DropOutcome.StarUp, up.outcome);
            Assert.AreEqual(2, save.Card("jellyCrown").stars);
            Assert.AreEqual(0, save.Card("jellyCrown").shards);
            for (int i = 0; i < 3; i++) Cards.Grant(save, first.card);
            Assert.AreEqual(3, save.Card("jellyCrown").stars, "three more shards reach the cap");
            Assert.AreEqual(Cards.DropOutcome.Nothing, Cards.Grant(save, first.card).outcome, "a maxed card gains nothing more");
            Assert.AreEqual(Cards.DropOutcome.New, Cards.Grant(save, GameDb.Card("ancientRing")).outcome);
            Assert.AreEqual(Cards.DropOutcome.Shard, Cards.Grant(save, GameDb.Card("ancientRing")).outcome, "starScale 0 cards still collect shards");

            // Later kills draw from the whole pool and only ever from that boss.
            for (int i = 0; i < 50; i++)
            {
                var d = Cards.Drop(save, "jellyKing", false, rng);
                Assert.AreEqual("jellyKing", d.card.boss);
            }
            Assert.IsNull(Cards.Drop(save, "wolf", false, rng), "a non-boss has no pool");
        }

        [Test]
        public void SlotsEquipAndSets()
        {
            var save = new SaveData();
            Assert.AreEqual(2, Cards.Slots(save));
            Assert.IsFalse(Cards.Equip(save, "jellyCrown"), "cannot equip a card you do not own");
            Cards.Grant(save, GameDb.Card("jellyCrown"));
            Cards.Grant(save, GameDb.Card("bouncyShell"));
            Cards.Grant(save, GameDb.Card("thornHeart"));
            Assert.IsTrue(Cards.Equip(save, "jellyCrown"));
            Assert.IsTrue(Cards.Equip(save, "bouncyShell"));
            Assert.IsFalse(Cards.Equip(save, "thornHeart"), "two slots to start with");
            Assert.AreEqual(1, Cards.ActiveSets(save).Count, "two jelly king cards light the jelly set");
            Assert.AreEqual("jellyKing", Cards.ActiveSets(save)[0].boss);
            save.unlocks.Add("cardSlot3");
            Assert.AreEqual(3, Cards.Slots(save));
            Assert.IsTrue(Cards.Equip(save, "thornHeart"));
            Cards.Unequip(save, "jellyCrown");
            Assert.IsEmpty(Cards.ActiveSets(save));
            var effects = Cards.ActiveEffects(save);
            Assert.IsTrue(effects.Any(e => e.kind == PerkEffectKind.OnHurtKnock), "bouncy shell");
            Assert.IsTrue(effects.Any(e => e.kind == PerkEffectKind.DodgeOverride && e.landing?.what == "thornWall"), "thorn heart landing");
        }

        [Test]
        public void StarsScaleNumbersButNeverTheKind()
        {
            var e = new PerkEffect { kind = PerkEffectKind.OnHurtKnock, chance = 0.3f, knock = 150 };
            var two = Cards.Scaled(e, 2, 0.2f);
            Assert.AreEqual(PerkEffectKind.OnHurtKnock, two.kind);
            Assert.AreEqual(0.36f, two.chance, 1e-4);
            Assert.AreEqual(180f, two.knock, 1e-4);
            Assert.AreEqual(0.3f, e.chance, 1e-6, "the data object itself is untouched");
            var mult = Cards.Scaled(new PerkEffect { kind = PerkEffectKind.Mult, target = MultTarget.PotionHeal, value = 1.4f }, 3, 0.2f);
            Assert.AreEqual(1f + 0.4f * 1.4f, mult.value, 1e-4, "a multiplier scales its bonus part only");
            Assert.AreSame(e, Cards.Scaled(e, 1, 0.2f), "one star is the data as written");
            Assert.AreSame(e, Cards.Scaled(e, 3, 0f), "starScale 0 cards never change");
        }

        [Test]
        public void EquippedCardsFoldIntoTheRunMods()
        {
            var save = new SaveData();
            Cards.Grant(save, GameDb.Card("sugarRush"));
            Cards.Grant(save, GameDb.Card("ruinTreasure"));
            Cards.Equip(save, "sugarRush");
            Cards.Equip(save, "ruinTreasure");
            var run = RunState.New("swordsman", save, "grassland-3", 3);
            Assert.AreEqual(1.4f, run.mods.potionHeal, 1e-5);
            Assert.AreEqual(GameDb.Rules.startPotions.red + 1, run.inv.red, "糖分補給 adds a red potion at run start");
            Assert.AreEqual(1, run.mods.killStardustAdd);
            Assert.AreEqual(30, run.mods.killStardustMax);
            run.TakePerk(GameDb.Perk("toughBody"));
            Assert.AreEqual(1.4f, run.mods.potionHeal, 1e-5, "taking a perk keeps the card effects");
            var bare = RunState.New("swordsman", new SaveData(), "grassland-3", 3);
            Assert.AreEqual(1f, bare.mods.potionHeal, 1e-5);
            CollectionAssert.AreEqual(bare.roomOrder, run.roomOrder, "cards never change the seed's rooms");
        }
    }
}
