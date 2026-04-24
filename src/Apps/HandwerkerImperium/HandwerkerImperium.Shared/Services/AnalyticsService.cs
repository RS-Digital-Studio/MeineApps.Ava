using System.Collections.Concurrent;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// REST-basierte Analytics-Implementierung via <see cref="IFirebaseService"/>.
/// Events werden in einer Queue gesammelt und alle 30s oder bei Pause/Close
/// in einem Batch an <c>analytics_events/{YYYY-MM-DD}/{pushId}</c> geschickt.
///
/// Design:
/// - Batching: Reduziert HTTP-Calls drastisch (1x alle 30s statt 1x pro Event).
/// - DSGVO: No-Op wenn <see cref="IsEnabled"/> false ist. Consent liegt im SettingsData.
/// - Robust: Fehlerhaftes Senden wirft die betroffenen Events nicht weg — sie werden beim
///   naechsten Flush erneut versucht, solange die Queue unter dem Cap ist.
/// - Offline-tolerant: Queue haelt bis zu <see cref="QueueCap"/> Events — danach werden
///   alte Eintraege verworfen (FIFO), damit der Speicher nicht explodiert.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private const int QueueCap = 500;                       // Max. Events in Memory
    private const int FlushIntervalSeconds = 30;            // Batch-Intervall
    private const int MaxBatchSize = 50;                    // Events pro HTTP-Request

    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly IPreferencesService _preferences;
    private readonly ILogService _log;

    private readonly ConcurrentQueue<AnalyticsEventPayload> _queue = new();
    private readonly Dictionary<string, string?> _userProperties = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly object _userPropsLock = new();

    private System.Timers.Timer? _flushTimer;
    private bool _disposed;
    private bool _isEnabled;
    private string? _sessionId;

    public AnalyticsService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        IPreferencesService preferences,
        ILogService log)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _preferences = preferences;
        _log = log;
    }

    /// <inheritdoc />
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;

            // Consent in Settings persistieren
            if (_gameStateService.IsInitialized)
            {
                _gameStateService.Settings.AnalyticsEnabled = value;
            }

            if (value)
            {
                StartFlushTimer();
            }
            else
            {
                StopFlushTimer();
                // Queue verwerfen — keine Daten nach Opt-Out senden
                while (_queue.TryDequeue(out _)) { }
            }
        }
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (!_gameStateService.IsInitialized) return;

        _sessionId = Guid.NewGuid().ToString("N")[..12];

        // Basis-User-Properties setzen
        SetUserProperty(AnalyticsUserProperties.Language, _gameStateService.Settings.Language);
        SetUserProperty(AnalyticsUserProperties.Premium, _gameStateService.State.IsPremium ? "true" : "false");
        SetUserProperty(AnalyticsUserProperties.PlayerLevel, _gameStateService.State.PlayerLevel.ToString());
        SetUserProperty(AnalyticsUserProperties.PrestigeTier, _gameStateService.State.Prestige.CurrentTier.ToString());
        SetUserProperty(AnalyticsUserProperties.AscensionLevel, _gameStateService.State.Ascension.AscensionLevel.ToString());
        SetUserProperty(AnalyticsUserProperties.GraphicsQuality, _gameStateService.Settings.GraphicsQuality.ToString());
        SetUserProperty(AnalyticsUserProperties.AppVersion,
            typeof(AnalyticsService).Assembly.GetName().Version?.ToString(3) ?? "unknown");

        // Days-Since-Install aus CreatedAt ableiten (Fallback: 0 wenn nicht gesetzt)
        var installDate = _gameStateService.State.CreatedAt;
        if (installDate > DateTime.MinValue)
        {
            var days = (int)(DateTime.UtcNow - installDate).TotalDays;
            SetUserProperty(AnalyticsUserProperties.DaysSinceInstall, days.ToString());

            // Retention-Event feuern (ein Mal pro Tag)
            var lastRetentionDayKey = "analytics_last_retention_day";
            var lastRetentionDay = _preferences.Get<int>(lastRetentionDayKey, -1);
            if (days != lastRetentionDay)
            {
                _preferences.Set(lastRetentionDayKey, days);
                TrackEvent(AnalyticsEvents.RetentionDay, new Dictionary<string, object?> { ["day"] = days });
            }
        }

        _isEnabled = _gameStateService.Settings.AnalyticsEnabled;
        if (_isEnabled)
        {
            StartFlushTimer();
            TrackEvent(AnalyticsEvents.SessionStart, new Dictionary<string, object?>
            {
                ["session_id"] = _sessionId
            });
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public void TrackEvent(string eventName, Dictionary<string, object?>? parameters = null)
    {
        if (!_isEnabled || _disposed) return;
        if (string.IsNullOrWhiteSpace(eventName)) return;

        var payload = new AnalyticsEventPayload
        {
            EventName = eventName,
            TimestampUtc = DateTime.UtcNow.ToString("O"),
            SessionId = _sessionId,
            Parameters = parameters,
        };

        // Queue-Cap: Aeltestes Event verwerfen wenn voll
        while (_queue.Count >= QueueCap && _queue.TryDequeue(out _)) { }
        _queue.Enqueue(payload);
    }

    /// <inheritdoc />
    public void TrackFunnelStep(string funnelName, int step, string stepName)
    {
        TrackEvent($"funnel_{funnelName}", new Dictionary<string, object?>
        {
            ["step"] = step,
            ["step_name"] = stepName
        });
    }

    /// <inheritdoc />
    public void SetUserProperty(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        lock (_userPropsLock)
        {
            if (value == null) _userProperties.Remove(name);
            else _userProperties[name] = value;
        }
    }

    /// <inheritdoc />
    public async Task FlushAsync()
    {
        if (!_isEnabled || _disposed) return;
        if (_queue.IsEmpty) return;
        if (!_firebase.IsOnline) return;
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return;

        // Nur ein Flush gleichzeitig
        if (!await _flushLock.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false))
            return;

        try
        {
            var batch = new List<AnalyticsEventPayload>(MaxBatchSize);
            while (batch.Count < MaxBatchSize && _queue.TryDequeue(out var ev))
                batch.Add(ev);

            if (batch.Count == 0) return;

            // User-Properties snapshotten (damit alle Events der Batch denselben User-Context haben)
            Dictionary<string, string?> userProps;
            lock (_userPropsLock)
            {
                userProps = new Dictionary<string, string?>(_userProperties);
            }

            var playerId = _firebase.PlayerId!;
            var dateKey = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Updates-Dict fuer einen einzigen PATCH-Request (statt N Requests)
            var updates = new Dictionary<string, object>(batch.Count);
            foreach (var ev in batch)
            {
                var pushId = Guid.NewGuid().ToString("N");
                updates[pushId] = new
                {
                    eventName = ev.EventName,
                    timestamp = ev.TimestampUtc,
                    sessionId = ev.SessionId,
                    playerId,
                    @params = ev.Parameters ?? new Dictionary<string, object?>(),
                    user = userProps
                };
            }

            var path = $"analytics_events/{dateKey}";
            var ok = await _firebase.UpdateAsync(path, updates).ConfigureAwait(false);

            if (!ok)
            {
                // Bei Fehler Events zurueck in die Queue (Queue-Cap wird neu geprueft)
                foreach (var ev in batch)
                {
                    if (_queue.Count >= QueueCap) break;
                    _queue.Enqueue(ev);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Analytics-Flush fehlgeschlagen", ex);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private void StartFlushTimer()
    {
        if (_flushTimer != null) return;
        _flushTimer = new System.Timers.Timer(FlushIntervalSeconds * 1000);
        _flushTimer.Elapsed += OnFlushTimerElapsed;
        _flushTimer.AutoReset = true;
        _flushTimer.Start();
    }

    private void StopFlushTimer()
    {
        if (_flushTimer == null) return;
        _flushTimer.Elapsed -= OnFlushTimerElapsed;
        _flushTimer.Stop();
        _flushTimer.Dispose();
        _flushTimer = null;
    }

    private void OnFlushTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        FlushAsync().SafeFireAndForget();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Letzten Flush best-effort — blockiert Shutdown max. ~2s
        try { FlushAsync().Wait(TimeSpan.FromSeconds(2)); } catch { /* Shutdown: unkritisch */ }
        StopFlushTimer();
        _flushLock.Dispose();
    }

    /// <summary>Interner Queue-Eintrag.</summary>
    private sealed class AnalyticsEventPayload
    {
        public string EventName { get; set; } = "";
        public string TimestampUtc { get; set; } = "";
        public string? SessionId { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
    }
}
