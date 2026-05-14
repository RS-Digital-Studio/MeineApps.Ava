using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services.Logging;

/// <summary>
/// Eigene <see cref="ILoggerProvider"/>-Implementierungen fuer BomberBlast (.1 .
///
/// <para>
/// Microsoft.Extensions.Logging-Infrastruktur mit drei eigenen Providern (Code-only-Mandat,
/// keine externen NuGet-Sinks):
/// </para>
/// <list type="bullet">
/// <item><see cref="TraceLoggerProvider"/> — schreibt nach <see cref="Trace"/> (LogCat auf
///       Android, Debug-Output auf Desktop).</item>
/// <item><see cref="FileLoggerProvider"/> — rollende Log-Datei im App-Daten-Verzeichnis.
///       Ueberlebt App-Crashes → Bug-Reproduktion aus dem Log statt aus dem Gedaechtnis.</item>
/// <item><see cref="CrashlyticsLoggerProvider"/> — Bridge zu ITelemetryService
///       (LogError(ex) → non-fatal Crash-Report, LogWarning/Error → Breadcrumb).</item>
/// </list>
///
/// <para>
/// Standard-ILogger&lt;T&gt;-API ueber alle Services. <see cref="ILogger.BeginScope{TState}(TState)"/>
/// landet aktuell als No-Op-Scope — falls strukturierte Scopes spaeter gebraucht werden, kann
/// jeder Provider einen eigenen Scope-Stack hinzufuegen.
/// </para>
/// </summary>
internal static class LoggerFormat
{
    public static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT",
        _ => "LOG",
    };
}

/// <summary>Provider: leitet alle Logs nach <see cref="Trace"/> (LogCat / Debug-Output).</summary>
public sealed class TraceLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TraceLogger();
    public void Dispose() { }

    private sealed class TraceLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        // Level-Filterung uebernimmt die LoggerFactory (SetMinimumLevel) — hier immer true.
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            var line = $"[BomberBlast] {LoggerFormat.ShortLevel(logLevel)}: {message}";
            if (exception != null)
                line += $"\n{exception}";
            Trace.WriteLine(line);
        }
    }
}

/// <summary>
/// Provider: schreibt Logs in eine rollende Datei. Thread-safe, Groessen-Cap mit einem Backup.
/// Pfad: <c>{LocalApplicationData}/BomberBlast/logs/app.log</c> (App-intern auf Android).
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxFileSizeBytes = 512 * 1024;  // 512 KB → dann rollt die Datei

    private readonly object _writeLock = new();
    private readonly string? _logFilePath;
    private readonly string? _backupFilePath;
    private bool _disposed;

    public FileLoggerProvider()
    {
        // Best-Effort: scheitert die Pfad-Aufloesung (selten), bleibt der File-Sink inaktiv —
        // der Trace-Sink loggt trotzdem weiter.
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = AppContext.BaseDirectory;

            var logDir = Path.Combine(baseDir, "BomberBlast", "logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, "app.log");
            _backupFilePath = Path.Combine(logDir, "app.old.log");
        }
        catch
        {
            _logFilePath = null;
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this);

    public void Dispose() => _disposed = true;

    private void Append(LogLevel logLevel, string message, Exception? exception)
    {
        if (_disposed || _logFilePath is null) return;

        var sb = new StringBuilder();
        sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
          .Append(" [").Append(LoggerFormat.ShortLevel(logLevel)).Append("] ")
          .Append(message);
        if (exception != null)
            sb.Append('\n').Append(exception);
        sb.Append('\n');
        var line = sb.ToString();

        lock (_writeLock)
        {
            try
            {
                RollIfNeeded();
                File.AppendAllText(_logFilePath, line);
            }
            catch
            {
                // Best-Effort — ein fehlgeschlagener Datei-Schreibvorgang darf die App nie crashen.
            }
        }
    }

    /// <summary>Rollt die Log-Datei wenn sie das Groessen-Limit ueberschreitet (1 Backup).</summary>
    private void RollIfNeeded()
    {
        if (_logFilePath is null || _backupFilePath is null) return;
        var info = new FileInfo(_logFilePath);
        if (!info.Exists || info.Length < MaxFileSizeBytes) return;

        if (File.Exists(_backupFilePath))
            File.Delete(_backupFilePath);
        File.Move(_logFilePath, _backupFilePath);
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        public FileLogger(FileLoggerProvider provider) => _provider = provider;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            _provider.Append(logLevel, formatter(state, exception), exception);
        }
    }
}

/// <summary>No-Op-Scope-Disposable — die Provider geben das hier zurueck wenn BeginScope unsupported ist.</summary>
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();
    private NullScope() { }
    public void Dispose() { }
}

/// <summary>
/// Provider: Bridge zu <see cref="ITelemetryService"/> (Crashlytics) — uebersetzt
/// <c>ILogger</c>-Eintraege in Firebase-Non-Fatals + Breadcrumbs.
///
/// <para>
/// Migration-Pfad (Welle 5+ /.1 #14): Ersetzt die manuelle Telemetry-Bridge des alten
/// <c>AppLogger</c>. Alle Services nutzen jetzt <c>ILogger&lt;T&gt;</c>; dieser Provider
/// dockt automatisch an die Microsoft.Extensions.Logging-Pipeline an. Errors mit Exception
/// landen als non-fatal Crash-Report mit Stack-Trace in Crashlytics, Warnings/Errors ohne
/// Exception werden als Breadcrumbs gespeichert (sichtbar im naechsten Real-Crash-Stack).
/// </para>
///
/// <para>
/// Info-Breadcrumbs werden nur im DEBUG-Build weitergereicht — im Release wird die
/// Crashlytics-Breadcrumb-Quota (max 64 Events pro Session) fuer Warnings/Errors reserviert.
/// </para>
///
/// <para>
/// ITelemetryService wird lazy ueber den ServiceProvider aufgeloest — Race-frei waehrend
/// der DI-Aufbauphase. Wenn der Telemetry-Service noch nicht verfuegbar ist, ist der Bridge
/// ein No-Op (Trace + File-Sink loggen unabhaengig weiter).
/// </para>
/// </summary>
public sealed class CrashlyticsLoggerProvider : ILoggerProvider
{
    private const string Tag = "BomberBlast";

    private readonly IServiceProvider _serviceProvider;
    private ITelemetryService? _cachedTelemetry;

    public CrashlyticsLoggerProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ILogger CreateLogger(string categoryName) => new CrashlyticsLogger(this, categoryName);

    public void Dispose() { }

    private ITelemetryService? ResolveTelemetry()
    {
        // Lazy-Cache — beim ersten Call den Service holen, danach ist die Referenz stabil.
        // Race-Conditions sind unkritisch: ITelemetryService ist ein Singleton, mehrfache
        // Auflösung ergibt dieselbe Instanz.
        if (_cachedTelemetry != null) return _cachedTelemetry;
        try
        {
            _cachedTelemetry = _serviceProvider.GetService(typeof(ITelemetryService)) as ITelemetryService;
        }
        catch
        {
            // Bei DI-Lookup-Fehlern während des Build-Up bleibt der Bridge inaktiv — Trace+File
            // loggen ungestoert weiter.
        }
        return _cachedTelemetry;
    }

    private sealed class CrashlyticsLogger : ILogger
    {
        private readonly CrashlyticsLoggerProvider _provider;
        private readonly string _category;

        public CrashlyticsLogger(CrashlyticsLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= LogLevel.Information;  // Trace/Debug zu noisy fuer Crashlytics-Quota

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var telemetry = _provider.ResolveTelemetry();
            if (telemetry is null) return;

            var message = formatter(state, exception);
            var level = LoggerFormat.ShortLevel(logLevel);
            var formatted = $"[{Tag}] {level}: {message}";

            switch (logLevel)
            {
                case LogLevel.Information:
                    // Audit L04: Info-Breadcrumbs nur im DEBUG-Build an Crashlytics weitergeben.
                    // Release: Quota fuer Warnings/Errors reserviert.
#if DEBUG
                    telemetry.Log(formatted);
#endif
                    break;
                case LogLevel.Warning:
                    telemetry.Log(formatted);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    if (exception != null)
                        telemetry.LogNonFatal(exception, formatted);
                    else
                        telemetry.Log(formatted);
                    break;
            }
        }
    }
}
