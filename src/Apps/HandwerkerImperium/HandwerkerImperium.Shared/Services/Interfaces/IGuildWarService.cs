using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet wöchentliche Gilden-Kriege via Firebase.
/// </summary>
public interface IGuildWarService
{
    /// <summary>Prüft ob ein aktiver Gilden-Krieg existiert und erstellt ggf. einen neuen.</summary>
    Task<GuildWar?> GetOrCreateActiveWarAsync();

    /// <summary>Trägt Punkte zum eigenen Gilden-Score bei.</summary>
    Task ContributeScoreAsync(long points);

    /// <summary>Lädt den aktuellen War-Status.</summary>
    Task<GuildWarDisplayData?> GetWarStatusAsync();

    /// <summary>Prüft ob der Krieg beendet ist und verteilt Belohnungen.</summary>
    Task CheckAndFinalizeWarAsync();
}

/// <summary>
/// Anzeige-Daten für den aktuellen Gilden-Krieg.
/// </summary>
public class GuildWarDisplayData
{
    public string OwnGuildName { get; set; } = "";
    public string OpponentGuildName { get; set; } = "";
    public long OwnScore { get; set; }
    public long OpponentScore { get; set; }
    public long OwnContribution { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool DidWin { get; set; }
}
