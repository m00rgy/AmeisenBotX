﻿using AmeisenBotX.Core.Data.Objects.Enums;
using AmeisenBotX.Core.Data.Objects.Structs;

namespace AmeisenBotX.Core.Data.Objects
{
    public class WowAura
    {
        public WowAura(RawWowAura rawWowAura, string name)
        {
            RawWowAura = rawWowAura;
            Name = name;
        }

        public ulong CreatorGuid => RawWowAura.Creator;

        public uint Duration => RawWowAura.Duration;

        public uint EndTime => RawWowAura.EndTime;

        public AuraFlags Flags => (AuraFlags)RawWowAura.Flags;

        public bool IsActive => Flags.HasFlag(AuraFlags.Active);

        public bool IsHarmful => Flags.HasFlag(AuraFlags.Harmful);

        public bool IsPassive => Flags.HasFlag(AuraFlags.Passive);

        public byte Level => RawWowAura.Level;

        public string Name { get; private set; }

        public int SpellId => RawWowAura.SpellId;

        public int StackCount => RawWowAura.StackCount;

        private RawWowAura RawWowAura { get; set; }
    }
}