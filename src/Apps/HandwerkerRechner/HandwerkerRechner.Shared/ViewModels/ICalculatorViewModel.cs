namespace HandwerkerRechner.ViewModels;

/// <summary>
/// Gemeinsames Interface für alle 19 Calculator-ViewModels.
/// Eliminiert die 19-fachen switch/case-Blöcke in MainViewModel
/// (WireCalculatorEvents, CleanupCurrentCalculator, HandleBackPressed).
/// </summary>
public interface ICalculatorViewModel
{
    /// <summary>Navigation zurück ("..")</summary>
    event Action<string>? NavigationRequested;

    /// <summary>Fehlermeldung an den Benutzer</summary>
    event Action<string, string>? MessageRequested;

    /// <summary>Floating-Text-Feedback (z.B. "Gespeichert!")</summary>
    event Action<string, string>? FloatingTextRequested;

    /// <summary>Text in die Zwischenablage kopieren</summary>
    event Action<string>? ClipboardRequested;

    /// <summary>Ob der Speichern-Dialog gerade offen ist (für Back-Navigation)</summary>
    bool ShowSaveDialog { get; set; }

    /// <summary>Timer stoppen, Events abmelden</summary>
    void Cleanup();

    /// <summary>Projekt-Daten laden</summary>
    Task LoadFromProjectIdAsync(string projectId);
}
