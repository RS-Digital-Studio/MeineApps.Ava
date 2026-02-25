using System.Globalization;

namespace BomberBlast.Models.BattlePass;

/// <summary>
/// Persistenter Battle-Pass-Zustand.
/// Wird als JSON in IPreferencesService gespeichert (Key: "BattlePassData").
/// </summary>
public class BattlePassData
{
    /// <summary>Aktuelle Saison-Nummer (startet bei 1)</summary>
    public int SeasonNumber { get; set; } = 1;

    /// <summary>Aktuelles Tier (0-basiert, 0 = kein Fortschritt, max = MaxTier)</summary>
    public int CurrentTier { get; set; }

    /// <summary>Aktuelle XP im aktuellen Tier</summary>
    public int CurrentXp { get; set; }

    /// <summary>Ob der Spieler den Premium-Pass gekauft hat (für diese Saison)</summary>
    public bool IsPremium { get; set; }

    /// <summary>Start der aktuellen Saison (UTC, ISO 8601)</summary>
    public string SeasonStartDate { get; set; } = DateTime.UtcNow.ToString("O");

    /// <summary>Bereits beanspruchte Free-Tier-Indizes (0-basiert)</summary>
    public List<int> ClaimedFreeTiers { get; set; } = [];

    /// <summary>Bereits beanspruchte Premium-Tier-Indizes (0-basiert)</summary>
    public List<int> ClaimedPremiumTiers { get; set; } = [];

    /// <summary>Ablaufzeitpunkt des 2x XP-Boosts (ISO 8601 UTC, null wenn nicht aktiv)</summary>
    public string? XpBoostExpiresAt { get; set; }

    // === Berechnete Properties (nicht serialisiert) ===

    /// <summary>XP benötigt für das nächste Tier</summary>
    public int XpForNextTier => CurrentTier < BattlePassTierDefinitions.MaxTier
        ? BattlePassTierDefinitions.GetXpForTier(CurrentTier + 1)
        : 0;

    /// <summary>Fortschritt zum nächsten Tier (0.0 - 1.0)</summary>
    public double TierProgress => XpForNextTier > 0
        ? Math.Clamp((double)CurrentXp / XpForNextTier, 0.0, 1.0)
        : 1.0;

    /// <summary>Verbleibende Tage in der Saison</summary>
    public int DaysRemaining
    {
        get
        {
            var startDate = DateTime.Parse(SeasonStartDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return Math.Max(0, BattlePassTierDefinitions.SeasonDurationDays - (int)(DateTime.UtcNow - startDate).TotalDays);
        }
    }

    /// <summary>Ob die Saison abgelaufen ist</summary>
    public bool IsSeasonExpired
    {
        get
        {
            var startDate = DateTime.Parse(SeasonStartDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return (DateTime.UtcNow - startDate).TotalDays > BattlePassTierDefinitions.SeasonDurationDays;
        }
    }

    /// <summary>
    /// Fügt XP hinzu und berechnet Tier-Aufstiege.
    /// Gibt die Anzahl der Tier-Aufstiege zurück.
    /// </summary>
    public int AddXp(int amount)
    {
        if (CurrentTier >= BattlePassTierDefinitions.MaxTier) return 0;
        if (IsSeasonExpired) return 0;

        int tierUps = 0;
        CurrentXp += amount;

        while (CurrentTier < BattlePassTierDefinitions.MaxTier && CurrentXp >= XpForNextTier)
        {
            CurrentXp -= XpForNextTier;
            CurrentTier++;
            tierUps++;
        }

        // XP-Cap wenn Max-Tier erreicht
        if (CurrentTier >= BattlePassTierDefinitions.MaxTier)
            CurrentXp = 0;

        return tierUps;
    }
}
