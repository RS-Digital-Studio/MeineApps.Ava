using System;
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Guild
{
    /// <summary>
    /// Statische Definition eines Boss-Typs. Bestimmt HP-Skalierung, Timer und Schadens-Multiplikatoren.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/GuildBoss.cs). init → set (IsExternalInit ist .NET 5+).
    /// Die Firebase-DTOs (FirebaseGuildBoss/GuildBossDamage) und Display-Daten bleiben für die
    /// Netzwerk-/Präsentationsschicht. 6 Boss-Typen.
    /// </summary>
    public class GuildBossDefinition
    {
        public GuildBossType BossType { get; set; }
        public string NameKey { get; set; } = "";
        public string DescKey { get; set; } = "";
        public string Icon { get; set; } = "";

        /// <summary>HP pro Boss-Level.</summary>
        public long HpPerLevel { get; set; }

        /// <summary>Timer-Dauer in Stunden.</summary>
        public int DurationHours { get; set; } = 48;

        public decimal CraftingDamageMultiplier { get; set; } = 1.0m;
        public decimal OrderDamageMultiplier { get; set; } = 1.0m;
        public decimal MiniGameDamageMultiplier { get; set; } = 1.0m;
        public decimal MoneyDonationDamageMultiplier { get; set; } = 1.0m;
        public string Color { get; set; } = "#888888";

        private static readonly List<GuildBossDefinition> _allDefinitions = new List<GuildBossDefinition>
        {
            // Steingolem: Standard-Boss, alle Schadensquellen gleich
            new GuildBossDefinition
            {
                BossType = GuildBossType.StoneGolem,
                NameKey = "GuildBoss_StoneGolem", DescKey = "GuildBossDesc_StoneGolem",
                Icon = "Wall", HpPerLevel = 5_000, DurationHours = 48, Color = "#78716C"
            },
            // Eisentitan: Crafting-Schaden zählt doppelt
            new GuildBossDefinition
            {
                BossType = GuildBossType.IronTitan,
                NameKey = "GuildBoss_IronTitan", DescKey = "GuildBossDesc_IronTitan",
                Icon = "ShieldSword", HpPerLevel = 7_500, DurationHours = 48,
                CraftingDamageMultiplier = 2.0m, Color = "#475569"
            },
            // Meisterarchitekt: Auftrags-Schaden zählt doppelt
            new GuildBossDefinition
            {
                BossType = GuildBossType.MasterArchitect,
                NameKey = "GuildBoss_MasterArchitect", DescKey = "GuildBossDesc_MasterArchitect",
                Icon = "HardHat", HpPerLevel = 6_000, DurationHours = 48,
                OrderDamageMultiplier = 2.0m, Color = "#D97706"
            },
            // Rostdrache: Mini-Game-Schaden zählt doppelt
            new GuildBossDefinition
            {
                BossType = GuildBossType.RustDragon,
                NameKey = "GuildBoss_RustDragon", DescKey = "GuildBossDesc_RustDragon",
                Icon = "Fire", HpPerLevel = 8_000, DurationHours = 48,
                MiniGameDamageMultiplier = 2.0m, Color = "#DC2626"
            },
            // Schattenhändler: Geldspenden zählen dreifach
            new GuildBossDefinition
            {
                BossType = GuildBossType.ShadowTrader,
                NameKey = "GuildBoss_ShadowTrader", DescKey = "GuildBossDesc_ShadowTrader",
                Icon = "Ninja", HpPerLevel = 5_500, DurationHours = 48,
                MoneyDonationDamageMultiplier = 3.0m, Color = "#6D28D9"
            },
            // Uhrwerk-Koloss: Härtester Boss, 24h, alle 1.5x
            new GuildBossDefinition
            {
                BossType = GuildBossType.ClockworkColossus,
                NameKey = "GuildBoss_ClockworkColossus", DescKey = "GuildBossDesc_ClockworkColossus",
                Icon = "CogSync", HpPerLevel = 10_000, DurationHours = 24,
                CraftingDamageMultiplier = 1.5m, OrderDamageMultiplier = 1.5m,
                MiniGameDamageMultiplier = 1.5m, MoneyDonationDamageMultiplier = 1.5m, Color = "#0E7490"
            }
        };

        public static List<GuildBossDefinition> GetAll() => _allDefinitions;

        /// <summary>Berechnet die maximalen HP für ein bestimmtes Boss-Level.</summary>
        public long CalculateHp(int level) => HpPerLevel * Math.Max(1, level);
    }
}
