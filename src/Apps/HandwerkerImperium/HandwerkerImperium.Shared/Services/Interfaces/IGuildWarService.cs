namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Anzeige-Daten für den aktuellen Gilden-Krieg (Quick-View im Guild-Hub).
/// Wird aus WarSeasonDisplayData befüllt.
/// </summary>
public class GuildWarDisplayData
{
    public string OwnGuildName { get; set; } = "";
    public string OpponentGuildName { get; set; } = "";
    public long OwnScore { get; set; }
    public long OpponentScore { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}
