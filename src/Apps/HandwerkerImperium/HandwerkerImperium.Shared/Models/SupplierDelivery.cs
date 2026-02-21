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
        _ => "DeliveryMoney"
    };

    /// <summary>
    /// Generiert eine zufällige Lieferung basierend auf dem aktuellen Spielstand.
    /// </summary>
    public static SupplierDelivery GenerateRandom(GameState state)
    {
        var random = Random.Shared;

        // Gewichtete Auswahl: Geld häufiger, SpeedBoost seltener
        var type = random.Next(100) switch
        {
            < 35 => DeliveryType.Money,
            < 55 => DeliveryType.GoldenScrews,
            < 75 => DeliveryType.Experience,
            < 90 => DeliveryType.MoodBoost,
            _ => DeliveryType.SpeedBoost
        };

        decimal amount = type switch
        {
            // 1-3 Minuten Einkommen (Minimum 50)
            DeliveryType.Money => Math.Max(50m, Math.Round(
                state.NetIncomePerSecond * random.Next(60, 180), 0)),
            // 1-3 Goldschrauben
            DeliveryType.GoldenScrews => random.Next(1, 4),
            // 20-100 XP (skaliert mit Level)
            DeliveryType.Experience => 20 + state.PlayerLevel * 2 + random.Next(0, 40),
            // Mood +10 für alle Worker
            DeliveryType.MoodBoost => 10m,
            // 30min Speed-Boost
            DeliveryType.SpeedBoost => 30m,
            _ => 50m
        };

        return new SupplierDelivery
        {
            Type = type,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(2) // 2 Minuten zum Abholen
        };
    }
}
