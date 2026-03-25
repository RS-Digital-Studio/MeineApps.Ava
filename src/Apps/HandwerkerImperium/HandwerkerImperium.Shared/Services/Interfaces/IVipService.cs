using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// VIP-System basierend auf Gesamtausgaben (In-App-Purchases).
/// Bronze ab 4,99 EUR, Silver ab 9,99 EUR, Gold ab 19,99 EUR, Platinum ab 49,99 EUR.
/// </summary>
public interface IVipService
{
    /// <summary>Aktualisiert den VIP-Level basierend auf TotalPurchaseAmount.</summary>
    void RefreshVipLevel();

    /// <summary>Registriert einen Kauf und aktualisiert VIP-Level.</summary>
    void RecordPurchase(decimal amountEur);

    /// <summary>Aktuelle VIP-Stufe.</summary>
    VipTier CurrentTier { get; }

    /// <summary>Einkommens-Bonus (z.B. 0.05 = +5%).</summary>
    decimal IncomeBonus { get; }

    /// <summary>XP-Bonus (z.B. 0.05 = +5%).</summary>
    decimal XpBonus { get; }

    /// <summary>Kosten-Reduktion (z.B. 0.05 = -5%).</summary>
    decimal CostReduction { get; }

    /// <summary>Extra Daily Challenges (Silver+: 1, sonst 0).</summary>
    int ExtraDailyChallenges { get; }

    /// <summary>Extra Weekly Missions (Gold+: 1, sonst 0).</summary>
    int ExtraWeeklyMissions { get; }

    /// <summary>Wird bei VIP-Level-Aenderung gefeuert.</summary>
    event Action? VipLevelChanged;
}
