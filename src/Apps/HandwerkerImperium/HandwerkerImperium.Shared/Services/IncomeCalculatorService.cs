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
    private readonly IManagerService? _managerService;
    private readonly IEternalMasteryService? _eternalMastery;

    private const decimal SoftCapThreshold = 8.0m;

    public IncomeCalculatorService(
        IEventService? eventService = null,
        IResearchService? researchService = null,
        IPrestigeService? prestigeService = null,
        IVipService? vipService = null,
        IManagerService? managerService = null,
        IEternalMasteryService? eternalMastery = null)
    {
        _eventService = eventService;
        _researchService = researchService;
        _prestigeService = prestigeService;
        _vipService = vipService;
        _managerService = managerService;
        _eternalMastery = eternalMastery;
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

        // Manager-Boni: IncomeBoost + EfficiencyBoost aller freigeschalteten Manager
        if (_managerService != null)
        {
            decimal totalManagerIncome = 0m;
            decimal totalManagerEfficiency = 0m;
            // Workshop-spezifische Manager
            for (int i = 0; i < state.Workshops.Count; i++)
            {
                var wsType = state.Workshops[i].Type;
                totalManagerIncome += _managerService.GetManagerBonusForWorkshop(wsType, ManagerAbility.IncomeBoost);
                totalManagerEfficiency += _managerService.GetManagerBonusForWorkshop(wsType, ManagerAbility.EfficiencyBoost);
            }
            // Globale Manager
            totalManagerIncome += _managerService.GetGlobalManagerBonus(ManagerAbility.IncomeBoost);
            totalManagerEfficiency += _managerService.GetGlobalManagerBonus(ManagerAbility.EfficiencyBoost);

            if (totalManagerIncome > 0)
                grossIncome *= (1m + totalManagerIncome);
            if (totalManagerEfficiency > 0)
                grossIncome *= (1m + totalManagerEfficiency);
        }

        // Premium: +50% Einkommensbonus
        if (state.IsPremium)
            grossIncome *= 1.5m;

        // AAA-Audit P1 Long-Term-Engagement: Eternal Mastery (permanenter Bonus pro Prestige)
        if (_eternalMastery != null && _eternalMastery.IsActive)
        {
            grossIncome *= (1m + _eternalMastery.IncomeBonus);
        }

        // V7 (Phase 4 Ressourcen-Plan): Erbstuecke (+2% Globales Einkommen pro aktivem Erbstueck,
        // +0.5% Globales Einkommen pro permanentem Erbstueck im Ascension-Schrein).
        decimal heirloomBonus = GetTotalHeirloomBonus(state);
        if (heirloomBonus > 0)
            grossIncome *= (1m + heirloomBonus);

        return grossIncome;
    }

    /// <summary>
    /// V7 (Phase 4): Summe aller Heirloom-Boni (aktiver Run + permanent).
    /// Public-static damit andere Services (Header-Display, Achievements) den Bonus auch ablesen koennen.
    /// </summary>
    public static decimal GetTotalHeirloomBonus(GameState state)
    {
        decimal active = state.HeirloomItems.Count * GameBalanceConstants.HeirloomBonusPerItem;
        decimal permanent = state.Ascension.PermanentHeirlooms.Count * GameBalanceConstants.PermanentHeirloomBonusPerItem;
        return active + permanent;
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

        // Fix 18.04.2026 Game-Audit: Tier-skalierender Soft-Cap. Der bisherige harte 8x-Cap
        // kollidierte bereits mit dem Legende-Tier-Multi (+800% = 9x) — alle Prestige-Shop-,
        // Rebirth- und Premium-Upgrades waren danach komprimiert und fuehlten sich wertlos an.
        // Jetzt skaliert die Schwelle mit dem erreichten Prestige-Tier:
        //   Kein Prestige: 4x  (Early-Game soll nicht ueber Balance schiessen)
        //   Bronze:        6x
        //   Silver:        8x
        //   Gold:         10x
        //   Platin:       12x
        //   Diamant:      14x
        //   Meister:      16x
        //   Legende:      20x  (knapp 2x ueber Tier-Multi → Late-Game-Upgrades wirken wieder)
        var tier = state.Prestige?.CurrentTier ?? PrestigeTier.None;
        decimal tierThreshold = tier switch
        {
            PrestigeTier.None    => 4.0m,
            PrestigeTier.Bronze  => 6.0m,
            PrestigeTier.Silver  => 8.0m,
            PrestigeTier.Gold    => 10.0m,
            PrestigeTier.Platin  => 12.0m,
            PrestigeTier.Diamant => 14.0m,
            PrestigeTier.Meister => 16.0m,
            PrestigeTier.Legende => 20.0m,
            _ => 8.0m,
        };

        // v2.1.1 (Audit B-H01): Ascension setzt Prestige.CurrentTier auf None zurueck → der tier-basierte
        // Threshold faellt nach jeder Ascension brutal von 20x auf 4x (gefuehlte Income-
        // Halbierung post-Ascension). Ein Ascension-basierter Floor kompensiert das:
        // AscensionLevel 1 haelt mindestens das Legende-Niveau (20x), danach +2x pro Level.
        decimal ascensionThreshold = state.Ascension.AscensionLevel > 0
            ? Math.Min(18.0m + state.Ascension.AscensionLevel * 2.0m, 30.0m)
            : 0m;
        decimal threshold = Math.Max(tierThreshold, ascensionThreshold);

        decimal effectiveMultiplier = grossIncome / state.TotalIncomePerSecond;
        if (effectiveMultiplier > threshold)
        {
            decimal excess = effectiveMultiplier - threshold;
            decimal softened = threshold + (decimal)Math.Log(1.0 + (double)excess, 2.0);
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

    /// <summary>
    /// Berechnet den Income-Multiplikator-Bonus aus gekauften Prestige-Shop-Items.
    /// Zentrale Methode - wird von OfflineProgressService und CraftingService genutzt.
    /// </summary>
    public static decimal GetPrestigeIncomeBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        var repeatableCounts = state.Prestige.RepeatableItemCounts;
        if (purchased.Count == 0 && repeatableCounts.Count == 0) return 0m;

        decimal bonus = 0m;
        foreach (var item in PrestigeShop.GetAllItems())
        {
            // Wiederholbare Items: Effekt * Kaufanzahl
            if (item.IsRepeatable)
            {
                if (repeatableCounts.TryGetValue(item.Id, out var count) && count > 0
                    && item.Effect.IncomeMultiplier > 0)
                    bonus += item.Effect.IncomeMultiplier * count;
                continue;
            }

            if (purchased.Contains(item.Id) && item.Effect.IncomeMultiplier > 0)
                bonus += item.Effect.IncomeMultiplier;
        }
        return bonus;
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

        // Premium: +50% Einkommensbonus (analog zu CalculateGrossIncome)
        // Crafting-Verkäufe durchlaufen NICHT CalculateGrossIncome → Bonus hier anwenden
        if (state.IsPremium)
            mult *= 1.5m;

        // v2.1.1 (Audit B-C03): Soft-Cap auf den Crafting-Sell-Multiplikator. Frueher gab es KEIN Cap —
        // der Multiplikator konnte im Late-Game >20x werden. Da T4-Items im Lager hortbar
        // sind, konnte der Spieler einen exponentiellen Geld-Pump aufbauen, der die
        // currentRunMoney-basierte PP-Formel untergraebt. Ueberschuss wird logarithmisch
        // gedaempft (analog ApplySoftCap), hart gedeckelt bei CraftingSellMultiplierHardCap.
        if (mult > GameBalanceConstants.CraftingSellMultiplierSoftCap)
        {
            decimal excess = mult - GameBalanceConstants.CraftingSellMultiplierSoftCap;
            mult = GameBalanceConstants.CraftingSellMultiplierSoftCap
                   + (decimal)Math.Log(1.0 + (double)excess, 2.0);
        }
        return Math.Min(mult, GameBalanceConstants.CraftingSellMultiplierHardCap);
    }
}
