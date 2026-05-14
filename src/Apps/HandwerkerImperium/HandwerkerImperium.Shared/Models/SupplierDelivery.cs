using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Eine Lieferanten-Lieferung mit zufälligem Bonus.
/// Erscheint alle 2-5 Minuten und muss innerhalb von 2 Minuten abgeholt werden.
/// </summary>
public class SupplierDelivery
{
    [JsonPropertyName("type")]
    public DeliveryType Type { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// V7 (): Bei <see cref="DeliveryType.Material"/> die Produkt-ID
    /// des gelieferten Tier-1-Materials. Anzahl steht in <see cref="Amount"/>.
    /// </summary>
    [JsonPropertyName("materialProductId")]
    public string? MaterialProductId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Ob die Lieferung abgelaufen ist.
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Verbleibende Zeit bis Ablauf.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TimeRemaining => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.UtcNow;

    /// <summary>
    /// Icon basierend auf Lieferungstyp.
    /// </summary>
    [JsonIgnore]
    public string Icon => Type switch
    {
        DeliveryType.Money => "Cash",
        DeliveryType.GoldenScrews => "Screwdriver",
        DeliveryType.Experience => "Star",
        DeliveryType.MoodBoost => "EmoticonHappy",
        DeliveryType.SpeedBoost => "LightningBolt",
        DeliveryType.Material => "PackageVariant",
        _ => "PackageVariant"
    };

    /// <summary>
    /// Lokalisierungs-Key für die Beschreibung.
    /// </summary>
    [JsonIgnore]
    public string DescriptionKey => Type switch
    {
        DeliveryType.Money => "DeliveryMoney",
        DeliveryType.GoldenScrews => "DeliveryGoldenScrews",
        DeliveryType.Experience => "DeliveryExperience",
        DeliveryType.MoodBoost => "DeliveryMoodBoost",
        DeliveryType.SpeedBoost => "DeliverySpeedBoost",
        DeliveryType.Material => "DeliveryMaterial",
        _ => "DeliveryMoney"
    };

    /// <summary>
    /// Generiert eine zufällige Lieferung basierend auf dem aktuellen Spielstand.
    /// </summary>
    public static SupplierDelivery GenerateRandom(GameState state)
    {
        var random = Random.Shared;

        // V7 (): 25% Chance auf Material-Lieferung — verdraengt die
        // Geld-Lieferung als haeufigsten Typ. Nur sinnvoll wenn Spieler den Auto-Production-Unlock
        // bereits hat (Level >= 50), sonst kein Material-Sinn → faellt zurueck auf alte Logik.
        bool eligibleForMaterial = state.PlayerLevel >= GameBalanceConstants.AutoProductionUnlockLevel;

        // Gewichtete Auswahl: Geld haeufiger, SpeedBoost seltener
        DeliveryType type;
        if (eligibleForMaterial && random.NextDouble() < 0.25)
        {
            type = DeliveryType.Material;
        }
        else
        {
            type = random.Next(100) switch
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
            // 1-3 Minuten Einkommen (Minimum 50)
            DeliveryType.Money => Math.Max(50m, Math.Round(
                state.NetIncomePerSecond * random.Next(60, 180), 0)),
            // 2-5 GS (verbessert von 1-3)
            DeliveryType.GoldenScrews => random.Next(2, 6),
            // 20-100 XP (skaliert mit Level)
            DeliveryType.Experience => 20 + state.PlayerLevel * 2 + random.Next(0, 40),
            // Mood +10 für alle Worker
            DeliveryType.MoodBoost => 10m,
            // 30min Speed-Boost
            DeliveryType.SpeedBoost => 30m,
            // Material: 1-10 Tier-1-Items eines zufaelligen Workshops
            DeliveryType.Material => random.Next(1, 11),
            _ => 50m
        };

        var delivery = new SupplierDelivery
        {
            Type = type,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(2) // 2 Minuten zum Abholen
        };

        // Material-Lieferung: zufaelliges Tier-1-Material eines freigeschalteten Workshops
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
                delivery.MaterialProductId = tier1Candidates[random.Next(tier1Candidates.Count)];
            else
                delivery.MaterialProductId = "planks"; // Fallback
        }

        return delivery;
    }
}
