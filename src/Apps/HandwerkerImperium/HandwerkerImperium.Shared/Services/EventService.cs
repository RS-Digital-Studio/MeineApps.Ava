using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

public sealed class EventService : IEventService
{
    private readonly IGameStateService _gameState;

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

        // Event-Intervalle + Chance skalieren nach Prestige
        int prestigeCount = state.Prestige?.TotalPrestigeCount ?? 0;
        (double minHours, double chance) = prestigeCount switch
        {
            0 => (8.0, 0.30),    // Kein Prestige: 8h, 30%
            1 => (6.0, 0.35),    // Bronze: 6h, 35%
            2 => (4.0, 0.40),    // Silver: 4h, 40%
            _ => (3.0, 0.50)     // Gold+: 3h, 50%
        };

        var hoursSinceLastCheck = (DateTime.UtcNow - state.LastEventCheck).TotalHours;
        if (hoursSinceLastCheck < minHours) return;

        state.LastEventCheck = DateTime.UtcNow;

        if (Random.Shared.NextDouble() > chance) return;

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

        var type = randomTypes[Random.Shared.Next(randomTypes.Length)];
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

        // Saisonaler Modifikator basierend auf aktuellem Monat (UTC)
        var seasonalMultiplier = GetSeasonalMultiplier(DateTime.UtcNow.Month);

        // Kombiniert: Saison beeinflusst Einkommen
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

    /// <summary>
    /// Berechnet den saisonalen Multiplikator für einen Monat.
    /// Zentralisiert, damit OfflineProgressService die gleiche Formel nutzt.
    /// </summary>
    public static decimal GetSeasonalMultiplier(int month) => month switch
    {
        3 or 4 or 5 => 1.15m,   // Frühling: +15%
        6 or 7 or 8 => 1.20m,   // Sommer: +20%
        9 or 10 or 11 => 1.10m, // Herbst: +10%
        _ => 0.90m               // Winter: -10%
    };
}
