using System.Collections.Generic;
using BoLike.Data;

namespace BoLike.Sim
{
    /// <summary>
    /// Everything the run's perks change about the player, folded into one place so Player only reads
    /// numbers. Rebuilt from the taken perks each time one is picked (see <see cref="Apply"/>).
    /// </summary>
    public class RunMods
    {
        public float maxHp = 1, atk = 1, matk = 1, move = 1, aspd = 1, cooldown = 1, spCost = 1, spRegen = 1, knock = 1, potionHeal = 1;
        /// <summary>Percentage points added to crit chance.</summary>
        public float critAdd;
        /// <summary>Added to the 1.5× crit multiplier.</summary>
        public float critDamageAdd;
        public int skillLvAdd;
        public float onKillHeal;
        public float onKillAoePower, onKillAoeRadius;
        public float roomClearHeal;
        public float reviveFraction, reviveInvuln;
        public bool reviveUsed;
        public float comboFinisherAoeRadius;
        public int projectileAdd;
        public int pierceAdd;

        public static RunMods From(IEnumerable<PerkDef> perks)
        {
            var m = new RunMods();
            foreach (var p in perks) m.Apply(p);
            return m;
        }

        public void Apply(PerkDef perk)
        {
            foreach (var e in perk.effects)
            {
                switch (e.kind)
                {
                    case PerkEffectKind.Mult:
                        switch (e.target)
                        {
                            case MultTarget.MaxHp: maxHp *= e.value; break;
                            case MultTarget.Atk: atk *= e.value; break;
                            case MultTarget.Matk: matk *= e.value; break;
                            case MultTarget.Move: move *= e.value; break;
                            case MultTarget.Aspd: aspd *= e.value; break;
                            case MultTarget.Cooldown: cooldown *= e.value; break;
                            case MultTarget.SpCost: spCost *= e.value; break;
                            case MultTarget.SpRegen: spRegen *= e.value; break;
                            case MultTarget.Knock: knock *= e.value; break;
                            case MultTarget.PotionHeal: potionHeal *= e.value; break;
                        }
                        break;
                    case PerkEffectKind.Crit: critAdd += e.chanceAdd; critDamageAdd += e.damageMultAdd; break;
                    case PerkEffectKind.SkillLvAll: skillLvAdd += e.add; break;
                    case PerkEffectKind.OnKillHeal: onKillHeal += e.fraction; break;
                    case PerkEffectKind.OnKillAoe: onKillAoePower += e.power; onKillAoeRadius = System.Math.Max(onKillAoeRadius, e.radius); break;
                    case PerkEffectKind.RoomClearHeal: roomClearHeal += e.fraction; break;
                    case PerkEffectKind.Potion: break; // granted once when the perk is taken (RunState)
                    case PerkEffectKind.Revive: reviveFraction = System.Math.Max(reviveFraction, e.fraction); reviveInvuln = System.Math.Max(reviveInvuln, e.invuln); break;
                    case PerkEffectKind.ComboFinisherAoe: comboFinisherAoeRadius = System.Math.Max(comboFinisherAoeRadius, e.radius); break;
                    case PerkEffectKind.ProjectileCount: projectileAdd += e.add; break;
                    case PerkEffectKind.Pierce: pierceAdd += e.add; break;
                }
            }
        }
    }

    /// <summary>Gold and potions carried during a run.</summary>
    public class Inventory
    {
        public int gold;
        public int red, blue;
    }
}
