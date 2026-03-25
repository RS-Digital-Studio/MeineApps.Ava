using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für das Ascension-System (Meta-Prestige).
/// Freigeschaltet nach 3x Legende-Prestige.
/// Resettet Prestige-Daten komplett, gibt Ascension-Punkte für permanente Perks.
/// </summary>
public interface IAscensionService
{
    /// <summary>Ob Ascension verfügbar ist (3+ Legende-Prestiges).</summary>
    bool CanAscend { get; }

    /// <summary>Berechnet die AP die bei nächster Ascension erhalten werden.</summary>
    int CalculateAscensionPoints();

    /// <summary>Führt die Ascension durch. Gibt false zurück wenn Voraussetzungen nicht erfüllt.</summary>
    Task<bool> DoAscension();

    /// <summary>Upgraded einen Perk um eine Stufe. Gibt false zurück wenn nicht genug AP oder max Level.</summary>
    bool UpgradePerk(string perkId);

    /// <summary>Alle 6 Perks mit aktuellem Level und Kosten.</summary>
    IReadOnlyList<AscensionPerk> GetAllPerks();

    /// <summary>Aktueller Effektwert eines Perks (0 wenn nicht gekauft).</summary>
    decimal GetPerkValue(string perkId);

    /// <summary>Golden-Era-Perk: Bonus auf GS-Verdienst (z.B. 0.2 = +20%).</summary>
    decimal GetGoldenScrewBonus();

    /// <summary>Timeless-Research-Perk: Bonus auf Research-Speed (z.B. 0.1 = -10% Dauer).</summary>
    decimal GetResearchSpeedBonus();

    /// <summary>Legendary-Reputation-Perk: Start-Reputation nach Prestige (Default 50).</summary>
    int GetStartReputation();

    /// <summary>Quick-Start-Perk: Anzahl sofort freigeschalteter Workshops (0-8).</summary>
    int GetQuickStartWorkshops();

    /// <summary>Eternal-Tools-Perk: Level (0-5), bestimmt wie viele Meisterwerkzeuge erhalten bleiben.</summary>
    int GetEternalToolsLevel();

    /// <summary>Start-Capital-Perk: Multiplikator auf Prestige-Startgeld (z.B. 2.0 = +200%).</summary>
    decimal GetStartCapitalMultiplier();

    /// <summary>Wird nach erfolgreicher Ascension gefeuert.</summary>
    event EventHandler? AscensionCompleted;
}
