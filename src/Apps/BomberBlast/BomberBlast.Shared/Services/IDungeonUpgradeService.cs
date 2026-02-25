using BomberBlast.Models.Dungeon;

namespace BomberBlast.Services;

/// <summary>
/// Service für permanente Dungeon-Upgrades (Meta-Progression).
/// Upgrades werden mit DungeonCoins gekauft und bleiben über Runs hinweg bestehen.
/// </summary>
public interface IDungeonUpgradeService
{
    /// <summary>Aktuelles DungeonCoin-Guthaben</summary>
    int DungeonCoinBalance { get; }

    /// <summary>Event wenn sich DungeonCoin-Balance ändert</summary>
    event Action? BalanceChanged;

    /// <summary>Gibt das aktuelle Level eines Upgrades zurück (0 = nicht gekauft)</summary>
    int GetUpgradeLevel(string upgradeId);

    /// <summary>Prüft ob ein Upgrade gekauft werden kann (genug DC + nicht Max-Level)</summary>
    bool CanBuyUpgrade(string upgradeId);

    /// <summary>Kauft die nächste Stufe eines Upgrades. Gibt true zurück bei Erfolg.</summary>
    bool TryBuyUpgrade(string upgradeId);

    /// <summary>Fügt DungeonCoins hinzu (Floor-Belohnungen)</summary>
    void AddDungeonCoins(int amount);

    /// <summary>Gibt die Kosten für die nächste Stufe zurück (0 wenn Max-Level)</summary>
    int GetNextLevelCost(string upgradeId);

    /// <summary>Gibt alle Upgrade-Definitionen mit aktuellem Level zurück</summary>
    List<(DungeonUpgradeDefinition Definition, int CurrentLevel)> GetAllUpgrades();
}
