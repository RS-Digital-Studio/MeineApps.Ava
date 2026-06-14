using System.Text.Json;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Models;

namespace BingXBot.Trading.Local;

/// <summary>
/// Server-seitige Settings-Impl: Kennt die Singleton-Settings-Instanzen + die Datenbank.
/// Aenderungen werden direkt in den Singletons uebernommen (weil sie per DI in Engine/Scanner
/// injiziert sind) UND in der DB persistiert.
///
/// Hinweis: Die DTO-Properties werden per Reflection-freiem "Set all" auf die Singletons
/// uebertragen (einfache POCO-Zuweisung). Kein AutoMapper noetig, weil Core-Klassen + DTOs
/// identische Strukturen sind.
/// </summary>
public sealed class LocalSettingsService : ISettingsService, IDisposable
{
    private readonly BotSettings _botSettings;
    private readonly RiskSettings _riskSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly BacktestSettings _backtestSettings;
    private readonly BotDatabaseService? _db;
    private int _revision;

    /// <summary>
    /// Schuetzt CopyPoco + PersistAsync vor parallelem Self-Race (24.04.2026 Debugger-Audit).
    /// Szenario: Client A ruft SaveBotAsync, Client B parallel SaveScannerAsync. Ohne Lock
    /// kann CopyPoco im einen Pfad mit JsonSerializer.Serialize im anderen kollidieren —
    /// `InvalidOperationException: Collection was modified` waere die Folge, Settings wuerden
    /// stillschweigend nicht persistiert. Auch zwischen LocalBotControlService.StartAsync
    /// (direkter Reference-Replace auf ScannerSettings.ActiveTimeframes) und PersistAsync
    /// reduziert das Race-Fenster — auch wenn ein vollstaendiger Schutz dort ein zusaetzliches
    /// Lock-Sharing erfordern wuerde.
    /// </summary>
    private readonly SemaphoreSlim _persistLock = new(1, 1);

    public event Action<FullSettingsDto>? SettingsChanged;

    /// <summary>
    /// v1.6.3 Phase 14 — Source-Tag fuer Audit-Trail. Wird vor jedem SaveXxxAsync aus dem
    /// REST-Endpoint gesetzt (`X-BingXBot-Source` Header → "User-Desktop"/"User-Mobile" /
    /// "Auto-Resume" / "Migration"). Default: "User" wenn nicht gesetzt.
    /// AsyncLocal damit parallele Save-Calls von unterschiedlichen Clients sich nicht
    /// gegenseitig den Source-Tag stehlen.
    /// </summary>
    private static readonly System.Threading.AsyncLocal<string?> _currentSource = new();
    public static string? CurrentSource
    {
        get => _currentSource.Value;
        set => _currentSource.Value = value;
    }

    /// <summary>Setzt den Source-Tag scoped, fuer using var _ = WithSource("User-Desktop").</summary>
    public static IDisposable WithSource(string source)
    {
        var prev = _currentSource.Value;
        _currentSource.Value = source;
        return new SourceScope(prev);
    }

    private sealed class SourceScope : IDisposable
    {
        private readonly string? _previous;
        public SourceScope(string? previous) { _previous = previous; }
        public void Dispose() => _currentSource.Value = _previous;
    }

    public LocalSettingsService(
        BotSettings botSettings,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BacktestSettings backtestSettings,
        BotDatabaseService? db = null)
    {
        _botSettings = botSettings;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _backtestSettings = backtestSettings;
        _db = db;
    }

    public Task<FullSettingsDto> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(new FullSettingsDto(
            Bot: _botSettings,
            Risk: _riskSettings,
            Scanner: _scannerSettings,
            Backtest: _backtestSettings,
            Revision: _revision));

    public async Task<SettingsHistoryDto> GetHistoryAsync(string? field = null, DateTime? since = null,
        int limit = 200, CancellationToken ct = default)
    {
        if (_db == null) return new SettingsHistoryDto(Array.Empty<SettingsChangeDto>());
        var lim = limit > 0 ? Math.Min(limit, 1_000) : 200;
        var changes = await _db.GetSettingsHistoryAsync(field, since, lim).ConfigureAwait(false);
        var dtos = changes.Select(c => new SettingsChangeDto(
            Timestamp: c.Timestamp,
            Field: c.Field,
            OldValue: c.OldValue,
            NewValue: c.NewValue,
            Source: c.Source,
            Snapshot: c.Snapshot)).ToList();
        return new SettingsHistoryDto(dtos);
    }

    public async Task SaveBotAsync(BotSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        List<SettingsChange>? diff = null;
        try
        {
            var beforeSnapshot = JsonSerializer.Serialize(_botSettings);
            // LastMode + WasRunningOnShutdown sind Server-Authority-Properties (gesetzt von
            // StartAsync / PersistResumeFlagAsync). CopyPoco ueberspringt sie, damit ein
            // Client-Snapshot sie nicht zurueckrollen kann — auch nicht in einem Race wo
            // preservedLastMode VOR StartAsync.Set gelesen wird und RestoreWrite NACH dem Set
            // den neuen Wert ueberschreibt (Bug 24.04.2026 Abend: Live-Start lief, LastMode
            // blieb Paper, beim naechsten AutoResume waere Paper statt Live gestartet).
            CopyPoco(settings, _botSettings);
            _botSettings.Risk = _riskSettings;
            _botSettings.Scanner = _scannerSettings;
            _botSettings.Backtest = _backtestSettings;
            diff = ComputeDiff("Bot", _botSettings, beforeSnapshot, withFullSnapshot: true);
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        if (diff is { Count: > 0 } && _db != null) await _db.LogSettingsChangesAsync(diff).ConfigureAwait(false);
        BumpAndNotify();
    }

    public async Task SaveRiskAsync(RiskSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        List<SettingsChange>? diff = null;
        try
        {
            var beforeSnapshot = JsonSerializer.Serialize(_riskSettings);
            CopyPoco(settings, _riskSettings);
            // Legacy-M5-Migration: Clients mit alter UI-Version senden evtl. M5 in PipScalingByTf.
            _riskSettings.MigrateLegacyM5();
            diff = ComputeDiff("Risk", _riskSettings, beforeSnapshot, withFullSnapshot: true);
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        if (diff is { Count: > 0 } && _db != null) await _db.LogSettingsChangesAsync(diff).ConfigureAwait(false);
        BumpAndNotify();
    }

    public async Task SaveScannerAsync(ScannerSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        List<SettingsChange>? diff = null;
        try
        {
            var beforeSnapshot = JsonSerializer.Serialize(_scannerSettings);
            CopyPoco(settings, _scannerSettings);
            // Legacy-M5-Migration: Clients mit alter UI-Version senden evtl. M5 in ActiveTimeframes / Dictionaries.
            _scannerSettings.MigrateLegacyM5();
            diff = ComputeDiff("Scanner", _scannerSettings, beforeSnapshot, withFullSnapshot: true);
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        if (diff is { Count: > 0 } && _db != null) await _db.LogSettingsChangesAsync(diff).ConfigureAwait(false);
        BumpAndNotify();
    }

    public async Task SaveBacktestAsync(BacktestSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        List<SettingsChange>? diff = null;
        try
        {
            var beforeSnapshot = JsonSerializer.Serialize(_backtestSettings);
            CopyPoco(settings, _backtestSettings);
            diff = ComputeDiff("Backtest", _backtestSettings, beforeSnapshot, withFullSnapshot: true);
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        if (diff is { Count: > 0 } && _db != null) await _db.LogSettingsChangesAsync(diff).ConfigureAwait(false);
        BumpAndNotify();
    }

    public async Task SaveAllAsync(FullSettingsDto snapshot, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        List<SettingsChange>? diff = null;
        try
        {
            var beforeBot = JsonSerializer.Serialize(_botSettings);
            var beforeRisk = JsonSerializer.Serialize(_riskSettings);
            var beforeScanner = JsonSerializer.Serialize(_scannerSettings);
            var beforeBacktest = JsonSerializer.Serialize(_backtestSettings);
            // LastMode + WasRunningOnShutdown sind Server-Authority — siehe SaveBotAsync.
            // CopyPoco filtert sie raus, kein Race mit parallelem StartAsync mehr moeglich.
            CopyPoco(snapshot.Bot, _botSettings);
            CopyPoco(snapshot.Risk, _riskSettings);
            CopyPoco(snapshot.Scanner, _scannerSettings);
            CopyPoco(snapshot.Backtest, _backtestSettings);
            // Legacy-M5-Migration: greift beim Empfangen alter Client-Snapshots.
            _scannerSettings.MigrateLegacyM5();
            _riskSettings.MigrateLegacyM5();
            _botSettings.Risk = _riskSettings;
            _botSettings.Scanner = _scannerSettings;
            _botSettings.Backtest = _backtestSettings;
            diff = new List<SettingsChange>();
            diff.AddRange(ComputeDiff("Bot", _botSettings, beforeBot, withFullSnapshot: false));
            diff.AddRange(ComputeDiff("Risk", _riskSettings, beforeRisk, withFullSnapshot: false));
            diff.AddRange(ComputeDiff("Scanner", _scannerSettings, beforeScanner, withFullSnapshot: false));
            diff.AddRange(ComputeDiff("Backtest", _backtestSettings, beforeBacktest, withFullSnapshot: false));
            // 1× Full-Snapshot in der ersten Diff-Row (Phase 14 Plan: Snapshot pro SaveAllAsync-Call).
            if (diff.Count > 0)
            {
                var fullSnap = JsonSerializer.Serialize(_botSettings);
                diff[0] = diff[0] with { Snapshot = fullSnap };
            }
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        if (diff is { Count: > 0 } && _db != null) await _db.LogSettingsChangesAsync(diff).ConfigureAwait(false);
        BumpAndNotify();
    }

    /// <summary>
    /// v1.6.3 Phase 14 — Diff-Helper. Vergleicht den JSON-Snapshot von vor dem Save mit dem
    /// aktuellen State und liefert eine SettingsChange-Liste pro geaendertes Top-Level-Feld.
    /// Reflection-Diff ueber JsonElement-Properties — robust gegen Collections/Records.
    /// </summary>
    private static List<SettingsChange> ComputeDiff<T>(string blockName, T currentObj, string beforeJson, bool withFullSnapshot)
        where T : class
    {
        var result = new List<SettingsChange>();
        try
        {
            var afterJson = JsonSerializer.Serialize(currentObj);
            using var beforeDoc = JsonDocument.Parse(beforeJson);
            using var afterDoc = JsonDocument.Parse(afterJson);
            var ts = DateTime.UtcNow;

            foreach (var afterProp in afterDoc.RootElement.EnumerateObject())
            {
                string? oldVal = null;
                if (beforeDoc.RootElement.TryGetProperty(afterProp.Name, out var beforeVal))
                    oldVal = beforeVal.GetRawText();
                var newVal = afterProp.Value.GetRawText();
                if (oldVal == newVal) continue;
                result.Add(new SettingsChange(
                    Timestamp: ts,
                    Field: $"{blockName}.{afterProp.Name}",
                    OldValue: oldVal,
                    NewValue: newVal,
                    Source: CurrentSource ?? "User",
                    Snapshot: null));
            }

            if (withFullSnapshot && result.Count > 0)
            {
                result[0] = result[0] with { Snapshot = afterJson };
            }
        }
        catch { /* Diff ist best-effort, blockiert den Save-Pfad nicht */ }
        return result;
    }

    /// <summary>
    /// MUSS innerhalb von <see cref="_persistLock"/> aufgerufen werden — das verhindert dass
    /// JsonSerializer.Serialize gegen parallele Settings-Mutationen race-t.
    /// </summary>
    private async Task PersistInternalAsync(CancellationToken ct)
    {
        if (_db == null) return;
        await _db.SaveSettingsAsync(_botSettings).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _persistLock.Dispose();
    }

    private void BumpAndNotify()
    {
        _revision++;
        SettingsChanged?.Invoke(new FullSettingsDto(
            _botSettings, _riskSettings, _scannerSettings, _backtestSettings, _revision));
    }

    /// <summary>
    /// Public Trigger fuer das <see cref="SettingsChanged"/>-Event ohne vorherigen Save.
    /// Nutzt der Local-Mode-Bootstrap nach dem initialen DB-Restore, damit die ViewModels
    /// (RiskSettingsViewModel/ScannerViewModel) ihre Bindings refreshen — sonst zeigen sie
    /// bis zum App-Restart die Defaults vom ersten Lazy-Resolve.
    /// </summary>
    public void RaiseChanged() => BumpAndNotify();

    // Kopiert Public-Properties (POCO-zu-POCO, gleicher Typ). Bewahrt die Referenz-Identitaet
    // des Ziel-Objekts — wichtig, weil andere Services Singleton-Referenzen halten.
    //
    // Nav-Props (BotSettings.Risk/Scanner/Backtest) werden AUSGESCHLOSSEN: der Client-Snapshot
    // wuerde sonst die Server-Singleton-Referenzen ueberschreiben — paralleler Scan-Loop saehe
    // fremde Objekte, RiskManager/Scanner-Konsumenten wuerden gegen deserialisierte Client-Instanzen
    // arbeiten statt gegen die DI-Singletons. Die Aufrufer (SaveBotAsync/SaveAllAsync) setzen
    // die Nav-Refs ueber die Singletons danach explizit neu.
    private static readonly HashSet<Type> NavigationTypesToSkip = new()
    {
        typeof(RiskSettings),
        typeof(ScannerSettings),
        typeof(BacktestSettings)
    };

    // Server-Authority-Properties: auch bei CopyPoco IMMER ueberspringen, damit ein Client-Snapshot
    // (mit z.B. LastMode=Paper-Default) den aktuellen Server-Runtime-State nicht kippt. Die setzende
    // Instanz ist ausschliesslich LocalBotControlService.StartAsync / PersistResumeFlagAsync +
    // Program.cs-Initial-Load aus der DB. Race-Sicher, weil CopyPoco die Felder komplett ignoriert.
    private static readonly HashSet<string> ServerAuthorityProperties = new(StringComparer.Ordinal)
    {
        nameof(BotSettings.LastMode),
        nameof(BotSettings.LastEngineMode),
        nameof(BotSettings.WasRunningOnShutdown)
    };

    private static void CopyPoco<T>(T src, T dst) where T : class
    {
        var props = typeof(T).GetProperties()
            .Where(p => p.CanRead && p.CanWrite
                && !NavigationTypesToSkip.Contains(p.PropertyType)
                && !ServerAuthorityProperties.Contains(p.Name));
        foreach (var p in props)
        {
            var value = p.GetValue(src);
            p.SetValue(dst, value);
        }
    }
}
