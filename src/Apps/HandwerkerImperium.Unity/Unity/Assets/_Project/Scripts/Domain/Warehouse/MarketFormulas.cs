#nullable enable
using System;
using HandwerkerImperium.Domain.Common;
using HandwerkerImperium.Domain.Crafting;
using HandwerkerImperium.Domain.Events;
using HandwerkerImperium.Domain.State;

namespace HandwerkerImperium.Domain.Warehouse
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>MarketService</c> (Avalonia-Original): die reine, deterministische
    /// Preis-Mathematik des Material-Markts. Pro Spieler + UTC-Tag deterministisch — verhindert
    /// Save-Scumming. Innerhalb des Tages oszilliert der Preis in einer Sinus-Welle (±50%) um den
    /// Basis-Preis.
    ///
    /// Bewusst NICHT extrahiert (state-/warehouse-/analytics-gekoppelt, gehoeren in den Game-Service):
    /// TryBuy/TrySell (Geld-/Inventar-Mutation), das MarketChanged-Event und die Telemetrie.
    ///
    /// Event-Modulatoren:
    /// - <see cref="GameEventType.MaterialShortage"/>: betroffene Workshop-Materialien 3x teurer.
    /// - <see cref="GameEventType.HighDemand"/>: betroffene Workshop-Materialien 2x teurer.
    ///
    /// Spread: Verkaufspreis = Kaufpreis × (1 - <see cref="SpreadFactor"/>) (5% Maklergebuehr — verhindert
    /// Sofort-Arbitrage).
    /// </summary>
    public static class MarketFormulas
    {
        /// <summary>Spread Faktor — Verkaufspreis = Kauf × (1 - Spread).</summary>
        public const decimal SpreadFactor = 0.05m;

        /// <summary>Schwingungs-Amplitude um den Basis-Preis (±50%).</summary>
        public const double DailyAmplitude = 0.50;

        /// <summary>Research-Node die den Markt freischaltet.</summary>
        public const string MarketUnlockResearchId = "logi_05";

        /// <summary>
        /// Markt-Verfuegbarkeit. Premium-Spieler haben die Markt-Insider-Heatmap sofort frei; ohne Pass
        /// schaltet die Forschung <see cref="MarketUnlockResearchId"/> den Markt frei.
        /// <paramref name="researchGatingEnabled"/> entspricht im Original <c>_research != null</c> — ist
        /// kein Research-System vorhanden, ist der Markt immer verfuegbar.
        /// </summary>
        public static bool IsMarketAvailable(GameState state, bool researchGatingEnabled = true)
        {
            if (state.IsPremium) return true;
            if (!researchGatingEnabled) return true;
            for (int i = 0; i < state.Researches.Count; i++)
            {
                if (state.Researches[i].Id == MarketUnlockResearchId && state.Researches[i].IsResearched)
                    return true;
            }
            return false;
        }

        /// <summary>Kaufpreis eines Produkts zum Zeitpunkt <paramref name="utcNow"/> (inkl. Event-Modulator).</summary>
        public static decimal GetBuyPrice(GameState state, string productId, DateTime utcNow)
        {
            var allProducts = CraftingProduct.GetAllProducts();
            if (!allProducts.TryGetValue(productId, out var product)) return 0m;

            decimal basePrice = product.BaseValue;
            double factor = ComputeDailyFactor(state.PlayerGuid, productId, utcNow);
            decimal price = basePrice * (decimal)factor;

            // Event-Modulator
            var activeEvent = state.ActiveEvent;
            if (activeEvent != null)
            {
                var recipe = CraftingRecipe.GetByOutputProduct(productId);
                var matchesWorkshop = recipe != null && activeEvent.Effect.AffectedWorkshop == recipe.WorkshopType;

                if (matchesWorkshop)
                {
                    if (activeEvent.Type == GameEventType.MaterialShortage)
                        price *= 3m;
                    else if (activeEvent.Type == GameEventType.HighDemand)
                        price *= 2m;
                }
            }

            return Math.Max(1m, Math.Round(price));
        }

        /// <summary>Verkaufspreis = Kaufpreis × (1 - Spread), gerundet.</summary>
        public static decimal GetSellPrice(GameState state, string productId, DateTime utcNow) =>
            Math.Round(GetBuyPrice(state, productId, utcNow) * (1m - SpreadFactor));

        /// <summary>
        /// Preis-Trend fuer die naechste Stunde, normalisiert auf [-1, 1]. Positiv = steigend.
        /// </summary>
        public static double GetPriceTrend(GameState state, string productId, DateTime utcNow)
        {
            decimal now = GetBuyPrice(state, productId, utcNow);
            if (now <= 0) return 0;
            var allProducts = CraftingProduct.GetAllProducts();
            if (!allProducts.TryGetValue(productId, out var product)) return 0;

            // Naechste Stunde
            double factorNow = ComputeDailyFactor(state.PlayerGuid, productId, utcNow);
            double factorNext = ComputeDailyFactor(state.PlayerGuid, productId, utcNow.AddHours(1));
            double diff = factorNext - factorNow;
            return Math.Clamp(diff * 2.0, -1.0, 1.0);
        }

        /// <summary>24 stuendliche Kaufpreise ab Tagesbeginn (UTC) — fuer die Preis-Chart-Anzeige.</summary>
        public static decimal[] Get24hPriceSeries(GameState state, string productId, DateTime utcNow)
        {
            var allProducts = CraftingProduct.GetAllProducts();
            if (!allProducts.TryGetValue(productId, out var product)) return new decimal[24];

            var basePrice = product.BaseValue;
            var result = new decimal[24];
            var startOfDay = utcNow.Date;
            for (int h = 0; h < 24; h++)
            {
                double factor = ComputeDailyFactor(state.PlayerGuid, productId, startOfDay.AddHours(h));
                result[h] = Math.Max(1m, Math.Round(basePrice * (decimal)factor));
            }
            return result;
        }

        /// <summary>
        /// Deterministische Tages-Preisfaktor-Berechnung. Pro Material + Tag bekommt jeder Spieler eine
        /// eigene Sinus-Welle mit phasenversetztem Offset. Seed = StableHash(PlayerGuid) ^ Tag-Index ^
        /// StableHash(productId) — <see cref="StableHash"/> statt <c>string.GetHashCode()</c>, weil
        /// Letzteres pro Prozess randomisiert ist und den Preis bei JEDEM Neustart auf eine andere
        /// Sinus-Phase springen liesse (verletzt die zugesicherte Tages-Determinismus-Eigenschaft).
        /// </summary>
        public static double ComputeDailyFactor(string? playerGuid, string productId, DateTime utc)
        {
            string playerKey = playerGuid ?? "anonymous";
            int dayIndex = (int)(utc - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
            int seed = StableHash.Compute(playerKey) ^ dayIndex ^ StableHash.Compute(productId);

            // Phase-Offset aus dem Seed (deterministisch pro Tag/Material/Spieler)
            var rng = new Random(seed);
            double phaseOffset = rng.NextDouble() * Math.PI * 2; // 0..2π

            // Tageszeit als Phase
            double hourFraction = utc.TimeOfDay.TotalHours / 24.0; // 0..1
            double phase = hourFraction * Math.PI * 2 + phaseOffset;

            // Faktor zwischen (1 - amp) und (1 + amp)
            double factor = 1.0 + Math.Sin(phase) * DailyAmplitude;
            return factor;
        }
    }
}
