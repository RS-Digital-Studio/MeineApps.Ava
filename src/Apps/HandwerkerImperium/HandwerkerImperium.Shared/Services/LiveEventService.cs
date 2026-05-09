using System.Globalization;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// AAA-Audit P1: LiveEventService — Foundation. RemoteConfig liefert Event-Daten,
/// Service tracked Score + Reward-Auszahlungen.
///
/// RemoteConfig-Schluessel:
/// - <c>live_event.id</c>: stable Event-ID
/// - <c>live_event.template</c>: "DoubleReward" | "BossRush" | "CoopMarathon" | "MiniGameMastery"
/// - <c>live_event.starts_at</c>: ISO-8601-Datum
/// - <c>live_event.ends_at</c>: ISO-8601-Datum
///
/// Aktueller Implementierungs-Status: State-Machine + RemoteConfig-Hook.
/// Der Game-Code muss noch die <see cref="AddScore"/>-Aufrufe einhängen
/// (z.B. nach Order-Complete bei DoubleReward).
/// </summary>
public sealed class LiveEventService : ILiveEventService
{
    private readonly IRemoteConfigService _remoteConfig;
    private readonly IGameStateService _gameState;
    private readonly IAnalyticsService? _analytics;

    public LiveEvent? CurrentEvent { get; private set; }
    public LiveEventTemplate? CurrentTemplate { get; private set; }

    /// <summary>Standard-Reward-Tiers (Audit: 3-Stufen).</summary>
    public IReadOnlyList<int> RewardTierThresholds { get; } = new[] { 100, 500, 2000 };

    public bool IsActive
    {
        get
        {
            if (CurrentEvent == null) return false;
            if (!DateTime.TryParse(CurrentEvent.EndsAtIso, CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind, out var endsAt))
                return false;
            return DateTime.UtcNow < endsAt;
        }
    }

    public event EventHandler<LiveEvent>? EventStarted;
    public event EventHandler<LiveEvent>? EventEnded;

    public LiveEventService(IRemoteConfigService remoteConfig,
                            IGameStateService gameState,
                            IAnalyticsService? analytics = null)
    {
        _remoteConfig = remoteConfig;
        _gameState = gameState;
        _analytics = analytics;
    }

    public Task InitializeAsync()
    {
        try
        {
            var id = _remoteConfig.GetString("live_event.id", "");
            if (string.IsNullOrEmpty(id))
            {
                CurrentEvent = null;
                CurrentTemplate = null;
                return Task.CompletedTask;
            }

            var template = _remoteConfig.GetString("live_event.template", "DoubleReward");
            var starts = _remoteConfig.GetString("live_event.starts_at", "");
            var ends = _remoteConfig.GetString("live_event.ends_at", "");

            // Persistierten State wiederfinden — Score nur fuer dasselbe Event uebernehmen.
            var persisted = _gameState.State.LiveEvent;
            var current = (persisted != null && persisted.Id == id)
                ? persisted
                : new LiveEvent
                {
                    Id = id,
                    TemplateId = template,
                    StartsAtIso = starts,
                    EndsAtIso = ends,
                };

            _gameState.State.LiveEvent = current;
            CurrentEvent = current;
            CurrentTemplate = Enum.TryParse<LiveEventTemplate>(template, true, out var tpl) ? tpl : null;

            if (persisted == null || persisted.Id != id)
            {
                EventStarted?.Invoke(this, current);
                _analytics?.TrackEvent("live_event_started", new Dictionary<string, object?>
                {
                    ["event_id"] = id,
                    ["template"] = template,
                });
            }
        }
        catch
        {
            // RemoteConfig-Probleme dürfen LoadingPipeline nicht crashen.
            CurrentEvent = null;
        }
        return Task.CompletedTask;
    }

    public void AddScore(long points)
    {
        if (CurrentEvent == null || !IsActive || points <= 0) return;
        CurrentEvent.PlayerScore += points;
    }

    public int? TryClaimNextReward()
    {
        if (CurrentEvent == null) return null;
        for (int i = 0; i < RewardTierThresholds.Count; i++)
        {
            if (CurrentEvent.PlayerScore >= RewardTierThresholds[i]
                && !CurrentEvent.ClaimedRewardTiers.Contains(i))
            {
                int rewardScrews = i switch
                {
                    0 => 25,
                    1 => 75,
                    2 => 200,
                    _ => 0
                };
                if (rewardScrews > 0)
                    _gameState.AddGoldenScrews(rewardScrews);

                CurrentEvent.ClaimedRewardTiers.Add(i);
                _analytics?.TrackEvent("live_event_tier_claimed", new Dictionary<string, object?>
                {
                    ["event_id"] = CurrentEvent.Id,
                    ["tier_index"] = i,
                    ["score"] = CurrentEvent.PlayerScore,
                });
                return i;
            }
        }
        return null;
    }

    /// <summary>Wird vom GameLoop aufgerufen — feuert EventEnded wenn das Event abgelaufen ist.</summary>
    public void Tick()
    {
        if (CurrentEvent == null) return;
        if (!IsActive)
        {
            var ended = CurrentEvent;
            CurrentEvent = null;
            CurrentTemplate = null;
            _gameState.State.LiveEvent = null;
            EventEnded?.Invoke(this, ended);
            _analytics?.TrackEvent("live_event_ended", new Dictionary<string, object?>
            {
                ["event_id"] = ended.Id,
                ["final_score"] = ended.PlayerScore,
            });
        }
    }
}
