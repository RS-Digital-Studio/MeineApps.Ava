using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet das Ascension-System (Meta-Prestige).
/// Freigeschaltet nach 3x Legende Prestige.
/// </summary>
public interface IAscensionService
{
    /// <summary>Prüft ob Ascension verfügbar ist (3x Legende erforderlich).</summary>
    bool CanAscend();

    /// <summary>Führt eine Ascension durch: Prestige-Zähler reset, Ascension-Punkte vergeben.</summary>
    Task<bool> DoAscension();

    /// <summary>Kauft/upgraded einen Ascension-Perk.</summary>
    bool BuyPerk(string perkId);

    /// <summary>Gibt alle Perks mit aktuellem Level zurück.</summary>
    List<(AscensionPerk Perk, int CurrentLevel)> GetPerksWithLevels();

    /// <summary>Startgeld-Multiplikator aus Ascension-Perks.</summary>
    decimal GetStartCapitalMultiplier();

    /// <summary>Research-Dauer-Reduktion (0.0-0.5).</summary>
    decimal GetResearchDurationReduction();

    /// <summary>Goldschrauben-Bonus-Multiplikator.</summary>
    decimal GetGoldenScrewBonus();

    /// <summary>Start-Reputation (Standard: 50).</summary>
    int GetStartReputation();

    /// <summary>Anzahl Start-Workshops (Standard: 1).</summary>
    int GetStartWorkshopCount();
}
