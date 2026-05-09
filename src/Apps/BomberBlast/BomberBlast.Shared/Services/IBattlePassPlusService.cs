namespace BomberBlast.Services;

/// <summary>
/// Battle-Pass-Plus-Tier (Phase 23b — AAA-Audit M1).
///
/// <para>Premium-Premium-Stufe oberhalb des Standard-BattlePass. Brawl-Stars-Pattern:
/// Brawl Pass (9,99 €) und Brawl Pass Plus (19,99 €). Plus enthält:</para>
/// <list type="bullet">
///   <item>Alle Standard-Premium-Rewards.</item>
///   <item>+25 Tier-Skip beim Saison-Start (Catch-up-Boost).</item>
///   <item>Plus-exklusive Skin/Trail (höchste Rarität).</item>
///   <item>+50% XP-Multiplier dauerhaft.</item>
///   <item>10 Bonus-Gems pro Tier-Up.</item>
/// </list>
///
/// <para>Code-Foundation — Console-Setup (IAP-Product-ID <c>battle_pass_plus_season</c>) vom User
/// nachgereicht. Bis dahin ist <see cref="HasPlus"/> false und Default-BattlePass-Pfad wird genutzt.</para>
/// </summary>
public interface IBattlePassPlusService
{
    /// <summary>True wenn der Spieler den Plus-Tier in der aktuellen Saison gekauft hat.</summary>
    bool HasPlus { get; }

    /// <summary>Saison-Nummer in der Plus aktiviert wurde (für Saison-Reset-Check).</summary>
    int PlusSeasonNumber { get; }

    /// <summary>Tier-Skip-Bonus beim Plus-Kauf (Default 25).</summary>
    int TierSkipOnPurchase { get; }

    /// <summary>XP-Multiplier wenn Plus aktiv ist (1.5 = +50%).</summary>
    float XpMultiplier { get; }

    /// <summary>Bonus-Gems pro Tier-Up wenn Plus aktiv ist (Default 10).</summary>
    int BonusGemsPerTier { get; }

    /// <summary>
    /// Schaltet Plus für die aktuelle Saison frei (nach erfolgreichem IAP).
    /// Idempotent für die gleiche Saison.
    /// </summary>
    void ActivatePlus(int currentSeasonNumber);

    /// <summary>Setzt Plus zurück (Saison-Wechsel — wird beim BattlePass-Reset aufgerufen).</summary>
    void ResetForNewSeason(int newSeasonNumber);
}

/// <summary>
/// Default-Implementation persistiert via Preferences.
/// </summary>
public sealed class BattlePassPlusService : IBattlePassPlusService
{
    private const string KeyHasPlus = "BattlePassPlus_Active";
    private const string KeyPlusSeason = "BattlePassPlus_Season";

    private readonly MeineApps.Core.Ava.Services.IPreferencesService _prefs;

    public BattlePassPlusService(MeineApps.Core.Ava.Services.IPreferencesService prefs)
    {
        _prefs = prefs;
    }

    public bool HasPlus => _prefs.Get(KeyHasPlus, false);
    public int PlusSeasonNumber => _prefs.Get(KeyPlusSeason, 0);

    public int TierSkipOnPurchase => 25;
    public float XpMultiplier => HasPlus ? 1.5f : 1.0f;
    public int BonusGemsPerTier => HasPlus ? 10 : 0;

    public void ActivatePlus(int currentSeasonNumber)
    {
        _prefs.Set(KeyHasPlus, true);
        _prefs.Set(KeyPlusSeason, currentSeasonNumber);
    }

    public void ResetForNewSeason(int newSeasonNumber)
    {
        // Wenn die persistierte Saison < newSeasonNumber → Plus läuft ab
        if (PlusSeasonNumber < newSeasonNumber)
        {
            _prefs.Set(KeyHasPlus, false);
            _prefs.Set(KeyPlusSeason, 0);
        }
    }
}
