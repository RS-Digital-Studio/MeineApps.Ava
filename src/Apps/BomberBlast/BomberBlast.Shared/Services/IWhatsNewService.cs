namespace BomberBlast.Services;

/// <summary>
/// "What's New"-Service. Sammelt Eintraege pro Versions-Bump kumulativ — auch fuer
/// nicht released Zwischenversionen. Beim Store-Update sieht der Spieler alle
/// Eintraege seit seiner zuletzt installierten Version (nicht nur die aktuelle).
/// Damit gehen lange Develop-Phasen ohne Release nicht verloren.
/// </summary>
public interface IWhatsNewService
{
    /// <summary>Aktuelle App-Version (aus Assembly-Metadata, "X.Y.Z").</summary>
    string CurrentVersion { get; }

    /// <summary>Letzte Version die der User bestaetigt hat.</summary>
    string LastSeenVersion { get; }

    /// <summary>Ob ein Whats-New-Modal angezeigt werden soll (CurrentVersion > LastSeenVersion + Eintraege vorhanden).</summary>
    bool ShouldShow { get; }

    /// <summary>
    /// Liefert die Liste der Whats-New-Eintraege fuer alle Versionen seit
    /// <see cref="LastSeenVersion"/> bis einschliesslich <see cref="CurrentVersion"/>.
    /// Bei Erstinstall (LastSeenVersion leer) wird die Liste ohnehin nicht angezeigt
    /// (siehe <see cref="ShouldShow"/>) — der Aufrufer bekommt aber den korrekt
    /// gefilterten Bestand fuer Debug-/Test-Zwecke.
    /// </summary>
    IReadOnlyList<WhatsNewEntry> GetEntries();

    /// <summary>
    /// Bestaetigt das Modal (LastSeenVersion = CurrentVersion).
    /// Modal wird beim naechsten App-Start nicht erneut angezeigt bis zur naechsten Version.
    /// </summary>
    void MarkSeen();
}

/// <summary>Ein einzelner Whats-New-Eintrag.</summary>
public sealed class WhatsNewEntry
{
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Bullets { get; init; } = Array.Empty<string>();
    public string? HeroImagePath { get; init; }  // optional Asset-Pfad fuer Header-Bild
}
