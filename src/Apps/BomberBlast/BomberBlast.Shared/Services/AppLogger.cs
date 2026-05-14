using Microsoft.Extensions.Logging;

namespace BomberBlast.Services;

/// <summary>
/// AppLogger — Fassade ueber <c>Microsoft.Extensions.Logging.ILogger</c> (Sprint 4.1 AAA-Audit #6).
///
/// <para>
/// Die 53 Services injizieren weiterhin <see cref="IAppLogger"/> — unveraendert. Intern delegiert
/// der AppLogger jetzt aber an die echte <c>Microsoft.Extensions.Logging</c>-Infrastruktur:
/// Level-Filterung ueber die <see cref="ILoggerFactory"/>, mehrere Sinks
/// (<see cref="Logging.TraceLoggerProvider"/> + <see cref="Logging.FileLoggerProvider"/>),
/// strukturierte Logs. So profitieren alle Call-Sites sofort, ohne einzeln auf
/// <c>ILogger&lt;T&gt;</c> umgestellt werden zu muessen.
/// </para>
///
/// <para>
/// Zusaetzlich zur ILogger-Pipeline werden Warnings + Errors an <see cref="ITelemetryService"/>
/// weitergereicht — Errors mit Exception als non-fatal Crash-Report (Stack-Trace in Crashlytics),
/// Warnings als Breadcrumbs.
/// </para>
///
/// <para>
/// Build-Filtering uebernimmt die LoggerFactory (SetMinimumLevel: Trace im Debug, Info im Release).
/// </para>
///
/// <para>
/// Scope-Pattern: <c>using (_logger.BeginScope("game={gameId}"))</c> — AsyncLocal-basiert
/// (thread-safe fuer parallele Sub-Tasks). Der Scope wird als Message-Prefix mitgeschrieben.
/// </para>
/// </summary>
public sealed class AppLogger : IAppLogger
{
    private const string Tag = "BomberBlast";

    private readonly ILogger _logger;
    private readonly ITelemetryService? _telemetry;

    /// <summary>AsyncLocal-Scope-Stack — folgt der aktuellen Async-Operation.</summary>
    private static readonly System.Threading.AsyncLocal<string?> _scope = new();

    public AppLogger(ILoggerFactory loggerFactory, ITelemetryService? telemetry = null)
    {
        _logger = loggerFactory.CreateLogger(Tag);
        _telemetry = telemetry;
    }

    public void LogTrace(string message)
        => _logger.LogTrace("{Message}", ApplyScope(message));

    public void LogInfo(string message)
    {
        _logger.LogInformation("{Message}", ApplyScope(message));
        // Audit L04: Info-Breadcrumbs nur im DEBUG-Build an Crashlytics weitergeben.
        // Release: Crashlytics-Breadcrumb-Quota (max 64 Events pro Session) sollte fuer
        // Warnings/Errors reserviert sein. Info-Logs sind im Release-Crash-Stack i.d.R. nicht hilfreich.
#if DEBUG
        _telemetry?.Log(FormatForTelemetry("INFO", message));
#endif
    }

    public void LogWarning(string message)
    {
        _logger.LogWarning("{Message}", ApplyScope(message));
        _telemetry?.Log(FormatForTelemetry("WARN", message));
    }

    public void LogError(string message)
    {
        _logger.LogError("{Message}", ApplyScope(message));
        _telemetry?.Log(FormatForTelemetry("ERROR", message));
    }

    public void LogError(string message, Exception ex)
    {
        _logger.LogError(ex, "{Message}", ApplyScope(message));
        // Error mit Exception → non-fatal Crash-Report mit Stack-Trace.
        _telemetry?.LogNonFatal(ex, FormatForTelemetry("ERROR", message));
    }

    public IDisposable BeginScope(string scopeName)
    {
        var previous = _scope.Value;
        _scope.Value = string.IsNullOrEmpty(previous) ? scopeName : $"{previous} » {scopeName}";
        return new ScopeRestorer(previous);
    }

    /// <summary>Praefixt die Message mit dem aktuellen AsyncLocal-Scope (falls vorhanden).</summary>
    private static string ApplyScope(string message)
    {
        var s = _scope.Value;
        return string.IsNullOrEmpty(s) ? message : $"[{s}] {message}";
    }

    /// <summary>Telemetrie-Breadcrumb-Format (Tag + Level + Scope) — fuer Crashlytics-Lesbarkeit.</summary>
    private static string FormatForTelemetry(string level, string message)
    {
        var s = _scope.Value;
        return string.IsNullOrEmpty(s)
            ? $"[{Tag}] {level}: {message}"
            : $"[{Tag}] {level} [{s}]: {message}";
    }

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
