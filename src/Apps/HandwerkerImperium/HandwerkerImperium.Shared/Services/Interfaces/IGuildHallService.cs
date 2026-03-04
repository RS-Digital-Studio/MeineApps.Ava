using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet das interaktive Gilden-Hauptquartier (10 Gebäude, Upgrades).
/// Jedes Gebäude hat eigene Boni und Upgrade-Stufen.
/// </summary>
public interface IGuildHallService
{
    /// <summary>Lädt alle Gebäude mit aktuellem Level und Upgrade-Status.</summary>
    Task<List<GuildBuildingDisplay>> GetBuildingsAsync();

    /// <summary>Startet ein Gebäude-Upgrade (prüft Kosten und Voraussetzungen).</summary>
    Task<bool> UpgradeBuildingAsync(GuildBuildingId buildingId);

    /// <summary>Prüft ob laufende Gebäude-Upgrades abgeschlossen sind.</summary>
    Task CheckUpgradeCompletionAsync();

    /// <summary>Gibt die gecachten Gebäude-Effekte zurück (kein Firebase-Request).</summary>
    GuildHallEffects GetCachedEffects();

    /// <summary>Aktualisiert den Gebäude-Effekt-Cache von Firebase.</summary>
    Task RefreshHallCacheAsync();

    /// <summary>Gibt das aktuelle Hallen-Level zurück (= Anzahl freigeschalteter Gebäude).</summary>
    int GetHallLevel();
}
