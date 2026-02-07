using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

public class EventService : IEventService
{
    private readonly IGameStateService _gameState;
    private readonly Random _random = new();

    public event EventHandler<GameEvent>? EventStarted;
    public event EventHandler<GameEvent>? EventEnded;

    public GameEvent? ActiveEvent => _gameState.State.ActiveEvent?.IsActive == true
        ? _gameState.State.ActiveEvent
        : null;

    public EventService(IGameStateService gameState)
    {
        _gameState = gameState;
    }

    public void CheckForNewEvent()
    {
        var state = _gameState.State;

        // Clear expired event
        if (state.ActiveEvent != null && !state.ActiveEvent.IsActive)
        {
            var expired = state.ActiveEvent;
            state.ActiveEvent = null;
            EventEnded?.Invoke(this, expired);
        }

        // Don't trigger new event if one is active
        if (state.ActiveEvent != null) return;

        // Check timing: 1-2 events per day, minimum 8h between events
        var hoursSinceLastCheck = (DateTime.UtcNow - state.LastEventCheck).TotalHours;
        if (hoursSinceLastCheck < 8) return;

        state.LastEventCheck = DateTime.UtcNow;

        // 30% chance per check (= ~1-2 events/day with 8h interval)
        if (_random.NextDouble() > 0.30) return;

        // Pick a random event type
        var randomTypes = new[]
        {
            GameEventType.MaterialSale,
            GameEventType.MaterialShortage,
            GameEventType.HighDemand,
            GameEventType.EconomicDownturn,
            GameEventType.TaxAudit,
            GameEventType.WorkerStrike,
            GameEventType.InnovationFair,
            GameEventType.CelebrityEndorsement
        };

        var type = randomTypes[_random.Next(randomTypes.Length)];
        var evt = GameEvent.Create(type);

        state.ActiveEvent = evt;
        state.EventHistory.Add(type.ToString());
        if (state.EventHistory.Count > 20)
            state.EventHistory.RemoveAt(0);

        EventStarted?.Invoke(this, evt);
    }

    public GameEventEffect GetCurrentEffects()
    {
        var effect = new GameEventEffect();

        // Active random event
        if (ActiveEvent != null)
        {
            effect = ActiveEvent.Effect;
        }

        // Seasonal modifier based on real-world month
        var month = DateTime.Now.Month;
        var seasonalMultiplier = month switch
        {
            3 or 4 or 5 => 1.15m,   // Spring: +15%
            6 or 7 or 8 => 1.20m,   // Summer: +20%
            9 or 10 or 11 => 1.10m, // Autumn: +10%
            _ => 0.90m               // Winter: -10%
        };

        // Combine: seasonal adjusts income
        return new GameEventEffect
        {
            IncomeMultiplier = effect.IncomeMultiplier * seasonalMultiplier,
            CostMultiplier = effect.CostMultiplier,
            RewardMultiplier = effect.RewardMultiplier,
            ReputationChange = effect.ReputationChange,
            MarketRestriction = effect.MarketRestriction,
            AffectedWorkshop = effect.AffectedWorkshop,
            SpecialEffect = effect.SpecialEffect
        };
    }
}
