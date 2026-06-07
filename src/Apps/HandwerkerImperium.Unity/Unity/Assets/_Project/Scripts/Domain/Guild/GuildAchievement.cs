using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Guild
{
    /// <summary>
    /// Statische Definition eines Gilden-Achievements.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/GuildAchievement.cs). init → set (IsExternalInit
    /// ist .NET 5+). UI-Methoden (Kategorie-/Tier-Farbe) und Firebase-/Display-DTOs bleiben für die
    /// Netzwerk-/Präsentationsschicht. 30 Achievements (10 Typen × 3 Tiers).
    /// </summary>
    public class GuildAchievementDefinition
    {
        public string Id { get; set; } = "";
        public string NameKey { get; set; } = "";
        public string DescKey { get; set; } = "";
        public string Icon { get; set; } = "";
        public GuildAchievementCategory Category { get; set; }
        public AchievementTier Tier { get; set; }
        public long Target { get; set; }
        public int GoldenScrewReward { get; set; }

        /// <summary>Kosmetik-Belohnung (leer bei Bronze, Banner/Emblem bei Silver/Gold).</summary>
        public string CosmeticReward { get; set; } = "";

        private static readonly List<GuildAchievementDefinition> _allDefinitions = new List<GuildAchievementDefinition>
        {
            // ── Gemeinsam stark: Gildengeld ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_money_bronze", NameKey = "GuildAch_Money_Bronze",
                DescKey = "GuildAchDesc_Money_Bronze", Icon = "CurrencyEur",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
                Target = 100_000, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_money_silver", NameKey = "GuildAch_Money_Silver",
                DescKey = "GuildAchDesc_Money_Silver", Icon = "CurrencyEur",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
                Target = 1_000_000, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_money_gold", NameKey = "GuildAch_Money_Gold",
                DescKey = "GuildAchDesc_Money_Gold", Icon = "CurrencyEur",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
                Target = 10_000_000, GoldenScrewReward = 50
            },

            // ── Gemeinsam stark: Forschungen ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_research_bronze", NameKey = "GuildAch_Research_Bronze",
                DescKey = "GuildAchDesc_Research_Bronze", Icon = "FlaskOutline",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_research_silver", NameKey = "GuildAch_Research_Silver",
                DescKey = "GuildAchDesc_Research_Silver", Icon = "FlaskOutline",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
                Target = 9, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_research_gold", NameKey = "GuildAch_Research_Gold",
                DescKey = "GuildAchDesc_Research_Gold", Icon = "FlaskOutline",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
                Target = 18, GoldenScrewReward = 50
            },

            // ── Gemeinsam stark: Mitglieder ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_members_bronze", NameKey = "GuildAch_Members_Bronze",
                DescKey = "GuildAchDesc_Members_Bronze", Icon = "AccountGroup",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
                Target = 5, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_members_silver", NameKey = "GuildAch_Members_Silver",
                DescKey = "GuildAchDesc_Members_Silver", Icon = "AccountGroup",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
                Target = 10, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_members_gold", NameKey = "GuildAch_Members_Gold",
                DescKey = "GuildAchDesc_Members_Gold", Icon = "AccountGroup",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
                Target = 20, GoldenScrewReward = 50
            },

            // ── Kriegshelden: Kriege gewinnen ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_wars_bronze", NameKey = "GuildAch_Wars_Bronze",
                DescKey = "GuildAchDesc_Wars_Bronze", Icon = "SwordCross",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_wars_silver", NameKey = "GuildAch_Wars_Silver",
                DescKey = "GuildAchDesc_Wars_Silver", Icon = "SwordCross",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
                Target = 10, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_wars_gold", NameKey = "GuildAch_Wars_Gold",
                DescKey = "GuildAchDesc_Wars_Gold", Icon = "SwordCross",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
                Target = 50, GoldenScrewReward = 50
            },

            // ── Kriegshelden: Saisons abschließen ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_seasons_bronze", NameKey = "GuildAch_Seasons_Bronze",
                DescKey = "GuildAchDesc_Seasons_Bronze", Icon = "CalendarStar",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_seasons_silver", NameKey = "GuildAch_Seasons_Silver",
                DescKey = "GuildAchDesc_Seasons_Silver", Icon = "CalendarStar",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
                Target = 4, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_seasons_gold", NameKey = "GuildAch_Seasons_Gold",
                DescKey = "GuildAchDesc_Seasons_Gold", Icon = "CalendarStar",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
                Target = 12, GoldenScrewReward = 50
            },

            // ── Kriegshelden: Liga erreichen ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_league_bronze", NameKey = "GuildAch_League_Bronze",
                DescKey = "GuildAchDesc_League_Bronze", Icon = "MedalOutline",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5 // Silver Liga = 1
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_league_silver", NameKey = "GuildAch_League_Silver",
                DescKey = "GuildAchDesc_League_Silver", Icon = "MedalOutline",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
                Target = 2, GoldenScrewReward = 25 // Gold Liga = 2
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_league_gold", NameKey = "GuildAch_League_Gold",
                DescKey = "GuildAchDesc_League_Gold", Icon = "MedalOutline",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
                Target = 3, GoldenScrewReward = 50 // Diamond Liga = 3
            },

            // ── Drachentöter: Bosse besiegen ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_boss_bronze", NameKey = "GuildAch_Boss_Bronze",
                DescKey = "GuildAchDesc_Boss_Bronze", Icon = "Skull",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_boss_silver", NameKey = "GuildAch_Boss_Silver",
                DescKey = "GuildAchDesc_Boss_Silver", Icon = "Skull",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
                Target = 10, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_boss_gold", NameKey = "GuildAch_Boss_Gold",
                DescKey = "GuildAchDesc_Boss_Gold", Icon = "Skull",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
                Target = 50, GoldenScrewReward = 50
            },

            // ── Drachentöter: Boss-MVP ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_mvp_bronze", NameKey = "GuildAch_Mvp_Bronze",
                DescKey = "GuildAchDesc_Mvp_Bronze", Icon = "StarShooting",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_mvp_silver", NameKey = "GuildAch_Mvp_Silver",
                DescKey = "GuildAchDesc_Mvp_Silver", Icon = "StarShooting",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
                Target = 5, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_mvp_gold", NameKey = "GuildAch_Mvp_Gold",
                DescKey = "GuildAchDesc_Mvp_Gold", Icon = "StarShooting",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
                Target = 20, GoldenScrewReward = 50
            },

            // ── Drachentöter: Boss unter 24h besiegen ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_speedkill_bronze", NameKey = "GuildAch_Speedkill_Bronze",
                DescKey = "GuildAchDesc_Speedkill_Bronze", Icon = "TimerOutline",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_speedkill_silver", NameKey = "GuildAch_Speedkill_Silver",
                DescKey = "GuildAchDesc_Speedkill_Silver", Icon = "TimerOutline",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
                Target = 3, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_speedkill_gold", NameKey = "GuildAch_Speedkill_Gold",
                DescKey = "GuildAchDesc_Speedkill_Gold", Icon = "TimerOutline",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
                Target = 10, GoldenScrewReward = 50
            },

            // ── Baumeister: Gebäude auf Max-Level ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_maxbuilding_bronze", NameKey = "GuildAch_MaxBuilding_Bronze",
                DescKey = "GuildAchDesc_MaxBuilding_Bronze", Icon = "OfficeBuilding",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_maxbuilding_silver", NameKey = "GuildAch_MaxBuilding_Silver",
                DescKey = "GuildAchDesc_MaxBuilding_Silver", Icon = "OfficeBuilding",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Silver,
                Target = 5, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_maxbuilding_gold", NameKey = "GuildAch_MaxBuilding_Gold",
                DescKey = "GuildAchDesc_MaxBuilding_Gold", Icon = "OfficeBuilding",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Gold,
                Target = 10, GoldenScrewReward = 50
            },

            // ── Baumeister: Hallen-Level (Bronze 3, Silver 6, Gold 10) ──
            new GuildAchievementDefinition
            {
                Id = "guild_ach_hall_bronze", NameKey = "GuildAch_Hall_Bronze",
                DescKey = "GuildAchDesc_Hall_Bronze", Icon = "HomeCity",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_hall_silver", NameKey = "GuildAch_Hall_Silver",
                DescKey = "GuildAchDesc_Hall_Silver", Icon = "HomeCity",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Silver,
                Target = 6, GoldenScrewReward = 25
            },
            new GuildAchievementDefinition
            {
                Id = "guild_ach_hall_gold", NameKey = "GuildAch_Hall_Gold",
                DescKey = "GuildAchDesc_Hall_Gold", Icon = "HomeCity",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Gold,
                Target = 10, GoldenScrewReward = 50
            }
        };

        public static List<GuildAchievementDefinition> GetAll() => _allDefinitions;
    }
}
