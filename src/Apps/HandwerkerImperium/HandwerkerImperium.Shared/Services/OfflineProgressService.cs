using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Berechnet Offline-Einnahmen mit denselben Modifikatoren wie der GameLoop:
/// Research-Effizienz, Prestige-Shop-Income, Meisterwerkzeuge, Saison-Multiplikator.
/// Kosten werden ebenfalls berücksichtigt (Research/Prestige-Shop CostReduction).
/// </summary>
public class OfflineProgressService : IOfflineProgressService
{
    private readonly IGameStateService _gameStateService;
    private readonly IEventService? _eventService;
    private readonly IResearchService? _researchService;
    private readonly IPrestigeService? _prestigeService;

    public event EventHandler<OfflineEarningsEventArgs>? OfflineEarningsCalculated;

    public OfflineProgressService(
        IGameStateService gameStateService,
        IEventService? eventService = null,
        IResearchService? researchService = null,
        IPrestigeService? prestigeService = null)
    {
        _gameStateService = gameStateService;
        _eventService = eventService;
        _researchService = researchService;
        _prestigeService = prestigeService;
    }

    public decimal CalculateOfflineProgress()
    {
        var state = _gameStateService.State;

        // Offline-Dauer berechnen
        var offlineDuration = GetOfflineDuration();
        if (offlineDuration.TotalSeconds < 60) // Unter 1 Minute, keine Belohnung
        {
            return 0;
        }

        // Offline-Dauer begrenzen
        var maxDuration = GetMaxOfflineDuration();
        bool wasCapped = offlineDuration > maxDuration;
        var effectiveDuration = wasCapped ? maxDuration : offlineDuration;

        // === Research-Effekte laden + Level-Resistenz-Bonus auf Workshops setzen ===
        var researchEffects = _researchService?.GetTotalEffects();
        decimal levelResistance = Math.Min(researchEffects?.LevelResistanceBonus ?? 0m, 0.50m);
        foreach (var ws in state.Workshops)
            ws.LevelResistanceBonus = levelResistance;

        // === Brutto-Einkommen berechnen (wie GameLoop) ===
        decimal grossIncome = state.TotalIncomePerSecond;

        // Prestige-Shop Income-Boni (pp_income_10/25/50)
        decimal shopIncomeBonus = GetPrestigeIncomeBonus(state);
        if (shopIncomeBonus > 0)
            grossIncome *= (1m + shopIncomeBonus);
        if (researchEffects != null && researchEffects.EfficiencyBonus > 0)
            grossIncome *= (1m + Math.Min(researchEffects.EfficiencyBonus, 0.50m));

        // Meisterwerkzeuge: Passiver Einkommens-Bonus
        decimal masterToolBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        if (masterToolBonus > 0)
            grossIncome *= (1m + masterToolBonus);

        // Gilden-Bonus: +1% pro Gilden-Level, max +20%
        if (state.GuildMembership != null && state.GuildMembership.IncomeBonus > 0)
            grossIncome *= (1m + state.GuildMembership.IncomeBonus);

        // Hard-Cap: Gesamt-Einkommens-Multiplikator auf +200% begrenzen (3.0x)
        // Analog zum GameLoopService
        if (state.TotalIncomePerSecond > 0)
        {
            decimal effectiveMultiplier = grossIncome / state.TotalIncomePerSecond;
            if (effectiveMultiplier > 3.0m)
                grossIncome = state.TotalIncomePerSecond * 3.0m;
        }

        // === Kosten berechnen (wie GameLoop) ===
        decimal costs = state.TotalCostsPerSecond;

        decimal totalCostReduction = 0m;
        if (_prestigeService != null)
            totalCostReduction += _prestigeService.GetCostReduction();
        if (researchEffects != null)
            totalCostReduction += researchEffects.CostReduction + researchEffects.WageReduction;

        // Storage-Gebäude: Materialkosten-Reduktion
        var storage = state.GetBuilding(BuildingType.Storage);
        if (storage != null)
            totalCostReduction += storage.MaterialCostReduction * 0.5m;

        if (totalCostReduction > 0)
            costs *= (1m - Math.Min(totalCostReduction, 0.50m));

        // Netto-Einkommen (mindestens 0 - offline kein Geld verlieren)
        decimal netPerSecond = Math.Max(0, grossIncome - costs);
        decimal earnings = netPerSecond * (decimal)effectiveDuration.TotalSeconds;

        // Saisonaler Multiplikator
        var month = DateTime.UtcNow.Month;
        decimal seasonalMultiplier = EventService.GetSeasonalMultiplier(month);
        earnings *= seasonalMultiplier;

        // Event feuern
        OfflineEarningsCalculated?.Invoke(this, new OfflineEarningsEventArgs(
            earnings,
            effectiveDuration,
            wasCapped));

        return earnings;
    }

    public TimeSpan GetMaxOfflineDuration()
    {
        int maxHours = _gameStateService.State.MaxOfflineHours;
        return TimeSpan.FromHours(maxHours);
    }

    public TimeSpan GetOfflineDuration()
    {
        var lastPlayed = _gameStateService.State.LastPlayedAt;
        var now = DateTime.UtcNow;

        // Zeitmanipulations-Schutz: Wenn lastPlayed in der Zukunft liegt,
        // wurde die Systemuhr zurückgestellt → keine Offline-Einnahmen
        if (lastPlayed > now)
            return TimeSpan.Zero;

        return now - lastPlayed;
    }

    /// <summary>
    /// Berechnet Income-Multiplikator-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private static decimal GetPrestigeIncomeBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return 0m;

        decimal bonus = 0m;
        foreach (var item in PrestigeShop.GetAllItems())
        {
            if (purchased.Contains(item.Id) && item.Effect.IncomeMultiplier > 0)
                bonus += item.Effect.IncomeMultiplier;
        }
        return bonus;
    }
}
