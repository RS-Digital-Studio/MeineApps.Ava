using BingXBot.Contracts.Services;
using BingXBot.Core.Models;
using BingXBot.Trading;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService: Persistiert Equity-Snapshots in die DB und publiziert sie für Live-UI.
///
/// Snapshot-Report-Fix Befund 1 / A0.2: Im Remote-Mode laeuft der <c>DashboardViewModel</c>-Timer
/// auf dem Pi nicht (es gibt dort keine UI). Equity-Snapshots wurden also nie geschrieben — die
/// <c>EquitySnapshots</c>-Tabelle hatte 0 Zeilen, und Remote-Clients sahen keine Equity-Kurve.
///
/// Dieser Service:
/// - schreibt einen Snapshot direkt nach jedem Trade-Close (via <see cref="BotEventBus.TradeCompleted"/>),
/// - schreibt einen periodischen Snapshot alle 5 Minuten (auch ohne Trade-Aktivität),
/// - publiziert das Event auf <see cref="BotEventBus.EquityUpdate"/>, damit der
///   <c>BotHubEventForwarder</c> es per SignalR an alle Clients pushed.
/// </summary>
public sealed class EquitySnapshotService : BackgroundService
{
    private readonly IAccountService _accountService;
    private readonly BotDatabaseService _db;
    private readonly BotEventBus _eventBus;
    private readonly ILogger<EquitySnapshotService> _logger;
    private readonly TimeSpan _interval;
    private DateTime _lastTradeTriggeredSnapshotUtc = DateTime.MinValue;
    private static readonly TimeSpan TradeTriggeredDebounce = TimeSpan.FromSeconds(2);

    public EquitySnapshotService(
        IAccountService accountService,
        BotDatabaseService db,
        BotEventBus eventBus,
        IConfiguration config,
        ILogger<EquitySnapshotService> logger)
    {
        _accountService = accountService;
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        var minutes = Math.Max(1, config.GetValue<int>("Server:EquitySnapshotIntervalMinutes", 5));
        _interval = TimeSpan.FromMinutes(minutes);

        // Trade-Close triggert sofortigen Snapshot — debounced, damit ein Multi-TP-Close
        // (TP1 + TP2 binnen Sekunden) nicht zwei Snapshots in derselben Sekunde erzeugt.
        _eventBus.TradeCompleted += OnTradeCompleted;
    }

    private void OnTradeCompleted(object? sender, CompletedTrade trade)
    {
        var now = DateTime.UtcNow;
        if (now - _lastTradeTriggeredSnapshotUtc < TradeTriggeredDebounce) return;
        _lastTradeTriggeredSnapshotUtc = now;
        _ = Task.Run(async () =>
        {
            try { await CaptureAsync(reason: $"TradeClose:{trade.Symbol}", default).ConfigureAwait(false); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Trade-getriggerter Equity-Snapshot fehlgeschlagen ({Symbol})", trade.Symbol);
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EquitySnapshotService gestartet (Intervall {Minutes} min)", _interval.TotalMinutes);
        // Initial-Snapshot nach kurzem Warmup (Bot/Connection-State soll sich gesetzt haben).
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        try { await CaptureAsync(reason: "InitialAtStartup", stoppingToken).ConfigureAwait(false); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initialer Equity-Snapshot fehlgeschlagen");
        }

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try { await CaptureAsync(reason: "Periodic", stoppingToken).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Periodischer Equity-Snapshot fehlgeschlagen");
                }
            }
        }
        catch (OperationCanceledException) { /* Shutdown */ }
    }

    private async Task CaptureAsync(string reason, CancellationToken ct)
    {
        var snap = await _accountService.GetSnapshotAsync(ct).ConfigureAwait(false);
        // Equity = Wallet-Balance + Unrealisierter PnL der offenen Positionen.
        // Wenn der Bot noch nicht verbunden ist (Balance=0, keine Positions), schreiben wir keinen Punkt
        // — sonst flutet ein nicht-verbundener Pi die Equity-Kurve mit 0-er Werten.
        if (snap.Balance <= 0m && snap.OpenPositionCount == 0)
        {
            return;
        }

        var equity = snap.Balance + snap.UnrealizedPnl;
        var point = new EquityPoint(DateTime.UtcNow, equity);
        await _db.SaveEquitySnapshotAsync(point).ConfigureAwait(false);
        _eventBus.PublishEquity(point);
        _logger.LogDebug("Equity-Snapshot persistiert ({Reason}): {Equity:F2} USDT", reason, equity);
    }

    public override void Dispose()
    {
        _eventBus.TradeCompleted -= OnTradeCompleted;
        base.Dispose();
    }
}
