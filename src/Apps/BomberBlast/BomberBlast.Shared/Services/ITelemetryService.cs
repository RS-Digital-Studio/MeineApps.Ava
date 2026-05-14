namespace BomberBlast.Services;

/// <summary>
/// Crash-Reporting + Performance-Monitoring (Firebase Crashlytics).
/// Code-Hooks vorbereitet (v2.0.44 — ). Console-Setup macht Robert.
/// Custom-Keys werden bei jedem Crash mit aufgezeichnet → erlauben Crash-Filter
/// nach Modus / Level / FPS-Bucket / Memory-Pressure.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Initialisierung beim App-Start. Setzt einen anonymisierten User-ID-Hash
    /// für Crash-Aggregation (NICHT die Firebase-UID — DSGVO-Schutz).
    /// </summary>
    void Initialize();

    /// <summary>
    /// Custom-Key der bei JEDEM Crash mitgeschickt wird (Mode/Level/FPS-Bucket/MemoryMB).
    /// Wert wird auf 100 Zeichen geclamped.
    /// </summary>
    void SetCustomKey(string key, string value);

    /// <summary>Custom-Key mit int-Wert.</summary>
    void SetCustomKey(string key, int value);

    /// <summary>Custom-Key mit bool-Wert.</summary>
    void SetCustomKey(string key, bool value);

    /// <summary>
    /// Loggt eine non-fatal Exception (App läuft weiter, aber wir wollen es wissen).
    /// Idiotensicher: Null-Exception wird ignoriert.
    /// </summary>
    void LogNonFatal(Exception ex, string? context = null);

    /// <summary>
    /// Loggt eine Breadcrumb-Message (sichtbar in Crashlytics-Stack-Trace bei nächstem Crash).
    /// Default-Implementation tut nichts.
    /// </summary>
    void Log(string message);

    /// <summary>
    /// Performance-Trace starten. Returnt einen Disposable, der den Trace stoppt.
    /// Misst Wall-Clock-Zeit zwischen Start und Stop.
    /// </summary>
    IDisposable StartTrace(string traceName);

    /// <summary>
    /// FPS-Bucket setzen (für Crash-Filterung: 60FPS-Crashes vs 30FPS-Crashes).
    /// Wird vom Game-Loop alle 5s aufgerufen.
    /// </summary>
    void SetFpsBucket(int avgFps);
}

/// <summary>
/// No-Op-Implementierung für Desktop oder bevor Firebase Crashlytics konfiguriert ist.
/// </summary>
public sealed class NullTelemetryService : ITelemetryService
{
    public void Initialize() { }
    public void SetCustomKey(string key, string value) { }
    public void SetCustomKey(string key, int value) { }
    public void SetCustomKey(string key, bool value) { }
    public void LogNonFatal(Exception ex, string? context = null) { }
    public void Log(string message) { }
    public IDisposable StartTrace(string traceName) => NullTrace.Instance;
    public void SetFpsBucket(int avgFps) { }

    private sealed class NullTrace : IDisposable
    {
        public static readonly NullTrace Instance = new();
        public void Dispose() { }
    }
}
