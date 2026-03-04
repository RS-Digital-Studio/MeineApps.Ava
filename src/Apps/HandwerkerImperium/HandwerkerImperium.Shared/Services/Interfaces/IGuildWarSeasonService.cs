using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet das Saison-Ligen-System für Gildenkriege.
/// Ersetzt den alten IGuildWarService mit erweitertem Saison-System.
/// </summary>
public interface IGuildWarSeasonService
{
    /// <summary>Initialisiert den Service und lädt die aktuelle Saison.</summary>
    Task InitializeAsync();

    /// <summary>Lädt die aufbereiteten Daten des aktuellen Kriegs/der Saison.</summary>
    Task<WarSeasonDisplayData?> GetCurrentWarDataAsync();

    /// <summary>Trägt Punkte zum eigenen Kriegs-Score bei.</summary>
    Task ContributeScoreAsync(long points, string source);

    /// <summary>Lädt die letzten Kriegs-Log-Einträge.</summary>
    Task<List<GuildWarLogEntry>> GetWarLogAsync(int limit = 50);

    /// <summary>Lädt die Bonus-Missionen des aktuellen Kriegs.</summary>
    Task<List<WarBonusMission>> GetBonusMissionsAsync();

    /// <summary>Prüft ob ein Phasenwechsel fällig ist (Attack→Defense→Evaluation→Completed).</summary>
    Task CheckPhaseTransitionAsync();

    /// <summary>Prüft ob die aktuelle Saison beendet ist und ggf. eine neue startet.</summary>
    Task CheckSeasonEndAsync();

    /// <summary>Gibt die aktuelle Liga der eigenen Gilde zurück.</summary>
    GuildLeague GetCurrentLeague();
}
