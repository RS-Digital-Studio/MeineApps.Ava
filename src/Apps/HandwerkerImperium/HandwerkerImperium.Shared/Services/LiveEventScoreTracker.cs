using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verdrahtet <see cref="ILiveEventService.AddScore"/> mit den realen Game-Events
/// (Audit F-01). Bis zu diesem Service wurde AddScore nirgendwo aufgerufen, die
/// LiveOps-Infrastruktur war ungenutzt. Singleton, IDisposable.
///
/// Scoring je Template:
/// - <see cref="LiveEventTemplate.DoubleReward"/>: +1 Punkt pro abgeschlossenem Auftrag (jeder Typ).
/// - <see cref="LiveEventTemplate.CoopMarathon"/>: +1 Punkt pro Cooperation-Auftrag.
/// - <see cref="LiveEventTemplate.MiniGameMastery"/>: +1 Punkt pro Perfect-Rating.
/// - <see cref="LiveEventTemplate.BossRush"/>: aktuell noch nicht verdrahtet —
///   benoetigt einen GuildBoss-Damage-Event-Hook (separates Ticket).
///
/// Reward-Tiers (25/75/200 GS bei 100/500/2000 Score) liegen in <see cref="LiveEventService"/>;
/// dieser Tracker schreibt nur Score.
/// </summary>
public sealed class LiveEventScoreTracker : IDisposable
{
    private readonly ILiveEventService _liveEvent;
    private readonly IGameStateService _gameState;

    public LiveEventScoreTracker(ILiveEventService liveEvent, IGameStateService gameState)
    {
        _liveEvent = liveEvent;
        _gameState = gameState;

        _gameState.OrderCompleted += OnOrderCompleted;
        _gameState.PerfectRatingIncremented += OnPerfectRatingIncremented;
    }

    private void OnOrderCompleted(object? sender, OrderCompletedEventArgs e)
    {
        if (!_liveEvent.IsActive || _liveEvent.CurrentTemplate is not { } template) return;

        switch (template)
        {
            case LiveEventTemplate.DoubleReward:
                _liveEvent.AddScore(1);
                break;
            case LiveEventTemplate.CoopMarathon when e.Order.OrderType == OrderType.Cooperation:
                _liveEvent.AddScore(1);
                break;
            // MiniGameMastery laeuft ueber PerfectRatingIncremented — nicht hier.
            // BossRush wartet auf GuildBoss-Damage-Event-Hook.
        }
    }

    private void OnPerfectRatingIncremented(object? sender, PerfectRatingIncrementedEventArgs e)
    {
        if (!_liveEvent.IsActive || _liveEvent.CurrentTemplate is not { } template) return;
        if (template != LiveEventTemplate.MiniGameMastery) return;
        _liveEvent.AddScore(1);
    }

    public void Dispose()
    {
        _gameState.OrderCompleted -= OnOrderCompleted;
        _gameState.PerfectRatingIncremented -= OnPerfectRatingIncremented;
    }
}
