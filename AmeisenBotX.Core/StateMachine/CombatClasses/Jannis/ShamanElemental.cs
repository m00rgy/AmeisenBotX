﻿using AmeisenBotX.Core.Character.Comparators;
using AmeisenBotX.Core.Character.Inventory.Enums;
using AmeisenBotX.Core.Data.Enums;
using AmeisenBotX.Core.Data.Objects.WowObject;
using AmeisenBotX.Core.Statemachine.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static AmeisenBotX.Core.Statemachine.Utils.AuraManager;
using static AmeisenBotX.Core.Statemachine.Utils.InterruptManager;

namespace AmeisenBotX.Core.Statemachine.CombatClasses.Jannis
{
    public class ShamanElemental : BasicCombatClass
    {
        // author: Jannis Höschele

#pragma warning disable IDE0051
        private const string ancestralSpiritSpell = "Ancestral Spirit";
        private const string chainLightningSpell = "Chain Lightning";
        private const int deadPartymembersCheckTime = 4;
        private const string elementalMasterySpell = "Elemental Mastery";
        private const string flameShockSpell = "Flame Shock";
        private const string flametongueBuff = "Flametongue ";
        private const string flametongueWeaponSpell = "Flametongue Weapon";
        private const string healingWaveSpell = "Healing Wave";
        private const string heroismSpell = "Heroism";
        private const string hexSpell = "Hex";
        private const string lavaBurstSpell = "Lava Burst";
        private const string lesserHealingWaveSpell = "Lesser Healing Wave";
        private const string lightningBoltSpell = "Lightning Bolt";
        private const string lightningShieldSpell = "Lightning Shield";
        private const string thunderstormSpell = "Thunderstorm";
        private const string waterShieldSpell = "Water Shield";
        private const string windShearSpell = "Wind Shear";
#pragma warning restore IDE0051

        public ShamanElemental(WowInterface wowInterface, AmeisenBotStateMachine stateMachine) : base(wowInterface, stateMachine)
        {
            MyAuraManager.BuffsToKeepActive = new Dictionary<string, CastFunction>();

            TargetAuraManager.DebuffsToKeepActive = new Dictionary<string, CastFunction>()
            {
                { flameShockSpell, () => CastSpellIfPossible(flameShockSpell, WowInterface.ObjectManager.TargetGuid, true) }
            };

            TargetInterruptManager.InterruptSpells = new SortedList<int, CastInterruptFunction>()
            {
                { 0, (x) => CastSpellIfPossible(windShearSpell, x.Guid, true) },
                { 1, (x) => CastSpellIfPossible(hexSpell, x.Guid, true) }
            };
        }

        public override string Author => "Jannis";

        public override WowClass Class => WowClass.Shaman;

        public override Dictionary<string, dynamic> Configureables { get; set; } = new Dictionary<string, dynamic>();

        public override string Description => "FCFS based CombatClass for the Elemental Shaman spec.";

        public override string Displayname => "Shaman Elemental";

        public override bool HandlesMovement => false;

        public override bool HandlesTargetSelection => false;

        public override bool IsMelee => false;

        public override IWowItemComparator ItemComparator { get; set; } = new BasicIntellectComparator(null, new List<WeaponType>() { WeaponType.TWOHANDED_AXES, WeaponType.TWOHANDED_MACES, WeaponType.TWOHANDED_SWORDS });

        public override CombatClassRole Role => CombatClassRole.Dps;

        public override string Version => "1.0";

        private bool HexedTarget { get; set; }

        private DateTime LastDeadPartymembersCheck { get; set; }

        public override void ExecuteCC()
        {
            if (TargetInterruptManager.Tick())
            {
                return;
            }

            if ((!WowInterface.ObjectManager.Player.HasBuffByName(lightningShieldSpell) && WowInterface.ObjectManager.Player.ManaPercentage > 60.0 && CastSpellIfPossible(lightningShieldSpell, 0))
                || !WowInterface.ObjectManager.Player.HasBuffByName(waterShieldSpell) && WowInterface.ObjectManager.Player.ManaPercentage < 20.0 && CastSpellIfPossible(waterShieldSpell, 0))
            {
                return;
            }

            if (WowInterface.ObjectManager.Player.HealthPercentage < 30
                && WowInterface.ObjectManager.Target.Type == WowObjectType.Player
                && CastSpellIfPossible(hexSpell, WowInterface.ObjectManager.TargetGuid, true))
            {
                HexedTarget = true;
                return;
            }

            if (WowInterface.ObjectManager.Player.HealthPercentage < 60
                && CastSpellIfPossible(healingWaveSpell, WowInterface.ObjectManager.PlayerGuid, true))
            {
                return;
            }

            if (WowInterface.ObjectManager.Target != null)
            {
                if ((WowInterface.ObjectManager.Target.Position.GetDistance(WowInterface.ObjectManager.Player.Position) < 6
                        && CastSpellIfPossible(thunderstormSpell, WowInterface.ObjectManager.TargetGuid, true))
                    || (WowInterface.ObjectManager.Target.MaxHealth > 10000000
                        && WowInterface.ObjectManager.Target.HealthPercentage < 25
                        && CastSpellIfPossible(heroismSpell, 0))
                    || CastSpellIfPossible(lavaBurstSpell, WowInterface.ObjectManager.TargetGuid, true)
                    || CastSpellIfPossible(elementalMasterySpell, 0))
                {
                    return;
                }

                if ((WowInterface.ObjectManager.WowObjects.OfType<WowUnit>().Where(e => WowInterface.ObjectManager.Target.Position.GetDistance(e.Position) < 16).Count() > 2 && CastSpellIfPossible(chainLightningSpell, WowInterface.ObjectManager.TargetGuid, true))
                    || CastSpellIfPossible(lightningBoltSpell, WowInterface.ObjectManager.TargetGuid, true))
                {
                    return;
                }
            }
        }

        public override void OutOfCombatExecute()
        {
            if (MyAuraManager.Tick()
                || DateTime.Now - LastDeadPartymembersCheck > TimeSpan.FromSeconds(deadPartymembersCheckTime)
                && HandleDeadPartymembers(ancestralSpiritSpell))
            {
                return;
            }

            if (CheckForWeaponEnchantment(EquipmentSlot.INVSLOT_MAINHAND, flametongueBuff, flametongueWeaponSpell))
            {
                return;
            }

            if (HexedTarget)
            {
                HexedTarget = false;
            }
        }
    }
}