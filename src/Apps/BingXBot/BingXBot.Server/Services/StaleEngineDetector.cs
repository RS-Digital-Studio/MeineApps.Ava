using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Enums;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService: Warnt per FCM wenn der Bot laut State "Running" ist, aber seit >6 h keine
/// Aktivitaet mehr zeigt (weder ScannerResult noch TradeOpened).
///
/// Symptom das heute aufgefallen ist: Pi-Server sagt Running, aber Engine hat seit Tagen nichts
/// gescannt → UI sieht Daten vom 20.04., Bot trades nichts. Der UI-Watchdog deckt den Fall lokal,
/// aber nicht push-basiert. Dieser Service pinged Robert aktiv wenn es passiert.
///
/// Anti-Spam: Nach einem Push wird 12 h nicht erneut gepusht fuer denselben Incident
/// (ausser der Bot kommt wieder in Bewegung und kriegt dann erneut Still-Stand).
/// </summary>
public sealed class StaleEngineDetector : IHostedService, IDisposable
{
    private readonly IBotEventStream _stream;
    private readonly FcmDeviceStore _store;
    private readonly ILogger<StaleEngineDetector> _logger;

    private readonly TimeSpan _staleAfter = TimeSpan.FromHours(6);
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _minTimeBetweenPushes = TimeSpan.FromHours(12);

    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private BotState _currentState = BotState.Stopped;
    private DateTime _lastPushUtc = DateTime.MinValue;

    private Timer? _timer;
    private readonly object _gate = new();

    public StaleEngineDetector(IBotEventStream stream, FcmDeviceStore store, ILogger<StaleEngineDetector> logger)
    {
        _stream = stream;
        _store = store;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stream.BotStateChanged += OnBotStateChanged;
        _stream.ScannerResult += OnActivity;
        _stream.TradeOpened += OnActivity;

        // Erster Check nach 10 min (nicht sofort, Bot muss Zeit zum Aufwaermen haben)
        _timer = new Timer(_ => CheckStale(), null, _checkInterval, _checkInterval);
        _logger.LogInformation("StaleEngineDetector gestartet (Schwelle {Hours} h, Check alle {Min} min)",
            _staleAfter.TotalHours, _checkInterval.TotalMinutes);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        _stream.BotStateChanged -= OnBotStateChanged;
        _stream.ScannerResult -= OnActivity;
        _stream.TradeOpened -= OnActivity;
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private void OnBotStateChanged(BotStateChangedDto dto)
    {
        lock (_gate)
        {
            _currentState = dto.State;
            if (dto.State == BotState.Running)
            {
                // Reset Stale-Timer bei Start/Resume — erst ab jetzt wieder counting.
                _lastActivityUtc = DateTime.UtcNow;
            }
        }
    }

    private void OnActivity(ScannerResultDto _) { lock (_gate) _lastActivityUtc = DateTime.UtcNow; }
    private void OnActivity(TradeDto _) { lock (_gate) _lastActivityUtc = DateTime.UtcNow; }

    private void CheckStale()
    {
        BotState state;
        TimeSpan idle;
        bool canPush;

        lock (_gate)
        {
            state = _currentState;
            idle = DateTime.UtcNow - _lastActivityUtc;
            canPush = (DateTime.UtcNow - _lastPushUtc) >= _minTimeBetweenPushes;
        }

        if (state != BotState.Running) return;
        if (idle < _staleAfter) return;
        if (!canPush) return;

        // Push ausloesen (loggt aktuell nur, echter FCM-Send kommt mit FirebaseAdmin-NuGet)
        _logger.LogWarning(
            "[FCM-Stub] Stale-Engine-Alert: Bot ist {Hours:F1} h ohne Scanner/Trade-Aktivitaet im Running-State. " +
            "Moegliche Ursachen: Scanner-TF leer, alle Symbole blacklisted, BingX-API-Quota, News-Blackout festgeklemmt. " +
            "{DeviceCount} Ziel-Geraete.",
            idle.TotalHours, _store.AllDevices.Count);

        lock (_gate) _lastPushUtc = DateTime.UtcNow;
    }
}
