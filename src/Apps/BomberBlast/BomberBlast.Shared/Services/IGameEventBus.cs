namespace BomberBlast.Services;

/// <summary>
/// Game-Event-Bus (Sprint 4.2 AAA-Audit #10).
///
/// <para>
/// Zentraler Pub/Sub-Hub fuer UI-Events (FloatingText, Celebration, ExitHint, Message).
/// Wurde bisher direkt im MainViewModel als 1218-LOC God-VM gehalten — neuer Code kann
/// diesen Service injizieren statt durch MainViewModel zu routen.
/// </para>
///
/// <para>
/// HINWEIS: Sprint 4.2 ist Foundation-Layer. Bestehende MainViewModel-Events bleiben
/// unveraendert (Backwards-Compat) — neue Features koennen aber direkt diesen Service
/// nutzen statt eine MainViewModel-Abhaengigkeit zu schaffen.
/// </para>
///
/// <para>
/// Vollstaendige God-VM-Reduktion (NavigationHub + LazyVmRegistry extrahieren) ist
/// eigener Refactor-Sprint mit Test-Coverage-Voraussetzung.
/// </para>
/// </summary>
public interface IGameEventBus
{
    /// <summary>Floating-Text-Event (Text + Style-Hint wie "gold"/"info"/"danger").</summary>
    event Action<string, string>? FloatingTextRequested;
    /// <summary>Celebration-Effekt (Confetti-Vollbild).</summary>
    event Action? CelebrationRequested;
    /// <summary>Android-Toast-Hinweis (z.B. Double-Back-to-Exit).</summary>
    event Action<string>? ExitHintRequested;
    /// <summary>Allgemeine Message (Title + Body), fuer non-modal Snackbar.</summary>
    event Action<string, string>? MessageRequested;

    /// <summary>Floating-Text feuern.</summary>
    void RaiseFloatingText(string text, string style);
    /// <summary>Celebration feuern.</summary>
    void RaiseCelebration();
    /// <summary>Exit-Hint feuern.</summary>
    void RaiseExitHint(string message);
    /// <summary>Generische Message feuern.</summary>
    void RaiseMessage(string title, string body);
}

/// <summary>Default-Implementation: einfacher Multicast-Delegate-Hub.</summary>
public sealed class GameEventBus : IGameEventBus
{
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;
    public event Action<string>? ExitHintRequested;
    public event Action<string, string>? MessageRequested;

    public void RaiseFloatingText(string text, string style) => FloatingTextRequested?.Invoke(text, style);
    public void RaiseCelebration() => CelebrationRequested?.Invoke();
    public void RaiseExitHint(string message) => ExitHintRequested?.Invoke(message);
    public void RaiseMessage(string title, string body) => MessageRequested?.Invoke(title, body);
}
