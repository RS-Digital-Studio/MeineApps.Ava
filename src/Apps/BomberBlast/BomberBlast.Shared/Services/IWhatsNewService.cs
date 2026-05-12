namespace BomberBlast.Services;

/// <summary>
/// "What's New"-Service (Sprint 4.3 AAA-Audit #17).
/// Liefert die aktuellste, noch nicht gesehene Version-Whats-New-Liste.
/// Wird beim App-Start gepruft — wenn die Assembly-Version neuer als die
/// LastSeenVersion-Pref ist, wird das Modal mit den neuen Eintraegen angezeigt.
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
    /// Liefert die Liste der Whats-New-Eintraege fuer die aktuelle Version.
    /// Leer wenn keine vorhanden sind.
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
