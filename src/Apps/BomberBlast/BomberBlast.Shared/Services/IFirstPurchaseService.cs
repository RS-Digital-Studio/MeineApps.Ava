namespace BomberBlast.Services;

/// <summary>
/// First-Time-Purchase-Bonus (Phase 23 — M5).
///
/// <para>Industry-Standard: Beim ersten echten Kauf bekommt der Spieler +100% auf den Inhalt.
/// v2.0.60 (B-B9): Bonus ist explizit auf das kleinste Gem-Paket (<c>gem_pack_small</c>, 100 Gems)
/// fokussiert — als Conversion-Hook („Erste 100 Gems = 200 Gems!"). Größere Pakete bekommen den
/// Bonus NICHT mehr (Whale-Anreiz war suboptimal — wer 14.99 € ausgibt, braucht keinen 100%-Bonus).</para>
///
/// <para>Persistenz: <c>FirstPurchaseClaimed</c>-Flag in Preferences. Sobald gesetzt, gilt der
/// Bonus nicht mehr. <c>HasClaimed</c> ist die Source-of-Truth — auch über App-Reinstall hinweg
/// (Cloud-Save sync).</para>
/// </summary>
public interface IFirstPurchaseService
{
    /// <summary>True wenn der Spieler den First-Purchase-Bonus bereits eingelöst hat.</summary>
    bool HasClaimed { get; }

    /// <summary>True wenn der Bonus aktuell verfügbar ist (nicht eingelöst).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// v2.0.60 (B-B9): Berechnet den Bonus-Multiplier für ein konkretes Produkt.
    /// Nur <c>gem_pack_small</c> bekommt den Bonus — andere Produkte erhalten 1.0×.
    /// </summary>
    float GetBonusMultiplier(string productId);

    /// <summary>
    /// Backward-Compat: liefert globalen Multiplier (2.0 wenn nicht claimed, sonst 1.0).
    /// Neue Aufrufer sollen die productId-Variante verwenden.
    /// </summary>
    [System.Obsolete("Verwende GetBonusMultiplier(productId) für saubere Targeting.")]
    float GetBonusMultiplier();

    /// <summary>
    /// v2.0.60 (B-B9): True wenn dieses Produkt für den First-Purchase-Bonus qualifiziert ist
    /// und noch nicht eingelöst wurde. Wird vom GemShopView genutzt um eine „First 100 = 200"-Badge zu zeigen.
    /// </summary>
    bool IsProductEligibleForBonus(string productId);

    /// <summary>Markiert den Bonus als eingelöst (nach erfolgreichem Kauf).</summary>
    void MarkAsClaimed();
}

/// <summary>
/// Standard-Implementation persistiert via <see cref="MeineApps.Core.Ava.Services.IPreferencesService"/>.
/// Cloud-Save-fähig: Key "FirstPurchaseClaimed" wird in CloudSaveService.SyncKeys aufgenommen.
/// </summary>
public sealed class FirstPurchaseService : IFirstPurchaseService
{
    private const string Key = "FirstPurchaseClaimed";
    // v2.0.60 (B-B9): Target-Produkt für First-Purchase-Bonus. Klein-genug für Casual-Conversion.
    private const string TargetProductId = "gem_pack_small";

    private readonly MeineApps.Core.Ava.Services.IPreferencesService _prefs;

    public FirstPurchaseService(MeineApps.Core.Ava.Services.IPreferencesService prefs)
    {
        _prefs = prefs;
    }

    public bool HasClaimed => _prefs.Get(Key, false);
    public bool IsAvailable => !HasClaimed;

    public float GetBonusMultiplier(string productId)
    {
        if (HasClaimed) return 1.0f;
        return productId == TargetProductId ? 2.0f : 1.0f;
    }

    [System.Obsolete("Verwende GetBonusMultiplier(productId) für saubere Targeting.")]
    public float GetBonusMultiplier() => HasClaimed ? 1.0f : 2.0f;

    public bool IsProductEligibleForBonus(string productId)
    {
        return !HasClaimed && productId == TargetProductId;
    }

    public void MarkAsClaimed() => _prefs.Set(Key, true);
}
