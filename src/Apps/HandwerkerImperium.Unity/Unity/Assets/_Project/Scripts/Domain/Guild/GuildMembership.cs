using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Guild
{
    /// <summary>
    /// Minimaler lokaler Cache der Gilden-Mitgliedschaft. Wird im GameState persistiert für
    /// Offline-IncomeBonus; die echten Gilden-Daten leben in Firebase.
    /// 1:1-Port aus dem Avalonia-Original (Models/Guild.cs). Die Guild-Display-DTOs (GuildListItem,
    /// GuildDetailData, …) und Firebase-Einladungen bleiben für die Netzwerk-/Präsentationsschicht.
    /// GuildHallEffects/GuildResearchEffects sind aus Schicht 9. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class GuildMembership
    {
        [JsonProperty("guildId")]
        public string GuildId { get; set; } = "";

        [JsonProperty("guildName")]
        public string GuildName { get; set; } = "";

        [JsonProperty("guildLevel")]
        public int GuildLevel { get; set; } = 1;

        [JsonProperty("guildIcon")]
        public string GuildIcon { get; set; } = "ShieldHome";

        [JsonProperty("guildColor")]
        public string GuildColor { get; set; } = "#D97706";

        /// <summary>Einkommens-Bonus durch Gilden-Level (+1% pro Level, max 20%).</summary>
        [JsonIgnore]
        public decimal IncomeBonus => Math.Min(0.20m, GuildLevel * 0.01m);

        // ── Gecachte Gilden-Forschungs-Effekte ──
        [JsonProperty("researchIncomeBonus")] public decimal ResearchIncomeBonus { get; set; }
        [JsonProperty("researchCostReduction")] public decimal ResearchCostReduction { get; set; }
        [JsonProperty("researchRewardBonus")] public decimal ResearchRewardBonus { get; set; }
        [JsonProperty("researchXpBonus")] public decimal ResearchXpBonus { get; set; }
        [JsonProperty("researchEfficiencyBonus")] public decimal ResearchEfficiencyBonus { get; set; }
        [JsonProperty("researchMiniGameBonus")] public decimal ResearchMiniGameBonus { get; set; }
        [JsonProperty("researchMaxMembersBonus")] public int ResearchMaxMembersBonus { get; set; }
        [JsonProperty("researchOrderSlotBonus")] public int ResearchOrderSlotBonus { get; set; }
        [JsonProperty("researchOrderQualityBonus")] public decimal ResearchOrderQualityBonus { get; set; }
        [JsonProperty("researchWorkerSlotBonus")] public int ResearchWorkerSlotBonus { get; set; }
        [JsonProperty("researchTrainingSpeedBonus")] public decimal ResearchTrainingSpeedBonus { get; set; }
        [JsonProperty("researchFatigueReduction")] public decimal ResearchFatigueReduction { get; set; }

        /// <summary>Zeitstempel des letzten Gilden-Tab-Besuchs (ISO 8601 UTC) für das Tab-Badge.</summary>
        [JsonProperty("lastTabVisitIso")]
        public string LastTabVisitIso { get; set; } = "";

        [JsonProperty("researchSpeedBonus")] public decimal ResearchSpeedBonus { get; set; }
        [JsonProperty("researchPrestigePointBonus")] public decimal ResearchPrestigePointBonus { get; set; }

        /// <summary>Aktuelles Hallen-Level der Gilde.</summary>
        [JsonProperty("guildHallLevel")]
        public int GuildHallLevel { get; set; } = 1;

        /// <summary>Aktuelle Liga-ID: "bronze", "silver", "gold", "diamond".</summary>
        [JsonProperty("leagueId")]
        public string LeagueId { get; set; } = "bronze";

        // ── Gecachte Hall-Boni ──
        [JsonProperty("hallCraftingSpeedBonus")] public decimal HallCraftingSpeedBonus { get; set; }
        [JsonProperty("hallIncomeBonus")] public decimal HallIncomeBonus { get; set; }
        [JsonProperty("hallOrderRewardBonus")] public decimal HallOrderRewardBonus { get; set; }
        [JsonProperty("hallWarPointsBonus")] public decimal HallWarPointsBonus { get; set; }
        [JsonProperty("hallDefenseBonus")] public decimal HallDefenseBonus { get; set; }
        [JsonProperty("hallEverythingBonus")] public decimal HallEverythingBonus { get; set; }
        [JsonProperty("hallResearchTimeReduction")] public decimal HallResearchTimeReduction { get; set; }
        [JsonProperty("hallWeeklyRewardBonus")] public decimal HallWeeklyRewardBonus { get; set; }
        [JsonProperty("hallMaxMembersBonus")] public int HallMaxMembersBonus { get; set; }

        // ── Mega-Projekt-Boni ──
        [JsonProperty("megaProjectCraftingSpeedBonus")] public decimal MegaProjectCraftingSpeedBonus { get; set; }
        [JsonProperty("megaProjectAutoSellPriceBonus")] public decimal MegaProjectAutoSellPriceBonus { get; set; }
        [JsonProperty("megaProjectBonusWarehouseSlots")] public int MegaProjectBonusWarehouseSlots { get; set; }

        /// <summary>Liste der abgeschlossenen Mega-Projekt-Typen (verhindert Doppel-Belohnung).</summary>
        [JsonProperty("completedMegaProjectTypes")]
        public List<int> CompletedMegaProjectTypes { get; set; } = new List<int>();

        /// <summary>Aktualisiert alle gecachten Hall-Effekte aus berechneten Effekten.</summary>
        public void ApplyHallEffects(GuildHallEffects effects)
        {
            HallCraftingSpeedBonus = effects.CraftingSpeedBonus;
            HallIncomeBonus = effects.IncomeBonus;
            HallOrderRewardBonus = effects.OrderRewardBonus;
            HallWarPointsBonus = effects.WarPointsBonus;
            HallDefenseBonus = effects.DefenseBonus;
            HallEverythingBonus = effects.EverythingBonus;
            HallResearchTimeReduction = effects.ResearchTimeReduction;
            HallWeeklyRewardBonus = effects.WeeklyRewardBonus;
            HallMaxMembersBonus = effects.MaxMembersBonus;
        }

        /// <summary>Aktualisiert alle gecachten Forschungs-Effekte aus berechneten Effekten.</summary>
        public void ApplyResearchEffects(GuildResearchEffects effects)
        {
            ResearchIncomeBonus = effects.IncomeBonus;
            ResearchCostReduction = effects.CostReduction;
            ResearchRewardBonus = effects.RewardBonus;
            ResearchXpBonus = effects.XpBonus;
            ResearchEfficiencyBonus = effects.EfficiencyBonus;
            ResearchMiniGameBonus = effects.MiniGameBonus;
            ResearchMaxMembersBonus = effects.MaxMembersBonus;
            ResearchOrderSlotBonus = effects.OrderSlotBonus;
            ResearchOrderQualityBonus = effects.OrderQualityBonus;
            ResearchWorkerSlotBonus = effects.WorkerSlotBonus;
            ResearchTrainingSpeedBonus = effects.TrainingSpeedBonus;
            ResearchFatigueReduction = effects.FatigueReduction;
            ResearchSpeedBonus = effects.ResearchSpeedBonus;
            ResearchPrestigePointBonus = effects.PrestigePointBonus;
        }
    }
}
