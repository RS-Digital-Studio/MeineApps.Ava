namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet kontextuelle Tipps für das Gilden-System (First-Time-Flags).
/// Zeigt beim ersten Besuch eines Gilden-Features einen Hilfe-Tipp an.
/// </summary>
public interface IGuildTipService
{
    /// <summary>Gibt den Tipp-Text für einen bestimmten Kontext zurück (null wenn bereits gesehen).</summary>
    string? GetTipForContext(string context);

    /// <summary>Markiert einen Tipp als gesehen (wird nicht mehr angezeigt).</summary>
    void MarkTipSeen(string context);

    /// <summary>Prüft ob ein ungesehener Tipp für den Kontext existiert.</summary>
    bool HasUnseenTip(string context);
}
