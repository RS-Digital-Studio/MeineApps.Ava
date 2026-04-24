using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;

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

    public async Task SaveBotAsync(BotSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Fix 17.04.2026: Runtime-Zustand (LastMode) NICHT durch Client-Snapshot ueberschreiben lassen.
            // Der Client sendet bei jeder Settings-Aenderung seinen lokalen BotSettings-Snapshot —
            // dort steht aber LastMode=Paper (Default), weil der Client den Server-Wert nie synced.
            // Ohne diesen Schutz sprang der Server-Mode bei jedem Client-Save zurueck auf Paper.
            // Fix 24.04.2026: WasRunningOnShutdown analog schuetzen — Flag wird vom Server beim Start/Stop
            // gesetzt, der Client kennt keinen aktuellen Wert und wuerde ihn auf false zuruecksetzen.
            var preservedLastMode = _botSettings.LastMode;
            var preservedWasRunningOnShutdown = _botSettings.WasRunningOnShutdown;
            CopyPoco(settings, _botSettings);
            _botSettings.LastMode = preservedLastMode;
            _botSettings.WasRunningOnShutdown = preservedWasRunningOnShutdown;
            _botSettings.Risk = _riskSettings;
            _botSettings.Scanner = _scannerSettings;
            _botSettings.Backtest = _backtestSettings;
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        BumpAndNotify();
    }

    public async Task SaveRiskAsync(RiskSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CopyPoco(settings, _riskSettings);
            // Legacy-M5-Migration: Clients mit alter UI-Version senden evtl. M5 in PipScalingByTf.
            _riskSettings.MigrateLegacyM5();
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        BumpAndNotify();
    }

    public async Task SaveScannerAsync(ScannerSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CopyPoco(settings, _scannerSettings);
            // Legacy-M5-Migration: Clients mit alter UI-Version senden evtl. M5 in ActiveTimeframes / Dictionaries.
            _scannerSettings.MigrateLegacyM5();
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        BumpAndNotify();
    }

    public async Task SaveBacktestAsync(BacktestSettings settings, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CopyPoco(settings, _backtestSettings);
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        BumpAndNotify();
    }

    public async Task SaveAllAsync(FullSettingsDto snapshot, CancellationToken ct = default)
    {
        await _persistLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Fix 17.04.2026: LastMode = Server-Authority, nicht durch Client-Snapshot ueberschreiben
            // (siehe SaveBotAsync — gleicher Grund, gleicher Fix).
            // Fix 24.04.2026: WasRunningOnShutdown ebenfalls Server-Authority (Auto-Resume-Flag).
            var preservedLastMode = _botSettings.LastMode;
            var preservedWasRunningOnShutdown = _botSettings.WasRunningOnShutdown;
            CopyPoco(snapshot.Bot, _botSettings);
            _botSettings.LastMode = preservedLastMode;
            _botSettings.WasRunningOnShutdown = preservedWasRunningOnShutdown;
            CopyPoco(snapshot.Risk, _riskSettings);
            CopyPoco(snapshot.Scanner, _scannerSettings);
            CopyPoco(snapshot.Backtest, _backtestSettings);
            // Legacy-M5-Migration: greift beim Empfangen alter Client-Snapshots.
            _scannerSettings.MigrateLegacyM5();
            _riskSettings.MigrateLegacyM5();
            _botSettings.Risk = _riskSettings;
            _botSettings.Scanner = _scannerSettings;
            _botSettings.Backtest = _backtestSettings;
            await PersistInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _persistLock.Release(); }
        BumpAndNotify();
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

    private static void CopyPoco<T>(T src, T dst) where T : class
    {
        var props = typeof(T).GetProperties()
            .Where(p => p.CanRead && p.CanWrite && !NavigationTypesToSkip.Contains(p.PropertyType));
        foreach (var p in props)
        {
            var value = p.GetValue(src);
            p.SetValue(dst, value);
        }
    }
}
