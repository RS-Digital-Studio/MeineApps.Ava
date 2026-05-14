namespace BomberBlast.Services;

/// <summary>
/// First-Time-Purchase-Bonus (Phase 23 — M5).
///
/// <para>Industry-Standard: Beim ersten echten Kauf bekommt der Spieler +100% auf den Inhalt.
/// Konkret: Wenn der Spieler ein Gem-Pack kauft (4,99 EUR / 600 Gems), bekommt er beim
/// ersten Kauf 1200 Gems statt 600. Soft-Currency wird verdoppelt — IAP-Preis bleibt gleich.</para>
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
    /// Berechnet den Bonus-Multiplier für einen IAP. Vor dem ersten Kauf gibt das 2.0 zurück
    /// (100% Bonus), danach 1.0. Sollte vom PurchaseService bei jeder Gem-Vergabe gelesen werden.
    /// </summary>
    float GetBonusMultiplier();

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
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _prefs;

    public FirstPurchaseService(MeineApps.Core.Ava.Services.IPreferencesService prefs)
    {
        _prefs = prefs;
    }

    public bool HasClaimed => _prefs.Get(Key, false);
    public bool IsAvailable => !HasClaimed;

    public float GetBonusMultiplier() => HasClaimed ? 1.0f : 2.0f;

    public void MarkAsClaimed() => _prefs.Set(Key, true);
}
