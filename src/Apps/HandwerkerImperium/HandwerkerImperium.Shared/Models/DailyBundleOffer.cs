namespace HandwerkerImperium.Models;

/// <summary>
/// P1.3 AAA-Audit (08.05.2026, Foundation): Tagesabhängiges IAP-Bundle.
///
/// Robert hat hier nur die Foundation. Volle Umsetzung:
/// - 7 Slots (Mo–So) mit jeweils SKU + Bonus-Items
/// - Auto-Rotation um 00:00 UTC
/// - Push-Notification 4h vor Ablauf
/// - Server-getrieben via <see cref="RemoteConfigKeys.DailyBundleSkus"/> (JSON-Array)
///
/// Aktiver Block: <see cref="IDailyBundleService"/> +
/// <c>ShopViewModel</c>-Integration als zukünftiger Sprint (~2 Wochen).
/// </summary>
public sealed class DailyBundleOffer
{
    /// <summary>Slot-Index 0-6 (Montag = 0).</summary>
    public int DayOfWeekIndex { get; init; }

    /// <summary>Google-Play-IAP-SKU (z.B. <c>bundle_monday_starter_299</c>).</summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>RESX-Key für Bundle-Titel.</summary>
    public string TitleKey { get; init; } = string.Empty;

    /// <summary>RESX-Key für Bundle-Beschreibung.</summary>
    public string DescriptionKey { get; init; } = string.Empty;

    /// <summary>Bonus-Goldschrauben zusätzlich zur Standard-IAP.</summary>
    public int BonusGoldenScrews { get; init; }

    /// <summary>Bonus-Geld für sofortigen Boost.</summary>
    public decimal BonusMoney { get; init; }

    /// <summary>Optionaler Speed-Boost-Token (Stunden).</summary>
    public int SpeedBoostHours { get; init; }

    /// <summary>Anzeige-Preis als String (kommt aus Store-Fetch zur Laufzeit).</summary>
    public string DisplayPrice { get; set; } = string.Empty;

    /// <summary>Wann läuft das aktuelle Bundle ab (UTC, nächstes 00:00).</summary>
    public System.DateTime ExpiresAtUtc { get; init; }

    /// <summary>Sekunden bis zum Ablauf — für Countdown-Anzeige.</summary>
    public long SecondsUntilExpiry => System.Math.Max(0, (long)(ExpiresAtUtc - System.DateTime.UtcNow).TotalSeconds);
}
