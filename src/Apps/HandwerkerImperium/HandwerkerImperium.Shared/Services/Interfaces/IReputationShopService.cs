using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Reputation-Shop (v2.1.0): Items kaufen mit Reputation-Score.
/// Sichtbar ab Reputation &gt; 60 (siehe <see cref="MinReputationToUnlock"/>).
/// </summary>
public interface IReputationShopService
{
    /// <summary>Mindest-Reputation, ab der der Shop in der UI erscheint.</summary>
    int MinReputationToUnlock => 60;

    /// <summary>
    /// Liste aller verfuegbaren Items. Statisch (gleicher Inhalt fuer alle Spieler).
    /// </summary>
    IReadOnlyList<ReputationShopItem> AvailableItems { get; }

    /// <summary>True wenn der Shop fuer den aktuellen Spieler sichtbar sein soll.</summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Versucht, ein Item zu kaufen. Falls genuegend Reputation vorhanden, wird der Effekt
    /// angewandt und die Reputation reduziert. Gibt true bei Erfolg.
    /// </summary>
    bool TryBuy(string itemId);

    /// <summary>Feuert nach erfolgreichem Kauf — UI kann Banner/Toast zeigen.</summary>
    event Action<ReputationShopItem>? ItemPurchased;
}
