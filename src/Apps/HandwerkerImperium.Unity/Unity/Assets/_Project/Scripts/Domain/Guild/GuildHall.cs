using System;
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Guild
{
    /// <summary>
    /// Kosten für ein Gebäude-Upgrade (Goldschrauben + Gildengeld).
    /// 1:1-Port aus dem Avalonia-Original (Models/GuildHall.cs). Original ist ein positional
    /// record; in Unity/netstandard2.1 fehlt IsExternalInit (.NET 5+) → einfache Klasse mit Ctor.
    /// </summary>
    public sealed class GuildBuildingCost
    {
        /// <summary>Benötigte Goldschrauben.</summary>
        public int GoldenScrews { get; }

        /// <summary>Benötigtes Gildengeld (EUR aus der Gildenkasse).</summary>
        public long GuildMoney { get; }

        public GuildBuildingCost(int goldenScrews, long guildMoney)
        {
            GoldenScrews = goldenScrews;
            GuildMoney = guildMoney;
        }
    }

    /// <summary>
    /// Statische Definition eines Gilden-Gebäudes.
    /// 1:1-Port aus dem Avalonia-Original. init → set (IsExternalInit ist .NET 5+).
    /// UI-Property (Color) bleibt enthalten, da Teil der Definition; 10 Gebäude.
    /// </summary>
    public class GuildBuildingDefinition
    {
        public GuildBuildingId BuildingId { get; set; }
        public string NameKey { get; set; } = "";
        public string DescKey { get; set; } = "";
        public string EffectKey { get; set; } = "";
        public string Icon { get; set; } = "";

        /// <summary>Effekt-Wert pro Level (z.B. 0.02 = +2% pro Level).</summary>
        public decimal EffectPerLevel { get; set; }

        public int MaxLevel { get; set; }

        /// <summary>Ab welchem Hallen-Level dieses Gebäude freigeschaltet wird.</summary>
        public int UnlockHallLevel { get; set; }

        public string Color { get; set; } = "#888888";

        /// <summary>
        /// Berechnet die Upgrade-Kosten für ein bestimmtes Ziel-Level (exponentiell pro Level).
        /// Basis: 10 GS + 500K Gildengeld, Faktor 2.0x (GS) bzw. 2.5x (Geld) pro Level.
        /// </summary>
        public GuildBuildingCost GetUpgradeCost(int targetLevel)
        {
            var screws = (int)(10 * Math.Pow(2.0, targetLevel - 1));
            var money = (long)(500_000 * Math.Pow(2.5, targetLevel - 1));
            return new GuildBuildingCost(screws, money);
        }

        private static readonly List<GuildBuildingDefinition> _allDefinitions = new List<GuildBuildingDefinition>
        {
            // Werkstatt: +2% Crafting-Geschwindigkeit pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.Workshop,
                NameKey = "GuildBuilding_Workshop", DescKey = "GuildBuildingDesc_Workshop",
                EffectKey = "GuildBuildingEffect_CraftingSpeed", Icon = "Hammer",
                EffectPerLevel = 0.02m, MaxLevel = 5, UnlockHallLevel = 1, Color = "#D97706"
            },
            // Forschungslabor: -5% Forschungszeit pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.ResearchLab,
                NameKey = "GuildBuilding_ResearchLab", DescKey = "GuildBuildingDesc_ResearchLab",
                EffectKey = "GuildBuildingEffect_ResearchTime", Icon = "FlaskOutline",
                EffectPerLevel = 0.05m, MaxLevel = 5, UnlockHallLevel = 2, Color = "#2196F3"
            },
            // Handelsposten: +3% Einkommen pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.TradingPost,
                NameKey = "GuildBuilding_TradingPost", DescKey = "GuildBuildingDesc_TradingPost",
                EffectKey = "GuildBuildingEffect_Income", Icon = "StorefrontOutline",
                EffectPerLevel = 0.03m, MaxLevel = 5, UnlockHallLevel = 3, Color = "#4CAF50"
            },
            // Schmiede: +2% Auftragsbelohnung pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.Smithy,
                NameKey = "GuildBuilding_Smithy", DescKey = "GuildBuildingDesc_Smithy",
                EffectKey = "GuildBuildingEffect_OrderReward", Icon = "Anvil",
                EffectPerLevel = 0.02m, MaxLevel = 5, UnlockHallLevel = 4, Color = "#EA580C"
            },
            // Wachturm: +5% Kriegs-Punkte pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.Watchtower,
                NameKey = "GuildBuilding_Watchtower", DescKey = "GuildBuildingDesc_Watchtower",
                EffectKey = "GuildBuildingEffect_WarPoints", Icon = "TowerFire",
                EffectPerLevel = 0.05m, MaxLevel = 5, UnlockHallLevel = 5, Color = "#DC2626"
            },
            // Versammlungshalle: +2 Max-Mitglieder pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.AssemblyHall,
                NameKey = "GuildBuilding_AssemblyHall", DescKey = "GuildBuildingDesc_AssemblyHall",
                EffectKey = "GuildBuildingEffect_MaxMembers", Icon = "AccountGroup",
                EffectPerLevel = 2m, MaxLevel = 3, UnlockHallLevel = 6, Color = "#0E7490"
            },
            // Schatzkammer: +5% Wochenziel-Belohnung pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.Treasury,
                NameKey = "GuildBuilding_Treasury", DescKey = "GuildBuildingDesc_Treasury",
                EffectKey = "GuildBuildingEffect_WeeklyReward", Icon = "TreasureChest",
                EffectPerLevel = 0.05m, MaxLevel = 3, UnlockHallLevel = 7, Color = "#FFD700"
            },
            // Festung: +5% Verteidigungsbonus pro Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.Fortress,
                NameKey = "GuildBuilding_Fortress", DescKey = "GuildBuildingDesc_Fortress",
                EffectKey = "GuildBuildingEffect_Defense", Icon = "ShieldLock",
                EffectPerLevel = 0.05m, MaxLevel = 3, UnlockHallLevel = 8, Color = "#475569"
            },
            // Trophäenhalle: Zeigt Achievements, 1 Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.TrophyHall,
                NameKey = "GuildBuilding_TrophyHall", DescKey = "GuildBuildingDesc_TrophyHall",
                EffectKey = "GuildBuildingEffect_Trophies", Icon = "Trophy",
                EffectPerLevel = 0m, MaxLevel = 1, UnlockHallLevel = 9, Color = "#9C27B0"
            },
            // Meisterthron: +5% auf alles pro Level, 1 Level
            new GuildBuildingDefinition
            {
                BuildingId = GuildBuildingId.MasterThrone,
                NameKey = "GuildBuilding_MasterThrone", DescKey = "GuildBuildingDesc_MasterThrone",
                EffectKey = "GuildBuildingEffect_Everything", Icon = "Crown",
                EffectPerLevel = 0.05m, MaxLevel = 1, UnlockHallLevel = 10, Color = "#B91C1C"
            }
        };

        public static List<GuildBuildingDefinition> GetAll() => _allDefinitions;
    }

    /// <summary>
    /// Berechnete Gesamteffekte aller Gilden-Gebäude. 1:1-Port aus dem Avalonia-Original.
    /// </summary>
    public class GuildHallEffects
    {
        public decimal CraftingSpeedBonus { get; set; }
        public decimal ResearchTimeReduction { get; set; }
        public decimal IncomeBonus { get; set; }
        public decimal OrderRewardBonus { get; set; }
        public decimal WarPointsBonus { get; set; }
        public int MaxMembersBonus { get; set; }
        public decimal WeeklyRewardBonus { get; set; }
        public decimal DefenseBonus { get; set; }
        public decimal EverythingBonus { get; set; }

        /// <summary>Berechnet die Gesamteffekte aus einer Map von Gebäude-Leveln.</summary>
        public static GuildHallEffects Calculate(Dictionary<GuildBuildingId, int> buildingLevels)
        {
            var effects = new GuildHallEffects();

            foreach (var def in GuildBuildingDefinition.GetAll())
            {
                if (!buildingLevels.TryGetValue(def.BuildingId, out var level) || level <= 0)
                    continue;

                var clampedLevel = Math.Min(level, def.MaxLevel);
                var totalEffect = def.EffectPerLevel * clampedLevel;

                switch (def.BuildingId)
                {
                    case GuildBuildingId.Workshop:
                        effects.CraftingSpeedBonus = totalEffect;
                        break;
                    case GuildBuildingId.ResearchLab:
                        effects.ResearchTimeReduction = totalEffect;
                        break;
                    case GuildBuildingId.TradingPost:
                        effects.IncomeBonus = totalEffect;
                        break;
                    case GuildBuildingId.Smithy:
                        effects.OrderRewardBonus = totalEffect;
                        break;
                    case GuildBuildingId.Watchtower:
                        effects.WarPointsBonus = totalEffect;
                        break;
                    case GuildBuildingId.AssemblyHall:
                        effects.MaxMembersBonus = (int)totalEffect;
                        break;
                    case GuildBuildingId.Treasury:
                        effects.WeeklyRewardBonus = totalEffect;
                        break;
                    case GuildBuildingId.Fortress:
                        effects.DefenseBonus = totalEffect;
                        break;
                    case GuildBuildingId.TrophyHall:
                        // Zeigt nur Achievements, kein numerischer Effekt
                        break;
                    case GuildBuildingId.MasterThrone:
                        effects.EverythingBonus = totalEffect;
                        break;
                }
            }

            return effects;
        }
    }
}
