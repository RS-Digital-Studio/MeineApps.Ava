#nullable enable
using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.State;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>AutoProductionService</c> (Avalonia-Original): die reinen
    /// Auto-Produktions-Parameter (Produkt-Mapping, Intervall, Freischaltung) + die Offline-Produktions-
    /// Berechnung. 1:1 zur Vorlage.
    ///
    /// Bewusst NICHT extrahiert (state-mutierend / WarehouseService-gekoppelt, gehoeren in den Game-Service):
    /// ProduceForAllWorkshops, AutoCraftHigherTiers/TryAutoCraftRecipe (mutieren CraftingInventory + Statistik).
    /// </summary>
    public static class AutoProductionFormulas
    {
        /// <summary>Mapping WorkshopType -> Tier-1 Produkt-ID fuer Auto-Produktion.</summary>
        private static readonly Dictionary<WorkshopType, string> Tier1Products = new Dictionary<WorkshopType, string>
        {
            [WorkshopType.Carpenter] = "planks",
            [WorkshopType.Plumber] = "pipes",
            [WorkshopType.Electrician] = "cables",
            [WorkshopType.Painter] = "paint_mix",
            [WorkshopType.Roofer] = "roof_tiles",
            [WorkshopType.Contractor] = "concrete",
            [WorkshopType.Architect] = "blueprint",
            [WorkshopType.GeneralContractor] = "contract",
            [WorkshopType.MasterSmith] = "fittings",
            [WorkshopType.InnovationLab] = "prototype",
        };

        /// <summary>Tier-1 Produkt-ID eines Workshop-Typs (oder <c>null</c>, falls nicht gemappt).</summary>
        public static string? GetTier1ProductId(WorkshopType type) => Tier1Products.GetValueOrDefault(type);

        /// <summary>Produktions-Intervall (Sekunden je Item je Worker): MasterSmith 60, InnovationLab 120, sonst 180.</summary>
        public static int GetProductionInterval(WorkshopType type) => type switch
        {
            WorkshopType.MasterSmith => GameBalanceConstants.AutoProductionMasterSmithInterval,
            WorkshopType.InnovationLab => GameBalanceConstants.AutoProductionInnovationLabInterval,
            _ => GameBalanceConstants.AutoProductionIntervalSeconds,
        };

        /// <summary>Auto-Produktion ist ab Workshop-Level <see cref="GameBalanceConstants.AutoProductionUnlockLevel"/> (50) frei.</summary>
        public static bool IsAutoProductionUnlocked(Workshop workshop) =>
            workshop.Level >= GameBalanceConstants.AutoProductionUnlockLevel;

        /// <summary>
        /// Effektive Offline-Sekunden mit Staffelung 80%/35%/15%/5% (erste 2h / 2-4h / 4-8h / 8h+).
        /// Identisch zur Offline-Earnings-Staffelung.
        /// </summary>
        public static double CalculateEffectiveOfflineSeconds(double totalSeconds)
        {
            double effective = 0;
            double remaining = totalSeconds;

            double first2h = Math.Min(remaining, 7200);
            effective += first2h * 0.80;
            remaining -= first2h;
            if (remaining <= 0) return effective;

            double next2h = Math.Min(remaining, 7200);
            effective += next2h * 0.35;
            remaining -= next2h;
            if (remaining <= 0) return effective;

            double next4h = Math.Min(remaining, 14400);
            effective += next4h * 0.15;
            remaining -= next4h;
            if (remaining <= 0) return effective;

            effective += remaining * 0.05;
            return effective;
        }

        /// <summary>
        /// Offline passiv produzierte Tier-1-Items je Produkt-ID. Read-only ueber den GameState — KEINE Mutation.
        /// Das effektive Stack-Limit (inkl. Logistik-Forschungs-Multiplikator) wird als Parameter uebergeben
        /// (Original: <c>_warehouse?.CurrentStackLimit ?? state.WarehouseStackLimit</c>), damit der Extrakt
        /// frei vom WarehouseService bleibt.
        /// </summary>
        public static Dictionary<string, int> CalculateOfflineProduction(GameState state, double offlineSeconds, int effectiveStackLimit)
        {
            var produced = new Dictionary<string, int>();
            if (offlineSeconds <= 0) return produced;

            double effectiveSeconds = CalculateEffectiveOfflineSeconds(offlineSeconds);

            for (int i = 0; i < state.Workshops.Count; i++)
            {
                var workshop = state.Workshops[i];
                if (!IsAutoProductionUnlocked(workshop)) continue;

                var productId = GetTier1ProductId(workshop.Type);
                if (productId == null) continue;

                int workingWorkers = 0;
                for (int w = 0; w < workshop.Workers.Count; w++)
                    if (workshop.Workers[w].IsWorking) workingWorkers++;
                if (workingWorkers <= 0) continue;

                int interval = GetProductionInterval(workshop.Type);
                int itemsProduced = (int)(effectiveSeconds / interval * workingWorkers);
                if (itemsProduced <= 0) continue;

                int current = state.CraftingInventory.GetValueOrDefault(productId, 0);
                int cap = effectiveStackLimit - current;
                if (cap <= 0) continue;
                if (itemsProduced > cap) itemsProduced = cap;

                if (produced.ContainsKey(productId))
                    produced[productId] += itemsProduced;
                else
                    produced[productId] = itemsProduced;
            }

            return produced;
        }
    }
}
