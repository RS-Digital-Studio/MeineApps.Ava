using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Zentrale Einkommens- und Kostenberechnung.
/// Wird von GameLoopService (pro Tick) und OfflineProgressService (bei App-Start) genutzt.
/// Eliminiert die duplizierte Logik und stellt sicher dass beide immer synchron sind.
/// </summary>
public sealed class IncomeCalculatorService : IIncomeCalculatorService
{
    private readonly IEventService? _eventService;
    private readonly IResearchService? _researchService;
    private readonly IPrestigeService? _prestigeService;
    private readonly IVipService? _vipService;

    private const decimal SoftCapThreshold = 8.0m;

    public IncomeCalculatorService(
        IEventService? eventService = null,
        IResearchService? researchService = null,
        IPrestigeService? prestigeService = null,
        IVipService? vipService = null)
    {
        _eventService = eventService;
        _researchService = researchService;
        _prestigeService = prestigeService;
        _vipService = vipService;
    }

    public decimal CalculateGrossIncome(GameState state, decimal prestigeIncomeBonus, decimal masterToolBonus = -1m,
        ResearchEffect? researchEffects = null, GameEventEffect? eventEffects = null)
    {
        decimal grossIncome = state.TotalIncomePerSecond;

        // Prestige-Shop Income-Boni
        if (prestigeIncomeBonus > 0)
            grossIncome *= (1m + prestigeIncomeBonus);

        // Research-Effizienz-Bonus (gekappt bei +50%)
        researchEffects ??= _researchService?.GetTotalEffects();
        if (researchEffects != null && researchEffects.EfficiencyBonus > 0)
            grossIncome *= (1m + Math.Min(researchEffects.EfficiencyBonus, 0.50m));

        // Event-Multiplikatoren
        eventEffects ??= _eventService?.GetCurrentEffects();
        if (eventEffects != null)
            grossIncome *= eventEffects.IncomeMultiplier;

        // TaxAudit: 10% Steuer auf Einkommen
        if (eventEffects?.SpecialEffect == "tax_10_percent")
            grossIncome *= 0.90m;

        // Meisterwerkzeuge: Passiver Einkommens-Bonus
        decimal mtBonus = masterToolBonus >= 0
            ? masterToolBonus
            : MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        if (mtBonus > 0)
            grossIncome *= (1m + mtBonus);

        // Gilden-Bonus: +1% pro Gilden-Level, max +20%
        if (state.GuildMembership != null && state.GuildMembership.IncomeBonus > 0)
            grossIncome *= (1m + state.GuildMembership.IncomeBonus);

        // Gilden-Forschungs-Boni
        if (state.GuildMembership != null)
        {
            var gm = state.GuildMembership;

            if (gm.ResearchIncomeBonus > 0)
                grossIncome *= (1m + gm.ResearchIncomeBonus);

            if (gm.ResearchEfficiencyBonus > 0)
                grossIncome *= (1m + gm.ResearchEfficiencyBonus);
        }

        // VIP-Einkommens-Bonus (nach allen anderen Boni, VOR dem Soft-Cap)
        if (_vipService != null)
        {
            decimal vipIncomeBonus = _vipService.IncomeBonus;
            if (vipIncomeBonus > 0)
                grossIncome *= (1m + vipIncomeBonus);
        }

        return grossIncome;
    }

    public decimal CalculateCosts(GameState state, ResearchEffect? researchEffects = null, GameEventEffect? eventEffects = null)
    {
        decimal costs = state.TotalCostsPerSecond;

        // Research + Prestige CostReduction + WageReduction
        decimal totalCostReduction = 0m;
        if (_prestigeService != null)
            totalCostReduction += _prestigeService.GetCostReduction();

        researchEffects ??= _researchService?.GetTotalEffects();
        if (researchEffects != null)
            totalCostReduction += researchEffects.CostReduction + researchEffects.WageReduction;

        // Storage-Gebäude: Materialkosten-Reduktion
        var storage = state.GetBuilding(BuildingType.Storage);
        if (storage != null)
            totalCostReduction += storage.MaterialCostReduction * 0.5m;

        if (totalCostReduction > 0)
            costs *= (1m - Math.Min(totalCostReduction, 0.50m)); // Cap bei 50%

        // Event-Kosteneffekte
        eventEffects ??= _eventService?.GetCurrentEffects();
        if (eventEffects != null)
            costs *= eventEffects.CostMultiplier;

        // Gilden-Forschungs-Boni: Kosten-Reduktion
        if (state.GuildMembership?.ResearchCostReduction > 0)
            costs *= (1m - Math.Min(state.GuildMembership.ResearchCostReduction, 0.50m));

        return costs;
    }

    public decimal ApplySoftCap(GameState state, decimal grossIncome)
    {
        if (state.TotalIncomePerSecond <= 0) return grossIncome;

        decimal effectiveMultiplier = grossIncome / state.TotalIncomePerSecond;
        if (effectiveMultiplier > SoftCapThreshold)
        {
            decimal excess = effectiveMultiplier - SoftCapThreshold;
            decimal softened = SoftCapThreshold + (decimal)Math.Log(1.0 + (double)excess, 2.0);
            grossIncome = state.TotalIncomePerSecond * softened;

            // Soft-Cap-Info für UI-Transparenz
            state.IsSoftCapActive = true;
            state.SoftCapReductionPercent = (int)Math.Round((1.0m - softened / effectiveMultiplier) * 100m);
        }
        else
        {
            state.IsSoftCapActive = false;
            state.SoftCapReductionPercent = 0;
        }

        return grossIncome;
    }

    public decimal CalculateCraftingSellMultiplier(GameState state, decimal prestigeIncomeBonus, decimal rebirthIncomeBonus, decimal masterToolBonus = -1m)
    {
        decimal mult = 1.0m;

        // Prestige-Shop Income-Boni
        if (prestigeIncomeBonus > 0)
            mult *= (1m + prestigeIncomeBonus);

        // Research-Effizienz-Bonus (gekappt bei +50%)
        var researchEffects = _researchService?.GetTotalEffects();
        if (researchEffects != null && researchEffects.EfficiencyBonus > 0)
            mult *= (1m + Math.Min(researchEffects.EfficiencyBonus, 0.50m));

        // Event-Multiplikatoren
        var eventEffects = _eventService?.GetCurrentEffects();
        if (eventEffects != null)
            mult *= eventEffects.IncomeMultiplier;

        // TaxAudit: 10% Steuer
        if (eventEffects?.SpecialEffect == "tax_10_percent")
            mult *= 0.90m;

        // Meisterwerkzeuge
        decimal mtBonus = masterToolBonus >= 0
            ? masterToolBonus
            : MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        if (mtBonus > 0)
            mult *= (1m + mtBonus);

        // Gilden-Boni
        if (state.GuildMembership != null)
        {
            if (state.GuildMembership.IncomeBonus > 0)
                mult *= (1m + state.GuildMembership.IncomeBonus);
            if (state.GuildMembership.ResearchIncomeBonus > 0)
                mult *= (1m + state.GuildMembership.ResearchIncomeBonus);
            if (state.GuildMembership.ResearchEfficiencyBonus > 0)
                mult *= (1m + state.GuildMembership.ResearchEfficiencyBonus);
        }

        // VIP-Einkommens-Bonus
        if (_vipService != null)
        {
            decimal vipBonus = _vipService.IncomeBonus;
            if (vipBonus > 0)
                mult *= (1m + vipBonus);
        }

        // Workshop-Rebirth-Bonus
        if (rebirthIncomeBonus > 0)
            mult *= (1m + rebirthIncomeBonus);

        // Premium: +50%
        if (state.IsPremium)
            mult *= 1.5m;

        // KEIN Soft-Cap, KEIN Speed/Rush → bewusst weggelassen
        return mult;
    }
}
