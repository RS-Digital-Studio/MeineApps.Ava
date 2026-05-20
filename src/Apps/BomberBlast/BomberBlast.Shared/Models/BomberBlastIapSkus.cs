namespace BomberBlast.Models;

/// <summary>
/// v2.0.60 (B-B2/B-B3): Zentrale Übersicht aller In-App-Purchase-SKUs für BomberBlast.
/// Single Source of Truth — alle Service-Aufrufer referenzieren diese Konstanten.
///
/// <para>Play Console muss diese SKUs in der Reihenfolge identisch konfiguriert haben:</para>
///
/// <list type="bullet">
///   <item><c>remove_ads</c> — Premium-IAP (non-consumable). Preis: 1,99 €.
///       Aktiviert PremiumService.IsPremium → Ad-Block + ×2 LevelComplete-Coins
///       + ×3 GameOver-Trostcoins + 5 spezielle Premium-Features.</item>
///   <item><c>gem_pack_small</c> — Consumable Gem-Paket. 100 Gems / 0,99 € = 101 G/€ (Basis).</item>
///   <item><c>gem_pack_medium</c> — Consumable. 600 Gems / 3,99 € = 150 G/€ (Popular).</item>
///   <item><c>gem_pack_large</c> — Consumable. 1500 Gems / 7,99 € = 188 G/€ (Best Value).</item>
///   <item><c>gem_pack_mega</c> — Consumable. 5000 Gems / 14,99 € = 334 G/€ (Whale-Tier).</item>
///   <item><c>battle_pass_plus_season</c> — Subscription (saisonal, 30 Tage). Preis: 4,99 €.
///       Aktiviert Premium-Track des aktuellen Battle-Pass.</item>
///   <item><c>vip_subscription_monthly</c> — Monatliche Subscription. Preis: 9,99 €/Monat.
///       Gewährt täglich 50 Gems + permanent ×1.5 XP + Premium-Battle-Pass inklusive.</item>
///   <item><c>starter_pack</c> — Non-consumable, einmaliges Limited-Time-Angebot.
///       Preis: 2,99 €. Aktiviert ab Level 20 (B-B16). Enthält 5000 C + 20 G + 3 Rare-Cards.</item>
/// </list>
///
/// <para>FirstPurchase-×2-Bonus (B-B9): Wird beim ersten Kauf eines Gem-Pakets gewährt.
/// Bevorzugt auf <c>gem_pack_small</c> als Conversion-Hook („Erste 100 Gems = 200 Gems!“).</para>
///
/// <para>Verifikation: Diese Liste muss bei jedem Release gegen den realen Play-Console-Stand
/// im IAP-Tab abgeglichen werden. Inkonsistenzen führen zu „Item nicht verfügbar“-Crashes.</para>
/// </summary>
public static class BomberBlastIapSkus
{
    // Premium (non-consumable, 1,99 €)
    public const string RemoveAds = "remove_ads";

    // Gem-Pakete (consumable)
    public const string GemPackSmall = "gem_pack_small";
    public const string GemPackMedium = "gem_pack_medium";
    public const string GemPackLarge = "gem_pack_large";
    public const string GemPackMega = "gem_pack_mega";

    // Subscriptions
    public const string BattlePassPlusSeason = "battle_pass_plus_season";
    public const string VipSubscriptionMonthly = "vip_subscription_monthly";

    // Limited-Time
    public const string StarterPack = "starter_pack";

    /// <summary>Alle SKUs als Array für Audit/Test-Zwecke.</summary>
    public static readonly string[] All =
    [
        RemoveAds,
        GemPackSmall, GemPackMedium, GemPackLarge, GemPackMega,
        BattlePassPlusSeason, VipSubscriptionMonthly,
        StarterPack,
    ];
}
