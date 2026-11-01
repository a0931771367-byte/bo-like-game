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

        // ---- beta 2 (cards / talents / perks 16–30) ----
        public int startRed, startBlue;
        public int startSkillPoints;
        /// <summary>Multiplies every skill's power (mage 魔力增幅) and every stardust payout (遺跡 cards).</summary>
        public float skillPower = 1f, stardust = 1f;
        public int killStardustAdd, killStardustMax;
        public bool firstHitImmune;
        public float idleRegenAfter, idleRegenFraction;
        public float onHurtKnockChance, onHurtKnock;
        public float lowHpShieldFraction, lowHpShieldDuration, lowHpShieldCooldown, lowHpShieldAura;
        /// <summary>Damage taken while guarding is multiplied by this (perk/talent guardMult lowers it).</summary>
        public float guardDamageMult = 0.25f;
        public float dodgeInvulnAdd, dodgeDistanceMult = 1f, dodgeCooldownAdd;
        public float roomStartBuffMs, roomStartBuffMult = 1f;
        public float roomStartSpFraction, roomStartHiddenMs;
        public float onKillSp;
        public float killStackPerKill, killStackMax;
        public float cooldownResetChance;
        public float counterWindow, counterMult = 1f;
        public float guardInvulnFraction, guardInvulnMs, guardInvulnCooldown;
        public float spEmptyFreeMult;
        public float resonanceMult, resonanceWindow;
        public bool reflectProjectile, reflectOnGuard;
        /// <summary>Every effect whose kind has no folded field yet (gameplay kinds read the list directly).</summary>
        public readonly List<PerkEffect> raw = new();

        /// <summary>Kinds some system acts on (folded here or read from <see cref="raw"/> by Player/RunState). Data tests
        /// use it so a new kind cannot ship silently doing nothing; SummonAlly and ComboExtraHit are still pending.</summary>
        public static bool Handles(PerkEffectKind k) => k != PerkEffectKind.SummonAlly && k != PerkEffectKind.ComboExtraHit;

        public static RunMods From(IEnumerable<PerkDef> perks) => From(perks, null);

        /// <summary>Folds the run's perks and the account-level effects (equipped cards, talents) into one table.</summary>
        public static RunMods From(IEnumerable<PerkDef> perks, IEnumerable<PerkEffect> extra)
        {
            var m = new RunMods();
            if (extra != null) foreach (var e in extra) m.Apply(e);
            foreach (var p in perks) m.Apply(p);
            return m;
        }

        public void Apply(PerkDef perk)
        {
            foreach (var e in perk.effects) Apply(e);
        }

        public void Apply(PerkEffect e)
        {
            raw.Add(e);
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
                            case MultTarget.SkillPower: skillPower *= e.value; break;
                            case MultTarget.Stardust: stardust *= e.value; break;
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
                    case PerkEffectKind.StartPotion: startRed += e.red; startBlue += e.blue; break;
                    case PerkEffectKind.GuardMult: guardDamageMult = System.Math.Min(guardDamageMult, e.value); break;
                    case PerkEffectKind.Dodge: dodgeInvulnAdd += e.invulnAdd; if (e.distanceMult > 0) dodgeDistanceMult *= e.distanceMult; dodgeCooldownAdd += e.cooldownAdd; break;
                    case PerkEffectKind.RoomStartBuff: roomStartBuffMs = System.Math.Max(roomStartBuffMs, e.duration); roomStartBuffMult *= e.mult > 0 ? e.mult : 1f; break;
                    case PerkEffectKind.RoomStart: roomStartSpFraction = System.Math.Max(roomStartSpFraction, e.spFraction); roomStartHiddenMs = System.Math.Max(roomStartHiddenMs, e.hiddenMs); break;
                    case PerkEffectKind.OnKillSp: onKillSp += e.fraction; break;
                    case PerkEffectKind.KillStackAtk: killStackPerKill += e.perKill; killStackMax += e.max; break;
                    case PerkEffectKind.CooldownReset: cooldownResetChance = System.Math.Min(1f, cooldownResetChance + e.chance); break;
                    case PerkEffectKind.Counter: counterWindow = System.Math.Max(counterWindow, e.window); counterMult = System.Math.Max(counterMult, e.mult); break;
                    case PerkEffectKind.GuardInvuln: guardInvulnFraction = System.Math.Max(guardInvulnFraction, e.hpFraction); guardInvulnMs = System.Math.Max(guardInvulnMs, e.invuln); guardInvulnCooldown = guardInvulnCooldown > 0 ? System.Math.Min(guardInvulnCooldown, e.cooldown) : e.cooldown; break;
                    case PerkEffectKind.SpEmptyFree: spEmptyFreeMult = System.Math.Max(spEmptyFreeMult, e.mult); break;
                    case PerkEffectKind.ElementResonance: resonanceMult = System.Math.Max(resonanceMult, e.mult); resonanceWindow = System.Math.Max(resonanceWindow, e.window); break;
                    case PerkEffectKind.ReflectProjectile: reflectProjectile = true; reflectOnGuard |= e.onGuard ?? false; break;
                    case PerkEffectKind.StartSkillPoints: startSkillPoints += e.add; break;
                    case PerkEffectKind.KillStardust: killStardustAdd += e.add; killStardustMax += e.max; break;
                    case PerkEffectKind.FirstHitImmune: firstHitImmune = true; break;
                    case PerkEffectKind.IdleRegen: idleRegenAfter = idleRegenAfter > 0 ? System.Math.Min(idleRegenAfter, e.after) : e.after; idleRegenFraction += e.fraction; break;
                    case PerkEffectKind.OnHurtKnock: onHurtKnockChance = System.Math.Min(1f, onHurtKnockChance + e.chance); onHurtKnock = System.Math.Max(onHurtKnock, e.knock); break;
                    case PerkEffectKind.LowHpShield:
                        lowHpShieldFraction = System.Math.Max(lowHpShieldFraction, e.hpFraction);
                        lowHpShieldDuration = System.Math.Max(lowHpShieldDuration, e.duration);
                        lowHpShieldCooldown = lowHpShieldCooldown > 0 ? System.Math.Min(lowHpShieldCooldown, e.cooldown) : e.cooldown;
                        lowHpShieldAura = System.Math.Max(lowHpShieldAura, e.auraPower);
                        break;
                    default: break; // remaining beta-2 kinds are read from `raw` by the systems that implement them
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
