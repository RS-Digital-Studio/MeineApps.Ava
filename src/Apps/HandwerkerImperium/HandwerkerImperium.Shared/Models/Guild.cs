using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Minimaler lokaler Cache der Gilden-Mitgliedschaft.
/// Wird in GameState persistiert für Offline-IncomeBonus.
/// Die echten Gilden-Daten leben in Firebase.
/// </summary>
public class GuildMembership
{
    [JsonPropertyName("guildId")]
    public string GuildId { get; set; } = "";

    [JsonPropertyName("guildName")]
    public string GuildName { get; set; } = "";

    [JsonPropertyName("guildLevel")]
    public int GuildLevel { get; set; } = 1;

    [JsonPropertyName("guildIcon")]
    public string GuildIcon { get; set; } = "ShieldHome";

    [JsonPropertyName("guildColor")]
    public string GuildColor { get; set; } = "#D97706";

    /// <summary>
    /// Einkommens-Bonus durch Gilden-Level (+1% pro Level, max 20%).
    /// </summary>
    [JsonIgnore]
    public decimal IncomeBonus => Math.Min(0.20m, GuildLevel * 0.01m);

    // ── Gecachte Gilden-Forschungs-Effekte (werden bei Refresh aktualisiert) ──

    [JsonPropertyName("researchIncomeBonus")]
    public decimal ResearchIncomeBonus { get; set; }

    [JsonPropertyName("researchCostReduction")]
    public decimal ResearchCostReduction { get; set; }

    [JsonPropertyName("researchRewardBonus")]
    public decimal ResearchRewardBonus { get; set; }

    [JsonPropertyName("researchXpBonus")]
    public decimal ResearchXpBonus { get; set; }

    [JsonPropertyName("researchEfficiencyBonus")]
    public decimal ResearchEfficiencyBonus { get; set; }

    [JsonPropertyName("researchMiniGameBonus")]
    public decimal ResearchMiniGameBonus { get; set; }

    [JsonPropertyName("researchMaxMembersBonus")]
    public int ResearchMaxMembersBonus { get; set; }

    [JsonPropertyName("researchOrderSlotBonus")]
    public int ResearchOrderSlotBonus { get; set; }

    [JsonPropertyName("researchOrderQualityBonus")]
    public decimal ResearchOrderQualityBonus { get; set; }

    [JsonPropertyName("researchWorkerSlotBonus")]
    public int ResearchWorkerSlotBonus { get; set; }

    [JsonPropertyName("researchTrainingSpeedBonus")]
    public decimal ResearchTrainingSpeedBonus { get; set; }

    [JsonPropertyName("researchFatigueReduction")]
    public decimal ResearchFatigueReduction { get; set; }

    [JsonPropertyName("researchSpeedBonus")]
    public decimal ResearchSpeedBonus { get; set; }

    [JsonPropertyName("researchPrestigePointBonus")]
    public decimal ResearchPrestigePointBonus { get; set; }

    // ── Neue Properties für Gilden-Überarbeitung ──

    /// <summary>Aktuelles Hallen-Level der Gilde.</summary>
    [JsonPropertyName("guildHallLevel")]
    public int GuildHallLevel { get; set; } = 1;

    /// <summary>Aktuelle Liga-ID: "bronze", "silver", "gold", "diamond".</summary>
    [JsonPropertyName("leagueId")]
    public string LeagueId { get; set; } = "bronze";

    // ── Gecachte Hall-Boni (werden bei Refresh aktualisiert) ──

    [JsonPropertyName("hallCraftingSpeedBonus")]
    public decimal HallCraftingSpeedBonus { get; set; }

    [JsonPropertyName("hallIncomeBonus")]
    public decimal HallIncomeBonus { get; set; }

    [JsonPropertyName("hallOrderRewardBonus")]
    public decimal HallOrderRewardBonus { get; set; }

    [JsonPropertyName("hallWarPointsBonus")]
    public decimal HallWarPointsBonus { get; set; }

    [JsonPropertyName("hallDefenseBonus")]
    public decimal HallDefenseBonus { get; set; }

    [JsonPropertyName("hallEverythingBonus")]
    public decimal HallEverythingBonus { get; set; }

    [JsonPropertyName("hallResearchTimeReduction")]
    public decimal HallResearchTimeReduction { get; set; }

    [JsonPropertyName("hallWeeklyRewardBonus")]
    public decimal HallWeeklyRewardBonus { get; set; }

    [JsonPropertyName("hallMaxMembersBonus")]
    public int HallMaxMembersBonus { get; set; }

    /// <summary>
    /// Aktualisiert alle gecachten Hall-Effekte aus berechneten Effekten.
    /// </summary>
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

    /// <summary>
    /// Aktualisiert alle gecachten Forschungs-Effekte aus berechneten Effekten.
    /// </summary>
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

/// <summary>
/// Anzeige-Daten für eine Gilde in der Browse-Liste.
/// </summary>
public class GuildListItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "ShieldHome";
    public string Color { get; set; } = "#D97706";
    public int Level { get; set; } = 1;
    public int MemberCount { get; set; }
    public int MaxMembers { get; set; } = 20;
    public string Description { get; set; } = "";
    public string LeagueId { get; set; } = "bronze";
    public long WeeklyGoal { get; set; }
    public long WeeklyProgress { get; set; }

    /// <summary>Anzeige-Text für Mitglieder (z.B. "5/20").</summary>
    public string MembersDisplay => $"{MemberCount}/{MaxMembers}";

    public double WeeklyGoalProgress => WeeklyGoal > 0
        ? Math.Clamp((double)WeeklyProgress / WeeklyGoal, 0.0, 1.0) : 0.0;
}

/// <summary>
/// Detail-Daten einer Gilde (inkl. Mitgliederliste).
/// </summary>
public class GuildDetailData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "ShieldHome";
    public string Color { get; set; } = "#D97706";
    public int Level { get; set; } = 1;
    public int MemberCount { get; set; }
    public long WeeklyGoal { get; set; }
    public long WeeklyProgress { get; set; }
    public int TotalWeeksCompleted { get; set; }
    public List<GuildMemberInfo> Members { get; set; } = [];

    public double WeeklyGoalProgress => WeeklyGoal > 0
        ? Math.Clamp((double)WeeklyProgress / WeeklyGoal, 0.0, 1.0) : 0.0;

    public decimal IncomeBonus => Math.Min(0.20m, Level * 0.01m);
}

/// <summary>
/// Anzeige-Daten eines Gilden-Mitglieds.
/// </summary>
public class GuildMemberInfo
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "member";
    public long Contribution { get; set; }
    public int PlayerLevel { get; set; }
    public bool IsCurrentPlayer { get; set; }
}

/// <summary>
/// Firebase-Daten einer Gilden-Einladung.
/// Pfad: /player_invites/{uid}/{guildId}
/// </summary>
public class GuildInvitation
{
    [JsonPropertyName("guildName")]
    public string GuildName { get; set; } = "";

    [JsonPropertyName("guildIcon")]
    public string GuildIcon { get; set; } = "";

    [JsonPropertyName("guildColor")]
    public string GuildColor { get; set; } = "";

    [JsonPropertyName("guildLevel")]
    public int GuildLevel { get; set; }

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("invitedBy")]
    public string InvitedBy { get; set; } = "";

    [JsonPropertyName("invitedAt")]
    public string InvitedAt { get; set; } = "";
}

/// <summary>
/// UI-Anzeige-Daten einer empfangenen Einladung.
/// </summary>
public class GuildInvitationDisplay
{
    public string GuildId { get; set; } = "";
    public string GuildName { get; set; } = "";
    public string GuildIcon { get; set; } = "";
    public string GuildColor { get; set; } = "";
    public int GuildLevel { get; set; }
    public string MemberDisplay { get; set; } = "";
    public string InvitedByDisplay { get; set; } = "";
}
