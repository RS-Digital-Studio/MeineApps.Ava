#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain;
using HandwerkerImperium.Domain.Crafting;
using HandwerkerImperium.Domain.State;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Eine Lieferanten-Lieferung mit zufälligem Bonus. Erscheint alle 2-5 Minuten, 2 Minuten Abholzeit.
    /// 1:1-Port aus dem Avalonia-Original (Models/SupplierDelivery.cs). DeliveryType-Enum ist in Schicht 10.
    /// GenerateRandom nimmt jetzt eine System.Random-Instanz statt Random.Shared. Icon/DescriptionKey (UI)
    /// wandern in die Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class SupplierDelivery
    {
        [JsonProperty("type")]
        public DeliveryType Type { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        /// <summary>Bei <see cref="DeliveryType.Material"/> die Produkt-ID des Tier-1-Materials (Anzahl in Amount).</summary>
        [JsonProperty("materialProductId")]
        public string? MaterialProductId { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>Ob die Lieferung abgelaufen ist.</summary>
        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        /// <summary>Verbleibende Zeit bis Ablauf.</summary>
        [JsonIgnore]
        public TimeSpan TimeRemaining => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.UtcNow;

        /// <summary>
        /// Generiert eine zufällige Lieferung basierend auf dem aktuellen Spielstand.
        /// <paramref name="rng"/> ersetzt Random.Shared des Originals.
        /// </summary>
        public static SupplierDelivery GenerateRandom(GameState state, Random rng)
        {
            // 25% Chance auf Material-Lieferung ab Auto-Produktion-Unlock-Level.
            bool eligibleForMaterial = state.PlayerLevel >= GameBalanceConstants.AutoProductionUnlockLevel;

            DeliveryType type;
            if (eligibleForMaterial && rng.NextDouble() < 0.25)
            {
                type = DeliveryType.Material;
            }
            else
            {
                type = rng.Next(100) switch
                {
                    < 35 => DeliveryType.Money,
                    < 55 => DeliveryType.GoldenScrews,
                    < 75 => DeliveryType.Experience,
                    < 90 => DeliveryType.MoodBoost,
                    _ => DeliveryType.SpeedBoost
                };
            }

            decimal amount = type switch
            {
                DeliveryType.Money => Math.Max(50m, Math.Round(state.NetIncomePerSecond * rng.Next(60, 180), 0)),
                DeliveryType.GoldenScrews => rng.Next(2, 6),
                DeliveryType.Experience => 20 + state.PlayerLevel * 2 + rng.Next(0, 40),
                DeliveryType.MoodBoost => 10m,
                DeliveryType.SpeedBoost => 30m,
                DeliveryType.Material => rng.Next(1, 11),
                _ => 50m
            };

            var delivery = new SupplierDelivery
            {
                Type = type,
                Amount = amount,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(2)
            };

            // Material-Lieferung: zufälliges Tier-1-Material eines freigeschalteten Workshops
            if (type == DeliveryType.Material)
            {
                var allRecipes = CraftingRecipe.GetAllRecipes();
                var tier1Candidates = new List<string>();
                for (int i = 0; i < allRecipes.Count; i++)
                {
                    var r = allRecipes[i];
                    if (r.Tier == 1 && state.UnlockedWorkshopTypes.Contains(r.WorkshopType))
                        tier1Candidates.Add(r.OutputProductId);
                }
                if (tier1Candidates.Count > 0)
                    delivery.MaterialProductId = tier1Candidates[rng.Next(tier1Candidates.Count)];
                else
                    delivery.MaterialProductId = "planks"; // Fallback
            }

            return delivery;
        }
    }
}
