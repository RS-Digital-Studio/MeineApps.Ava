using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Kontextuelles Tutorial-System: Zeigt Hints beim ersten Benutzen eines Features.
/// Ersetzt das alte lineare 8-Schritte-Tutorial.
/// </summary>
public interface IContextualHintService
{
    /// <summary>
    /// Event wenn ein neuer Hint angezeigt oder der aktive Hint dismissed wird.
    /// null = kein aktiver Hint.
    /// </summary>
    event EventHandler<ContextualHint?> HintChanged;

    /// <summary>
    /// Der aktuell angezeigte Hint (null wenn keiner aktiv).
    /// </summary>
    ContextualHint? ActiveHint { get; }

    /// <summary>
    /// Versucht einen Hint anzuzeigen. Wird ignoriert wenn der Hint bereits gesehen wurde
    /// oder ein anderer Hint gerade aktiv ist.
    /// </summary>
    /// <returns>true wenn der Hint angezeigt wird.</returns>
    bool TryShowHint(ContextualHint hint);

    /// <summary>
    /// Dismissed den aktuell aktiven Hint und markiert ihn als gesehen.
    /// </summary>
    void DismissHint();

    /// <summary>
    /// Prüft ob ein bestimmter Hint bereits gesehen wurde.
    /// </summary>
    bool HasSeenHint(string hintId);

    /// <summary>
    /// Setzt alle gesehenen Hints zurück (für Settings "Tutorial wiederholen").
    /// </summary>
    void ResetAllHints();
}
