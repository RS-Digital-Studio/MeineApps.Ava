namespace HandwerkerImperium.Models;

/// <summary>
/// Firebase-Daten eines gildenlosen Spielers der für Einladungen verfügbar ist.
/// Pfad: /available_players/{uid}
/// </summary>
public class AvailablePlayerInfo
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string LastActive { get; set; } = "";
}
