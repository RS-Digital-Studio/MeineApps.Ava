using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet Shop-Upgrades und deren Auswirkungen
/// </summary>
public interface IShopService
{
    /// <summary>Aktueller Upgrade-Stand</summary>
    PlayerUpgrades Upgrades { get; }

    /// <summary>Alle Shop-Items mit aktuellem Stand generieren</summary>
    List<ShopDisplayItem> GetShopItems();

    /// <summary>Upgrade kaufen (prueft Coins, upgraded Level, speichert)</summary>
    bool TryPurchase(UpgradeType type);

    /// <summary>Upgrade kostenlos anwenden (ohne Coin-Abzug, z.B. per Rewarded Ad)</summary>
    bool TryPurchaseFree(UpgradeType type);

    /// <summary>Score-Multiplikator (1.0 / 1.25 / 1.5 / 2.0)</summary>
    float GetScoreMultiplier();

    /// <summary>Zeitbonus-Multiplikator (10 oder 20)</summary>
    int GetTimeBonusMultiplier();

    /// <summary>Start-Bomben (1 + Upgrade-Level)</summary>
    int GetStartBombs();

    /// <summary>Start-Feuerreichweite (1 + Upgrade-Level)</summary>
    int GetStartFire();

    /// <summary>Ob Speed von Anfang an aktiv ist</summary>
    bool HasStartSpeed();

    /// <summary>Start-Leben (3 + Upgrade-Level)</summary>
    int GetStartLives();

    /// <summary>Ob Eis-Bombe freigeschaltet ist</summary>
    bool HasIceBomb();

    /// <summary>Ob Feuer-Bombe freigeschaltet ist</summary>
    bool HasFireBomb();

    /// <summary>Ob Klebe-Bombe freigeschaltet ist</summary>
    bool HasStickyBomb();

    /// <summary>Gem-Preis für das nächste Upgrade-Level (0 = nicht mit Gems kaufbar)</summary>
    int GetGemPrice(UpgradeType type);

    /// <summary>Upgrade mit Gems kaufen (Alternative zu Coins, ab Level 3)</summary>
    bool TryPurchaseWithGems(UpgradeType type);

    /// <summary>Alle Upgrades zuruecksetzen</summary>
    void ResetUpgrades();
}
