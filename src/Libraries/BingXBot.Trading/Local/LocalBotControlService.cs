using System.Diagnostics;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;

namespace BingXBot.Trading.Local;

/// <summary>
/// Server-seitige Bot-Steuerung: Multi-TF Standalone (15.04.2026) — wrappt LiveTradingManager
/// und PaperTradingService hinter einer einheitlichen IBotControlService-Fassade.
///
/// Mode-Mapping:
///   - TradingMode.Paper -> PaperTradingService.Start()
///   - TradingMode.Live  -> LiveTradingManager.ConnectAsync() + StartAsync()
///
/// StatusChanged wird aus BotEventBus.BotStateChanged abgeleitet.
///
/// Auto-Resume (24.04.2026): Bei Start wird <see cref="BotSettings.WasRunningOnShutdown"/>
/// auf true gesetzt + persistiert. Bei Stop/EmergencyStop auf false. Pi-Reboot/systemd-Restart
/// triggert dann <c>BotAutoResumeService</c>, der den Bot automatisch wieder startet.
/// Wenn <see cref="BotDatabaseService"/> null injiziert wird (z.B. im Client-Modus oder Tests),
/// laeuft die Logik harmlos ohne Persistenz weiter.
/// </summary>
public sealed class LocalBotControlService : IBotControlService, IDisposable
{
    private readonly LiveTradingManager _liveManager;
    private readonly PaperTradingService _paperService;
    private readonly CrossSectional.CrossSectionalManager? _xsecManager;
    private readonly BotSettings _botSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _db;
    private readonly ISecureStorageService? _secureStorage;
    private readonly StrategyManager _strategyManager;
    private readonly Stopwatch _uptime = new();

    /// <summary>
    /// Idempotenz-Schutz (24.04.2026 Debugger-Robustness #2): verhindert dass parallele
    /// Start/Stop-Aufrufe (z.B. Auto-Resume + manueller Client-Click in den 15s) die Engine
    /// zweimal starten oder zwei parallele Stop-Sequenzen auf BingX-Positionen feuern.
    /// PaperTradingService.Start hat zwar einen `if (_isRunning) return;`-Check, der ist aber
    /// nicht thread-safe (kein volatile, kein Lock) — Lock auf der Top-Level-API ist sicherer.
    /// </summary>
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private BotState _lastState = BotState.Stopped;
    private string? _lastError;

    public event Action<BotStatusDto>? StatusChanged;

    public LocalBotControlService(
        LiveTradingManager liveManager,
        PaperTradingService paperService,
        BotSettings botSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus,
        StrategyManager strategyManager,
        BotDatabaseService? db = null,
        ISecureStorageService? secureStorage = null,
        CrossSectional.CrossSectionalManager? xsecManager = null)
    {
        _liveManager = liveManager;
        _paperService = paperService;
        _xsecManager = xsecManager;
        _botSettings = botSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
        _strategyManager = strategyManager;
        _db = db;
        // DI-injizierter Secure-Storage ersetzt den Reflection-Zugriff auf LiveTradingManager._secureStorage.
        // Nullable, weil Tests ohne Secure-Storage den Service instanziieren koennen.
        _secureStorage = secureStorage;

        _eventBus.BotStateChanged += HandleBotState;
    }

    public BotStatusDto GetStatus()
    {
        var mode = _botSettings.LastMode;
        return new BotStatusDto(
            State: _lastState,
            Mode: mode,
            IsHedgeMode: _liveManager.RestClient != null,
            ActiveTimeframes: _scannerSettings.ActiveTimeframes.ToList(),
            UptimeSeconds: _uptime.IsRunning ? (long)_uptime.Elapsed.TotalSeconds : 0,
            HasCredentials: _secureStorage?.HasCredentials ?? false,
            IsConnected: _liveManager.IsConnected,
            LastError: _lastError);
    }

    public Task<BotStatusDto> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(GetStatus());

    public async Task<BotStatusDto> StartAsync(BotStartRequest request, CancellationToken ct = default)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Engine-Running-Handling:
            // (A) Gleicher Mode wie angefordert -> echte Idempotenz, stummer Status-Return
            //     (Race-Fall "Auto-Resume + User-Klick in den 15 s Initial-Delay").
            // (B) Anderer Mode angefordert -> klar ablehnen mit LastError-Message, damit der
            //     Client das im UI als Fehler zeigen kann. Stummes Ignorieren fuehrte vorher
            //     zu "Dashboard sagt Live, Pi laeuft Paper" — der User merkt es nicht und
            //     denkt der Start hat geklappt.
            // Laufende Engine ermitteln (Scalper Paper/Live ODER Cross-Sectional). Es laeuft immer nur eine
            // (der _lifecycleLock + die exklusiven Manager garantieren das).
            (TradingMode Mode, EngineMode Engine)? running =
                (_xsecManager?.IsRunning ?? false) ? (_botSettings.LastMode, EngineMode.CrossSectional)
                : _paperService.IsRunning ? (TradingMode.Paper, EngineMode.Scalper)
                : _liveManager.IsRunning ? (TradingMode.Live, EngineMode.Scalper)
                : null;

            if (running.HasValue)
            {
                if (running.Value == (request.Mode, request.Engine))
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                        $"Start ignoriert: Engine laeuft bereits ({running.Value.Mode}/{running.Value.Engine})."));
                    return GetStatus();
                }

                _lastError = $"Bot laeuft bereits als {running.Value.Mode}/{running.Value.Engine}. Bitte zuerst Stop klicken, dann wechseln.";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                    $"Wechsel {running.Value.Mode}/{running.Value.Engine} -> {request.Mode}/{request.Engine} abgelehnt: Engine laeuft bereits."));
                return GetStatus();
            }

            _lastError = null;
            _botSettings.LastMode = request.Mode;
            _botSettings.LastEngineMode = request.Engine;

            if (request.ActiveTimeframes is { Count: > 0 })
            {
                _scannerSettings.ActiveTimeframes = request.ActiveTimeframes.ToList();
                // Legacy-M5-Migration: Clients mit alter UI senden evtl. M5 — auf M15 umlenken.
                _scannerSettings.MigrateLegacyM5();
            }

            try
            {
                if (request.Engine == EngineMode.CrossSectional)
                {
                    // Cross-Sectional-Momentum (market-neutraler Korb). Eigener Manager, kein per-Symbol-Scan.
                    // Paper = SimulatedExchange, Live = BingXRestClient (Hedge zwingend). Kein StrategyManager noetig.
                    if (_xsecManager == null)
                        throw new InvalidOperationException("Cross-Sectional-Modus nicht verfuegbar (Manager nicht registriert).");
                    await _xsecManager.StartAsync(request.Mode, request.InitialBalance).ConfigureAwait(false);
                }
                else if (request.Mode == TradingMode.Paper)
                {
                    // Strategie aktivieren — sonst greift der ScanAndTradeAsync-Guard
                    // (`_strategyManager.CurrentTemplate == null`) und der Bot logged stumm
                    // "Keine Strategie ausgewählt" ohne Trades zu eröffnen. Live-Pfad macht das
                    // analog in `LiveTradingManager.StartAsync`.
                    var paperStrategyName = _botSettings.LastStrategyName ?? "SK-System";
                    var paperStrategy = StrategyFactory.Create(paperStrategyName);
                    _strategyManager.SetStrategy(paperStrategy);
                    _scannerSettings.IsHedgeModeActive = true;
                    _paperService.Start(request.InitialBalance ?? _botSettings.PaperInitialBalance);
                }
                else
                {
                    var connect = await _liveManager.ConnectAsync().ConfigureAwait(false);
                    var strategy = _botSettings.LastStrategyName ?? "SK-System";
                    await _liveManager.StartAsync(strategy).ConfigureAwait(false);
                    _ = connect;
                }

                _uptime.Restart();

                // Auto-Resume-Flag setzen + persistieren (Pi-Reboot ueberlebt Engine-Zustand).
                await PersistResumeFlagAsync(true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"Start fehlgeschlagen: {ex.Message}"));
                _eventBus.PublishBotState(BotState.Error);
                // Auto-Resume-Loop-Schutz (24.04.2026): Bei dauerhaft fehlenden API-Keys / BingX-Outage
                // wuerde der Server sonst bei jedem Reboot erneut versuchen + scheitern + loggen.
                // Flag auf false setzen → User muss nach Ursachenbehebung manuell starten.
                await PersistResumeFlagAsync(false).ConfigureAwait(false);
            }

            return GetStatus();
        }
        finally { _lifecycleLock.Release(); }
    }

    public async Task<BotStatusDto> StopAsync(CancellationToken ct = default)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Crash-Safety: User-Wille (kein Auto-Resume) ZUERST persistieren — falls der Server
            // zwischen Engine-Stop und Flag-Persist crasht, soll der naechste Reboot den Bot
            // NICHT auto-resumen. Beim Crash ist die Engine eh weg, die in-memory-/DB-Inkonsistenz
            // wird durch den Prozess-Tod aufgeloest.
            await PersistResumeFlagAsync(false).ConfigureAwait(false);

            try
            {
                if (_paperService.IsRunning)
                {
                    await _paperService.StopAsync().ConfigureAwait(false);
                }
                if (_liveManager.IsRunning)
                {
                    await _liveManager.StopAsync().ConfigureAwait(false);
                }
                if (_xsecManager?.IsRunning == true)
                {
                    await _xsecManager.StopAsync().ConfigureAwait(false);
                }
                _uptime.Reset();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"Stop fehlgeschlagen: {ex.Message}"));
            }

            return GetStatus();
        }
        finally { _lifecycleLock.Release(); }
    }

    public async Task<BotStatusDto> EmergencyStopAsync(CancellationToken ct = default)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Crash-Safety: EmergencyStop ist die staerkste User-Eskalation — Flag MUSS sofort weg,
            // BEVOR Engine-Stop. Sonst koennte ein Crash mid-stop dazu fuehren dass der naechste
            // Reboot die Engine wieder startet — bei Notfall-Stop ist das die schlimmste Konsequenz.
            await PersistResumeFlagAsync(false).ConfigureAwait(false);

            try
            {
                if (_paperService.IsRunning)
                {
                    await _paperService.EmergencyStopAsync().ConfigureAwait(false);
                }
                if (_liveManager.IsRunning)
                {
                    await _liveManager.EmergencyStopAsync().ConfigureAwait(false);
                }
                if (_xsecManager?.IsRunning == true)
                {
                    await _xsecManager.EmergencyStopAsync().ConfigureAwait(false);
                }
                _uptime.Reset();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"EmergencyStop fehlgeschlagen: {ex.Message}"));
            }
            return GetStatus();
        }
        finally { _lifecycleLock.Release(); }
    }

    /// <summary>
    /// Setzt <see cref="BotSettings.WasRunningOnShutdown"/> (in-memory) und persistiert das Flag
    /// separat via <see cref="BotDatabaseService.SaveAutoResumeFlagAsync(bool)"/>.
    ///
    /// Bewusst NICHT via <c>SaveSettingsAsync(_botSettings)</c>: Die volle BotSettings-Serialisierung
    /// ist ein Race-Risiko, weil Start/Stop oft parallel zu UI-Mutationen an den mutablen Collections
    /// (<c>Scanner.ActiveTimeframes</c>, <c>Whitelist</c>, ...) laufen. Separater Key vermeidet das komplett.
    ///
    /// Best-effort: Persistenz-Fehler werden geloggt, blockieren aber nicht die Bot-Steuerung
    /// (sonst koennte der User bei DB-Lock o.ae. den Bot weder starten noch stoppen).
    /// Bei <c>_db == null</c> (Client-Modus, Tests) wird das Property nur in-memory gesetzt.
    /// </summary>
    private async Task PersistResumeFlagAsync(bool value)
    {
        _botSettings.WasRunningOnShutdown = value;
        if (_db == null) return;
        try
        {
            await _db.SaveAutoResumeFlagAsync(value).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                $"Auto-Resume-Flag konnte nicht persistiert werden: {ex.Message}"));
        }
    }

    public async Task ClosePositionAsync(string symbol, Side side, CancellationToken ct = default)
    {
        // Bevorzugter Pfad: ueber den TradingService selber, damit der ganze CompletedTrade-Flow
        // (DB-Persist, RiskManager-Stats, EventBus.PublishTrade, ExitState-Cleanup) durchlaeuft.
        // Vorher ging das via _restClient.ClosePositionAsync direkt an BingX vorbei — Bot blieb
        // im Dunklen, Trade landete weder in der DB noch im SignalR-Stream.
        // Cross-Sectional: ueber die Execution-Exchange des Managers schliessen (Paper-Sim oder Live-Rest).
        if (_xsecManager?.IsRunning == true)
        {
            if (_xsecManager.RestClient is { } xrest) { await xrest.ClosePositionAsync(symbol, side).ConfigureAwait(false); return; }
            if (_xsecManager.PaperExchange is { } xpaper) { await xpaper.ClosePositionAsync(symbol, side).ConfigureAwait(false); return; }
        }

        if (_liveManager.IsRunning && _liveManager.Service is { } liveSvc)
        {
            await liveSvc.ClosePositionExternalAsync(symbol, side).ConfigureAwait(false);
            return;
        }

        if (_paperService.IsRunning)
        {
            await _paperService.ClosePositionExternalAsync(symbol, side).ConfigureAwait(false);
            return;
        }

        // Fallback: Bot laeuft nicht, aber Position muss trotzdem zu (z.B. nach Stop manuell zumachen).
        if (_liveManager.IsConnected && _liveManager.RestClient is { } rest)
        {
            await rest.ClosePositionAsync(symbol, side).ConfigureAwait(false);
            return;
        }

        if (_paperService.Exchange is { } exchange)
        {
            await exchange.ClosePositionAsync(symbol, side).ConfigureAwait(false);
        }
    }

    private void HandleBotState(object? sender, BotState state)
    {
        _lastState = state;
        if (state == BotState.Running && !_uptime.IsRunning) _uptime.Restart();
        if (state is BotState.Stopped or BotState.EmergencyStop or BotState.Error) _uptime.Reset();
        StatusChanged?.Invoke(GetStatus());
    }

    public void Dispose()
    {
        _eventBus.BotStateChanged -= HandleBotState;
        _lifecycleLock.Dispose();
    }
}
