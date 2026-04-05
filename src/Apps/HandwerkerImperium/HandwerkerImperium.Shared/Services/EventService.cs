using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

public sealed class EventService : IEventService
{
    private readonly IGameStateService _gameState;

    // Dirty-Flag-Cache: Events ändern sich alle paar Minuten, nicht jede Sekunde
    private GameEventEffect? _cachedEffect;
    private bool _effectDirty = true;
    private int _cachedMonth; // Saisonwechsel-Erkennung

    public event EventHandler<GameEvent>? EventStarted;
    public event EventHandler<GameEvent>? EventEnded;

    public GameEvent? ActiveEvent => _gameState.State.ActiveEvent?.IsActive == true
        ? _gameState.State.ActiveEvent
        : null;

    public EventService(IGameStateService gameState)
    {
        _gameState = gameState;

        // Bei State-Wechsel (Prestige/Import/Reset) Effect-Cache invalidieren
        _gameState.StateLoaded += (_, _) => InvalidateEffectCache();
    }

    /// <summary>
    /// Markiert den Effect-Cache als ungültig (aufrufen bei Event-Änderung).
    /// </summary>
    public void InvalidateEffectCache() => _effectDirty = true;

    public void CheckForNewEvent()
    {
        var state = _gameState.State;

        // Clear expired event
        if (state.ActiveEvent != null && !state.ActiveEvent.IsActive)
        {
            var expired = state.ActiveEvent;
            state.ActiveEvent = null;
            _effectDirty = true;
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

        // EVENT-5: Pity-Timer - nach 2 negativen Events in Folge nur positive Events zulassen
        var allRandomTypes = new[]
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

        GameEventType[] randomTypes;
        if (state.ConsecutiveNegativeEvents >= 2)
        {
            // Pity aktiv: Nur positive Events auswählen
            randomTypes = allRandomTypes.Where(t => t.IsPositive()).ToArray();
        }
        else
        {
            randomTypes = allRandomTypes;
        }

        var type = randomTypes[Random.Shared.Next(randomTypes.Length)];
        var evt = GameEvent.Create(type);

        // Pity-Counter aktualisieren
        if (!type.IsPositive())
            state.ConsecutiveNegativeEvents++;
        else
            state.ConsecutiveNegativeEvents = 0;

        state.ActiveEvent = evt;
        state.EventHistory.Add(type.ToString());
        if (state.EventHistory.Count > 20)
            state.EventHistory.RemoveAt(0);

        _effectDirty = true;
        EventStarted?.Invoke(this, evt);
    }

    public GameEventEffect GetCurrentEffects()
    {
        // Bei Monatswechsel Cache invalidieren (saisonaler Multiplikator ändert sich)
        var currentMonth = DateTime.UtcNow.Month;
        if (currentMonth != _cachedMonth)
            _effectDirty = true;

        if (!_effectDirty && _cachedEffect != null)
            return _cachedEffect;

        var effect = new GameEventEffect();

        // Active random event
        if (ActiveEvent != null)
        {
            effect = ActiveEvent.Effect;
        }

        // Saisonaler Modifikator basierend auf aktuellem Monat (UTC)
        var seasonalMultiplier = GetSeasonalMultiplier(DateTime.UtcNow.Month);

        // Kombiniert: Saison beeinflusst Einkommen
        _cachedEffect = new GameEventEffect
        {
            IncomeMultiplier = effect.IncomeMultiplier * seasonalMultiplier,
            CostMultiplier = effect.CostMultiplier,
            RewardMultiplier = effect.RewardMultiplier,
            ReputationChange = effect.ReputationChange,
            MarketRestriction = effect.MarketRestriction,
            AffectedWorkshop = effect.AffectedWorkshop,
            SpecialEffect = effect.SpecialEffect
        };
        _effectDirty = false;
        _cachedMonth = currentMonth;
        return _cachedEffect;
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
