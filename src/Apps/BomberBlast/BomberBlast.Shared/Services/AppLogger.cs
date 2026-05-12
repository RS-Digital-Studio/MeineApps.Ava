using System.Diagnostics;

namespace BomberBlast.Services;

/// <summary>
/// AppLogger mit Crashlytics-Sink (Sprint 4.1 AAA-Audit #9).
///
/// <para>
/// Schreibt Logs nach <see cref="Trace"/> (LogCat auf Android, Debug-Output auf Desktop)
/// UND leitet Warnings + Errors an <see cref="ITelemetryService"/> weiter — Errors als
/// non-fatal Exceptions, Warnings als Breadcrumbs.
/// </para>
///
/// <para>
/// Build-Filtering:
/// <list type="bullet">
/// <item>Debug-Build: alle Levels werden ausgegeben (Trace/Info/Warning/Error)</item>
/// <item>Release-Build: nur Info/Warning/Error (Trace wird unterdrueckt)</item>
/// </list>
/// </para>
///
/// <para>
/// Scope-Pattern: <c>using (_logger.BeginScope("game={gameId}"))</c> — alle Logs in
/// diesem Block bekommen den Scope-Namen als Prefix. AsyncLocal-basiert (thread-safe
/// fuer parallele Sub-Tasks).
/// </para>
/// </summary>
public sealed class AppLogger : IAppLogger
{
    private const string Tag = "BomberBlast";

    private readonly ITelemetryService? _telemetry;

    /// <summary>AsyncLocal-Scope-Stack — folgt der aktuellen Async-Operation.</summary>
    private static readonly System.Threading.AsyncLocal<string?> _scope = new();

    public AppLogger(ITelemetryService? telemetry = null)
    {
        _telemetry = telemetry;
    }

    public void LogTrace(string message)
    {
#if DEBUG
        WriteLine("TRACE", message);
#endif
    }

    public void LogInfo(string message)
    {
        WriteLine("INFO", message);
        // Info → Crashlytics-Breadcrumb (sichtbar im Crash-Stack)
        _telemetry?.Log(FormatMessage("INFO", message));
    }

    public void LogWarning(string message)
    {
        WriteLine("WARN", message);
        _telemetry?.Log(FormatMessage("WARN", message));
    }

    public void LogError(string message)
    {
        WriteLine("ERROR", message);
        _telemetry?.Log(FormatMessage("ERROR", message));
    }

    public void LogError(string message, Exception ex)
    {
        WriteLine("ERROR", $"{message} - {ex.Message}");
        // Error mit Exception → non-fatal Crash-Report mit Stack-Trace
        _telemetry?.LogNonFatal(ex, FormatMessage("ERROR", message));
    }

    public IDisposable BeginScope(string scopeName)
    {
        var previous = _scope.Value;
        _scope.Value = string.IsNullOrEmpty(previous) ? scopeName : $"{previous} » {scopeName}";
        return new ScopeRestorer(previous);
    }

    private static string FormatMessage(string level, string message)
    {
        var s = _scope.Value;
        return string.IsNullOrEmpty(s)
            ? $"[{Tag}] {level}: {message}"
            : $"[{Tag}] {level} [{s}]: {message}";
    }

    private static void WriteLine(string level, string message)
        => Trace.WriteLine(FormatMessage(level, message));

    private sealed class ScopeRestorer : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;
        public ScopeRestorer(string? previous) => _previous = previous;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scope.Value = _previous;
        }
    }
}
