﻿using AmeisenBotX.Core.Character.Spells.Objects;
using AmeisenBotX.Logging;
using AmeisenBotX.Logging.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmeisenBotX.Core.Character.Spells
{
    public class SpellBook
    {
        public SpellBook(WowInterface wowInterface)
        {
            WowInterface = wowInterface;

            Spells = new List<Spell>();
        }

        public delegate void SpellBookUpdate();

        public event SpellBookUpdate OnSpellBookUpdate;

        public List<Spell> Spells { get; private set; }

        private WowInterface WowInterface { get; }

        public Spell GetSpellByName(string spellname)
        {
            return Spells?.FirstOrDefault(e => string.Equals(e.Name, spellname, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsSpellKnown(string spellname)
        {
            return Spells != null && Spells.Any(e => string.Equals(e.Name, spellname, StringComparison.OrdinalIgnoreCase));
        }

        public void Update()
        {
            string rawSpells = WowInterface.HookManager.GetSpells()
                .Replace(".799999237061", ""); // weird druid bug kekw

            try
            {
                Spells = JsonConvert.DeserializeObject<List<Spell>>(rawSpells).OrderBy(e => e.Name).ThenByDescending(e => e.Rank).ToList();
                OnSpellBookUpdate?.Invoke();
            }
            catch (Exception e)
            {
                AmeisenLogger.Instance.Log("CharacterManager", $"Failed to parse Spells JSON:\n{rawSpells}\n{e}", LogLevel.Error);
            }
        }
    }
}