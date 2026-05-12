namespace BomberBlast.Services;

/// <summary>
/// Strukturiertes Logging-Interface (ersetzt Debug.WriteLine).
/// Sprint 4.1 AAA-Audit #9: AppLogger leitet Errors+Warnings an ITelemetryService weiter
/// (Crashlytics-Sink) — sichtbar in Firebase mit Stack-Trace + Custom-Keys.
/// </summary>
public interface IAppLogger
{
    /// <summary>Trace-Level: Sehr detaillierte Diagnose. Im Release-Build unterdrueckt.</summary>
    void LogTrace(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(string message, Exception ex);

    /// <summary>
    /// Sprint 4.1 AAA-Audit #9: Beginnt einen "Scope" — Sub-Lifetime mit auto-Dispose
    /// fuer strukturierte Kontextangabe (z.B. GameSession-ID).
    /// Aktuell als String-Prefix, spaeter ggf. ILogger&lt;T&gt;-Scopes.
    /// </summary>
    IDisposable BeginScope(string scopeName);
}
