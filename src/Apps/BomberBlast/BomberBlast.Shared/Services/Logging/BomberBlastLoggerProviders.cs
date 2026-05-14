using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services.Logging;

/// <summary>
/// Eigene <see cref="ILoggerProvider"/>-Implementierungen fuer BomberBlast (Sprint 4.1 AAA-Audit #6).
///
/// <para>
/// Statt eines <see cref="Trace"/>-Wrappers nutzt <see cref="AppLogger"/> jetzt die echte
/// <c>Microsoft.Extensions.Logging</c>-Infrastruktur — Level-Filterung, mehrere Sinks,
/// strukturierte Logs. Zwei eigene Provider statt externer NuGet-Pakete (Code-only-Mandat):
/// </para>
/// <list type="bullet">
/// <item><see cref="TraceLoggerProvider"/> — schreibt nach <see cref="Trace"/> (LogCat auf
///       Android, Debug-Output auf Desktop). Ersetzt den alten Trace.WriteLine-Pfad.</item>
/// <item><see cref="FileLoggerProvider"/> — rollende Log-Datei im App-Daten-Verzeichnis.
///       Ueberlebt App-Crashes → Bug-Reproduktion aus dem Log statt aus dem Gedaechtnis.</item>
/// </list>
///
/// <para>
/// Scope-Handling bleibt in <see cref="AppLogger"/> (AsyncLocal-String-Prefix) — die Provider
/// sind bewusst "dumm" und formatieren nur Timestamp + Level + bereits scope-praefixte Message.
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

/// <summary>No-Op-Scope-Disposable — Scopes werden in AppLogger via AsyncLocal-Prefix gehandhabt.</summary>
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();
    private NullScope() { }
    public void Dispose() { }
}
