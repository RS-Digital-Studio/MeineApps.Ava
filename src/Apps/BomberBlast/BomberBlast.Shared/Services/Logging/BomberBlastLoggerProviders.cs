using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services.Logging;

/// <summary>
/// Eigene <see cref="ILoggerProvider"/>-Implementierungen fuer BomberBlast.
///
/// <para>
/// Microsoft.Extensions.Logging-Infrastruktur mit zwei eigenen Providern (Code-only-Mandat,
/// keine externen NuGet-Sinks):
/// </para>
/// <list type="bullet">
/// <item><see cref="TraceLoggerProvider"/> — schreibt nach <see cref="Trace"/> (LogCat auf
///       Android, Debug-Output auf Desktop).</item>
/// <item><see cref="FileLoggerProvider"/> — rollende Log-Datei im App-Daten-Verzeichnis.
///       Ueberlebt App-Crashes → Bug-Reproduktion aus dem Log statt aus dem Gedaechtnis.</item>
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
