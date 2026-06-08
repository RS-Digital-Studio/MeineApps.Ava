#nullable enable
using System;
using HandwerkerImperium.Domain;
using HandwerkerImperium.Domain.Research;
using HandwerkerImperium.Domain.Events;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Buildings;
using HandwerkerImperium.Domain.State;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Reine Einkommens-/Kosten-Formeln, extrahiert aus dem Avalonia-Original
    /// (Services/IncomeCalculatorService.cs). Die DI-Service-Abhängigkeiten (Research/Event/VIP/
    /// Manager/EternalMastery/Prestige) werden als Parameter übergeben — die Mathematik (Reihenfolge
    /// der Multiplikationen, Caps, log2-Soft-Cap) ist 1:1 zum Original. Die Game-Schicht füttert die
    /// aggregierten Boni aus den jeweiligen Services.
    /// </summary>
    public static class IncomeFormulas
    {
        /// <summary>Summe aller Heirloom-Boni (aktiver Run + permanent im Ascension-Schrein).</summary>
        public static decimal GetTotalHeirloomBonus(GameState state)
        {
            decimal active = state.HeirloomItems.Count * GameBalanceConstants.HeirloomBonusPerItem;
            decimal permanent = state.Ascension.PermanentHeirlooms.Count * GameBalanceConstants.PermanentHeirloomBonusPerItem;
            return active + permanent;
        }

        /// <summary>Income-Multiplikator-Bonus aus gekauften Prestige-Shop-Items (inkl. wiederholbarer).</summary>
        public static decimal GetPrestigeIncomeBonus(GameState state)
        {
            var purchased = state.Prestige.PurchasedShopItems;
            var repeatableCounts = state.Prestige.RepeatableItemCounts;
            if (purchased.Count == 0 && repeatableCounts.Count == 0) return 0m;

            decimal bonus = 0m;
            foreach (var item in PrestigeShop.GetAllItems())
            {
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

        /// <summary>
        /// Brutto-Einkommen pro Sekunde. <paramref name="masterToolBonus"/> = -1 berechnet aus
        /// state.CollectedMasterTools; <paramref name="managerIncomeBonus"/>/<paramref name="managerEfficiencyBonus"/>
        /// sind die vom ManagerService vor-aggregierten Boni (Workshop-spezifisch + global).
        /// </summary>
        public static decimal CalculateGrossIncome(
            GameState state,
            decimal prestigeIncomeBonus,
            ResearchEffect? researchEffects,
            GameEventEffect? eventEffects,
            decimal masterToolBonus,
            decimal vipIncomeBonus,
            decimal managerIncomeBonus,
            decimal managerEfficiencyBonus,
            decimal eternalMasteryBonus,
            bool eternalMasteryActive)
        {
            decimal grossIncome = state.TotalIncomePerSecond;

            // Prestige-Shop Income-Boni
            if (prestigeIncomeBonus > 0)
                grossIncome *= (1m + prestigeIncomeBonus);

            // Research-Effizienz-Bonus (gekappt bei +50%)
            if (researchEffects != null && researchEffects.EfficiencyBonus > 0)
                grossIncome *= (1m + Math.Min(researchEffects.EfficiencyBonus, 0.50m));

            // Event-Multiplikatoren
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

            // Gilden-Forschungs-Boni + Gildenhallen-Boni
            if (state.GuildMembership != null)
            {
                var gm = state.GuildMembership;
                if (gm.ResearchIncomeBonus > 0)
                    grossIncome *= (1m + gm.ResearchIncomeBonus);
                if (gm.ResearchEfficiencyBonus > 0)
                    grossIncome *= (1m + gm.ResearchEfficiencyBonus);
                if (gm.HallIncomeBonus > 0)
                    grossIncome *= (1m + gm.HallIncomeBonus);
                if (gm.HallEverythingBonus > 0)
                    grossIncome *= (1m + gm.HallEverythingBonus);
            }

            // VIP-Einkommens-Bonus (nach allen anderen Boni, VOR dem Soft-Cap)
            if (vipIncomeBonus > 0)
                grossIncome *= (1m + vipIncomeBonus);

            // Manager-Boni: IncomeBoost + EfficiencyBoost (vor-aggregiert)
            if (managerIncomeBonus > 0)
                grossIncome *= (1m + managerIncomeBonus);
            if (managerEfficiencyBonus > 0)
                grossIncome *= (1m + managerEfficiencyBonus);

            // Premium: +50% Einkommensbonus
            if (state.IsPremium)
                grossIncome *= 1.5m;

            // Eternal Mastery (permanenter Bonus pro Prestige)
            if (eternalMasteryActive)
                grossIncome *= (1m + eternalMasteryBonus);

            // Erbstücke (+2% pro aktivem, +0.5% pro permanentem)
            decimal heirloomBonus = GetTotalHeirloomBonus(state);
            if (heirloomBonus > 0)
                grossIncome *= (1m + heirloomBonus);

            return grossIncome;
        }

        /// <summary>Laufende Kosten pro Sekunde. <paramref name="prestigeCostReduction"/> aus dem PrestigeService.</summary>
        public static decimal CalculateCosts(
            GameState state,
            ResearchEffect? researchEffects,
            GameEventEffect? eventEffects,
            decimal prestigeCostReduction)
        {
            decimal costs = state.TotalCostsPerSecond;

            decimal totalCostReduction = prestigeCostReduction;
            if (researchEffects != null)
                totalCostReduction += researchEffects.CostReduction + researchEffects.WageReduction;

            // Storage-Gebäude: Materialkosten-Reduktion (halb gewichtet)
            var storage = state.GetBuilding(BuildingType.Storage);
            if (storage != null)
                totalCostReduction += storage.MaterialCostReduction * 0.5m;

            if (totalCostReduction > 0)
                costs *= (1m - Math.Min(totalCostReduction, 0.50m)); // Cap bei 50%

            if (eventEffects != null)
                costs *= eventEffects.CostMultiplier;

            if (state.GuildMembership?.ResearchCostReduction > 0)
                costs *= (1m - Math.Min(state.GuildMembership.ResearchCostReduction, 0.50m));

            return costs;
        }

        /// <summary>
        /// Tier-skalierender Soft-Cap auf den effektiven Income-Multiplikator (log2-Dämpfung des
        /// Überschusses). Mutiert state.IsSoftCapActive/SoftCapReductionPercent für die UI-Transparenz.
        /// </summary>
        public static decimal ApplySoftCap(GameState state, decimal grossIncome)
        {
            if (state.TotalIncomePerSecond <= 0) return grossIncome;

            var tier = state.Prestige?.CurrentTier ?? PrestigeTier.None;
            decimal tierThreshold = tier switch
            {
                PrestigeTier.None => 4.0m,
                PrestigeTier.Bronze => 6.0m,
                PrestigeTier.Silver => 8.0m,
                PrestigeTier.Gold => 10.0m,
                PrestigeTier.Platin => 12.0m,
                PrestigeTier.Diamant => 14.0m,
                PrestigeTier.Meister => 16.0m,
                PrestigeTier.Legende => 20.0m,
                _ => 8.0m,
            };

            // Ascension-Floor: kompensiert den Tier-Reset auf None nach Ascension.
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

                if (softened < effectiveMultiplier)
                {
                    state.IsSoftCapActive = true;
                    state.SoftCapReductionPercent = Math.Max(0, (int)Math.Round((1.0m - softened / effectiveMultiplier) * 100m));
                }
                else
                {
                    state.IsSoftCapActive = false;
                    state.SoftCapReductionPercent = 0;
                }
            }
            else
            {
                state.IsSoftCapActive = false;
                state.SoftCapReductionPercent = 0;
            }

            return grossIncome;
        }

        /// <summary>
        /// Crafting-Verkaufs-Multiplikator (durchläuft NICHT CalculateGrossIncome → Premium hier).
        /// Soft-Cap + Hard-Cap aus GameBalanceConstants.
        /// </summary>
        public static decimal CalculateCraftingSellMultiplier(
            GameState state,
            decimal prestigeIncomeBonus,
            decimal rebirthIncomeBonus,
            decimal masterToolBonus,
            ResearchEffect? researchEffects,
            GameEventEffect? eventEffects,
            decimal vipIncomeBonus)
        {
            decimal mult = 1.0m;

            if (prestigeIncomeBonus > 0)
                mult *= (1m + prestigeIncomeBonus);

            if (researchEffects != null && researchEffects.EfficiencyBonus > 0)
                mult *= (1m + Math.Min(researchEffects.EfficiencyBonus, 0.50m));

            if (eventEffects != null)
                mult *= eventEffects.IncomeMultiplier;

            if (eventEffects?.SpecialEffect == "tax_10_percent")
                mult *= 0.90m;

            decimal mtBonus = masterToolBonus >= 0
                ? masterToolBonus
                : MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
            if (mtBonus > 0)
                mult *= (1m + mtBonus);

            if (state.GuildMembership != null)
            {
                var gm = state.GuildMembership;
                if (gm.IncomeBonus > 0)
                    mult *= (1m + gm.IncomeBonus);
                if (gm.ResearchIncomeBonus > 0)
                    mult *= (1m + gm.ResearchIncomeBonus);
                if (gm.ResearchEfficiencyBonus > 0)
                    mult *= (1m + gm.ResearchEfficiencyBonus);
                if (gm.HallIncomeBonus > 0)
                    mult *= (1m + gm.HallIncomeBonus);
                if (gm.HallEverythingBonus > 0)
                    mult *= (1m + gm.HallEverythingBonus);
            }

            if (vipIncomeBonus > 0)
                mult *= (1m + vipIncomeBonus);

            if (rebirthIncomeBonus > 0)
                mult *= (1m + rebirthIncomeBonus);

            if (state.IsPremium)
                mult *= 1.5m;

            if (mult > GameBalanceConstants.CraftingSellMultiplierSoftCap)
            {
                decimal excess = mult - GameBalanceConstants.CraftingSellMultiplierSoftCap;
                mult = GameBalanceConstants.CraftingSellMultiplierSoftCap
                       + (decimal)Math.Log(1.0 + (double)excess, 2.0);
            }
            return Math.Min(mult, GameBalanceConstants.CraftingSellMultiplierHardCap);
        }
    }
}
