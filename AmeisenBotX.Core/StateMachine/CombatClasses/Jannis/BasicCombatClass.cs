﻿using AmeisenBotX.Core.Character.Comparators;
using AmeisenBotX.Core.Character.Inventory.Enums;
using AmeisenBotX.Core.Character.Spells.Objects;
using AmeisenBotX.Core.Common;
using AmeisenBotX.Core.Data.Enums;
using AmeisenBotX.Core.Data.Objects.WowObject;
using AmeisenBotX.Core.Movement.Pathfinding.Objects;
using AmeisenBotX.Core.Statemachine.Enums;
using AmeisenBotX.Core.Statemachine.States;
using AmeisenBotX.Core.Statemachine.Utils;
using AmeisenBotX.Core.Statemachine.Utils.TargetSelectionLogic;
using AmeisenBotX.Logging;
using AmeisenBotX.Logging.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static AmeisenBotX.Core.Statemachine.Utils.AuraManager;

namespace AmeisenBotX.Core.Statemachine.CombatClasses.Jannis
{
    public abstract class BasicCombatClass : ICombatClass
    {
        protected BasicCombatClass(WowInterface wowInterface, AmeisenBotStateMachine stateMachine)
        {
            WowInterface = wowInterface;
            StateMachine = stateMachine;

            CooldownManager = new CooldownManager(WowInterface.CharacterManager.SpellBook.Spells);
            RessurrectionTargets = new Dictionary<string, DateTime>();

            ITargetSelectionLogic targetSelectionLogic = Role switch
            {
                CombatClassRole.Dps => targetSelectionLogic = new DpsTargetSelectionLogic(wowInterface),
                CombatClassRole.Heal => targetSelectionLogic = new HealTargetSelectionLogic(wowInterface),
                CombatClassRole.Tank => targetSelectionLogic = new TankTargetSelectionLogic(wowInterface),
                _ => null,
            };

            TargetManager = new TargetManager(targetSelectionLogic, TimeSpan.FromMilliseconds(250));

            Spells = new Dictionary<string, Spell>();
            WowInterface.CharacterManager.SpellBook.OnSpellBookUpdate += () =>
            {
                Spells.Clear();
                foreach (Spell spell in WowInterface.CharacterManager.SpellBook.Spells.OrderBy(e => e.Rank).GroupBy(e => e.Name).Select(e => e.First()))
                {
                    if (!Spells.ContainsKey(spell.Name))
                    {
                        Spells.Add(spell.Name, spell);
                    }
                }
            };

            MyAuraManager = new AuraManager
            (
                null,
                null,
                TimeSpan.FromSeconds(1),
                () => { if (WowInterface.ObjectManager.Player != null) { return WowInterface.ObjectManager.Player.Auras.Select(e => e.Name).ToList(); } else { return null; } },
                () => { if (WowInterface.ObjectManager.Player != null) { return WowInterface.ObjectManager.Player.Auras.Select(e => e.Name).ToList(); } else { return null; } },
                null,
                DispellDebuffsFunction
            );

            TargetAuraManager = new AuraManager
            (
                null,
                null,
                TimeSpan.FromSeconds(1),
                () => { if (WowInterface.ObjectManager.Target != null) { return WowInterface.ObjectManager.Target.Auras.Select(e => e.Name).ToList(); } else { return null; } },
                () => { if (WowInterface.ObjectManager.Target != null) { return WowInterface.ObjectManager.Target.Auras.Select(e => e.Name).ToList(); } else { return null; } },
                DispellBuffsFunction,
                null
            );

            GroupAuraManager = new GroupAuraManager(WowInterface);

            TargetInterruptManager = new InterruptManager(new List<WowUnit>() { WowInterface.ObjectManager.Target }, null);

            ActionEvent = new TimegatedEvent(TimeSpan.FromMilliseconds(50));
            NearInterruptUnitsEvent = new TimegatedEvent(TimeSpan.FromMilliseconds(250));
            UpdatePriorityUnits = new TimegatedEvent(TimeSpan.FromMilliseconds(1000));

            WalkBehindEnemy = false;
        }

        public TimegatedEvent ActionEvent { get; set; }

        public abstract string Author { get; }

        public abstract WowClass Class { get; }

        public abstract Dictionary<string, dynamic> Configureables { get; set; }

        public CooldownManager CooldownManager { get; internal set; }

        public abstract string Description { get; }

        public DispellBuffsFunction DispellBuffsFunction { get; internal set; }

        public DispellDebuffsFunction DispellDebuffsFunction { get; internal set; }

        public abstract string Displayname { get; }

        public abstract bool HandlesMovement { get; }

        public abstract bool HandlesTargetSelection { get; }

        public abstract bool IsMelee { get; }

        public abstract IWowItemComparator ItemComparator { get; set; }

        public AuraManager MyAuraManager { get; internal set; }

        public GroupAuraManager GroupAuraManager { get; internal set; }

        public TimegatedEvent NearInterruptUnitsEvent { get; set; }

        public List<string> PriorityTargets { get => TargetManager.PriorityTargets; set => TargetManager.PriorityTargets = value; }

        public Dictionary<string, DateTime> RessurrectionTargets { get; private set; }

        public abstract CombatClassRole Role { get; }

        public Dictionary<string, Spell> Spells { get; internal set; }

        public AuraManager TargetAuraManager { get; internal set; }

        public InterruptManager TargetInterruptManager { get; internal set; }

        public TargetManager TargetManager { get; internal set; }

        public TimegatedEvent UpdatePriorityUnits { get; set; }

        public bool UseDefaultTargetSelection { get; protected set; } = true;

        public abstract string Version { get; }

        public virtual bool WalkBehindEnemy { get; }

        public WowInterface WowInterface { get; internal set; }

        private AmeisenBotStateMachine StateMachine { get; }

        public void Execute()
        {
            if (!ActionEvent.Run()) { return; }

            if (UpdatePriorityUnits.Run())
            {
                if (StateMachine.CurrentState.Key == BotState.Dungeon
                    && WowInterface.DungeonEngine != null
                    && WowInterface.DungeonEngine.DungeonProfile.PriorityUnits != null
                    && WowInterface.DungeonEngine.DungeonProfile.PriorityUnits.Count > 0)
                {
                    TargetManager.PriorityTargets = WowInterface.DungeonEngine.DungeonProfile.PriorityUnits.ToList();
                }
            }

            // we dont want to do anything if we are casting something...
            if (WowInterface.ObjectManager.Player.IsCasting) { return; }

            if (UseDefaultTargetSelection)
            {
                if (TargetManager.GetUnitToTarget(out List<WowUnit> targetToTarget))
                {
                    ulong guid = targetToTarget.First().Guid;

                    if (WowInterface.ObjectManager.Player.TargetGuid != guid)
                    {
                        WowInterface.HookManager.TargetGuid(guid);
                        WowInterface.ObjectManager.UpdateObject(WowInterface.ObjectManager.Player);
                    }
                }

                if (WowInterface.ObjectManager.Target == null
                    || WowInterface.ObjectManager.Target.IsDead
                    || !BotUtils.IsValidUnit(WowInterface.ObjectManager.Target))
                {
                    return;
                }
            }

            if (NearInterruptUnitsEvent.Run())
            {
                TargetInterruptManager.UnitsToWatch = WowInterface.ObjectManager.GetNearEnemies<WowUnit>(WowInterface.ObjectManager.Player.Position, IsMelee ? 5.0 : 25.0).ToList();
            }

            ExecuteCC();
        }

        public abstract void ExecuteCC();

        public abstract void OutOfCombatExecute();

        public override string ToString()
        {
            return $"[{Class}] [{Role}] {Displayname}";
        }

        protected bool CastSpellIfPossible(string spellName, ulong guid, bool needsResource = false, int currentResourceAmount = 0, bool forceTargetSwitch = false)
        {
            if (!DoIKnowSpell(spellName)) { return false; }

            if (GetValidTarget(guid, out WowUnit target))
            {
                if (currentResourceAmount == 0)
                {
                    currentResourceAmount = WowInterface.ObjectManager.Player.Class switch
                    {
                        WowClass.Deathknight => WowInterface.ObjectManager.Player.Runeenergy,
                        WowClass.Rogue => WowInterface.ObjectManager.Player.Energy,
                        WowClass.Warrior => WowInterface.ObjectManager.Player.Rage,
                        _ => WowInterface.ObjectManager.Player.Mana,
                    };
                }

                bool isTargetMyself = target != null && target.Guid == WowInterface.ObjectManager.PlayerGuid;

                if (Spells[spellName] != null
                    && !CooldownManager.IsSpellOnCooldown(spellName)
                    && (!needsResource || Spells[spellName].Costs < currentResourceAmount)
                    && (target == null || IsInRange(Spells[spellName], target.Position)))
                {
                    HandleTargetSelection(guid, forceTargetSwitch, isTargetMyself);
                    CastSpell(spellName, isTargetMyself);
                    return true;
                }
            }

            return false;
        }

        protected bool CastSpellIfPossibleDk(string spellName, ulong guid, bool needsRuneenergy = false, bool needsBloodrune = false, bool needsFrostrune = false, bool needsUnholyrune = false, bool forceTargetSwitch = false)
        {
            if (!DoIKnowSpell(spellName)) { return false; }

            if (GetValidTarget(guid, out WowUnit target))
            {
                bool isTargetMyself = target != null && target.Guid == WowInterface.ObjectManager.PlayerGuid;

                if (Spells[spellName] != null
                    && !CooldownManager.IsSpellOnCooldown(spellName)
                    && (!needsRuneenergy || Spells[spellName].Costs < WowInterface.ObjectManager.Player.Runeenergy)
                    && (!needsBloodrune || (WowInterface.HookManager.IsRuneReady(0) || WowInterface.HookManager.IsRuneReady(1)))
                    && (!needsFrostrune || (WowInterface.HookManager.IsRuneReady(2) || WowInterface.HookManager.IsRuneReady(3)))
                    && (!needsUnholyrune || (WowInterface.HookManager.IsRuneReady(4) || WowInterface.HookManager.IsRuneReady(5)))
                    && (target == null || IsInRange(Spells[spellName], target.Position)))
                {
                    HandleTargetSelection(guid, forceTargetSwitch, isTargetMyself);
                    CastSpell(spellName, isTargetMyself);
                    return true;
                }
            }

            return false;
        }

        protected bool CastSpellIfPossibleRogue(string spellName, ulong guid, bool needsEnergy = false, bool needsCombopoints = false, int requiredCombopoints = 1, bool forceTargetSwitch = false)
        {
            if (!DoIKnowSpell(spellName)) { return false; }

            if (GetValidTarget(guid, out WowUnit target))
            {
                bool isTargetMyself = target != null && target.Guid == WowInterface.ObjectManager.PlayerGuid;

                if (Spells[spellName] != null
                    && !CooldownManager.IsSpellOnCooldown(spellName)
                    && (!needsEnergy || Spells[spellName].Costs < WowInterface.ObjectManager.Player.Energy)
                    && (!needsCombopoints || WowInterface.ObjectManager.Player.ComboPoints >= requiredCombopoints)
                    && (target == null || IsInRange(Spells[spellName], target.Position)))
                {
                    HandleTargetSelection(guid, forceTargetSwitch, isTargetMyself);
                    CastSpell(spellName, isTargetMyself);
                    return true;
                }
            }

            return false;
        }

        protected bool CheckForWeaponEnchantment(EquipmentSlot slot, string enchantmentName, string spellToCastEnchantment)
        {
            if (WowInterface.CharacterManager.Equipment.Items.ContainsKey(slot))
            {
                int itemId = WowInterface.CharacterManager.Equipment.Items[slot].Id;

                if (itemId > 0)
                {
                    WowItem item = WowInterface.ObjectManager.WowObjects.OfType<WowItem>().FirstOrDefault(e => e.EntryId == itemId);

                    if (item != null
                        && !item.GetEnchantmentStrings().Any(e => e.Contains(enchantmentName))
                        && CastSpellIfPossible(spellToCastEnchantment, 0, true))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected bool HandleDeadPartymembers(string SpellName)
        {
            if (!Spells.ContainsKey(SpellName))
            {
                Spells.Add(SpellName, WowInterface.CharacterManager.SpellBook.GetSpellByName(SpellName));
            }

            if (Spells[SpellName] != null
                && !CooldownManager.IsSpellOnCooldown(SpellName)
                && Spells[SpellName].Costs < WowInterface.ObjectManager.Player.Mana)
            {
                IEnumerable<WowPlayer> players = WowInterface.ObjectManager.WowObjects.OfType<WowPlayer>();
                List<WowPlayer> groupPlayers = players.Where(e => e.IsDead && e.Health == 0 && WowInterface.ObjectManager.PartymemberGuids.Contains(e.Guid)).ToList();

                if (groupPlayers.Count > 0)
                {
                    WowPlayer player = groupPlayers.FirstOrDefault(e => RessurrectionTargets.ContainsKey(e.Name) ? RessurrectionTargets[e.Name] < DateTime.Now : true);

                    if (player != null)
                    {
                        if (!RessurrectionTargets.ContainsKey(player.Name))
                        {
                            RessurrectionTargets.Add(player.Name, DateTime.Now + TimeSpan.FromSeconds(8));
                            return false;
                        }

                        if (RessurrectionTargets[player.Name] < DateTime.Now)
                        {
                            return CastSpellIfPossible(SpellName, player.Guid, true);
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        private bool CastSpell(string spellName, bool castOnSelf)
        {
            bool result = false;
            double cooldown = WowInterface.HookManager.GetSpellCooldown(spellName);

            if (cooldown == 0)
            {
                AmeisenLogger.Instance.Log("CombatClass", $"[{Displayname}]: Casting Spell \"{spellName}\" on \"{WowInterface.ObjectManager.Target?.Name}\"", LogLevel.Verbose);
                WowInterface.HookManager.CastSpell(spellName, castOnSelf);
                cooldown = WowInterface.HookManager.GetSpellCooldown(spellName);
                result = true;
            }

            AmeisenLogger.Instance.Log("CombatClass", $"[{Displayname}]: Spell \"{spellName}\" is on cooldown for \"{cooldown}\"", LogLevel.Verbose);
            CooldownManager.SetSpellCooldown(spellName, (int)cooldown);
            return result;
        }

        private bool DoIKnowSpell(string spellName)
        {
            if (!Spells.ContainsKey(spellName))
            {
                Spell spell = WowInterface.CharacterManager.SpellBook.GetSpellByName(spellName);

                if (spell != null)
                {
                    Spells.Add(spellName, spell);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private bool GetValidTarget(ulong guid, out WowUnit target)
        {
            if (guid == 0)
            {
                target = WowInterface.ObjectManager.Player;
            }
            else
            {
                target = WowInterface.ObjectManager.GetWowObjectByGuid<WowUnit>(guid);
            }

            return target != null;
        }

        private void HandleTargetSelection(ulong guid, bool forceTargetSwitch, bool isTargetMyself)
        {
            if (guid != 0 && WowInterface.ObjectManager.TargetGuid != guid)
            {
                // we dont need to switch target when casting spell on self
                if (forceTargetSwitch || !isTargetMyself)
                {
                    WowInterface.HookManager.TargetGuid(guid);
                }
            }
        }

        private bool IsInRange(Spell spell, Vector3 position)
        {
            if ((spell.MinRange == 0 && spell.MaxRange == 0) || spell.MaxRange == 0)
            {
                return true;
            }

            double distance = WowInterface.ObjectManager.Player.Position.GetDistance(position);
            return distance >= spell.MinRange && distance <= spell.MaxRange;
        }
    }
}