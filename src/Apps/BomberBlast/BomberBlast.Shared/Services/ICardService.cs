using BomberBlast.Models;
using BomberBlast.Models.Cards;
using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet die Karten-Sammlung, Deck-Zusammenstellung und Karten-Drops.
/// </summary>
public interface ICardService
{
    /// <summary>Alle besessenen Karten</summary>
    IReadOnlyList<OwnedCard> OwnedCards { get; }

    /// <summary>Ausgerüstete Deck-Slots (max 5, kann leer sein)</summary>
    IReadOnlyList<BombType> EquippedSlots { get; }

    /// <summary>Prüft ob der Spieler eine bestimmte Karte besitzt</summary>
    bool HasCard(BombType type);

    /// <summary>Gibt die besessene Karte zurück (oder null)</summary>
    OwnedCard? GetOwnedCard(BombType type);

    /// <summary>Fügt eine Karte zur Sammlung hinzu (oder erhöht Duplikat-Zähler)</summary>
    void AddCard(BombType type);

    /// <summary>Karte upgraden (Bronze→Silber→Gold). Gibt true zurück bei Erfolg</summary>
    bool TryUpgradeCard(BombType type);

    /// <summary>Karte in einen Deck-Slot ausrüsten (0-4)</summary>
    void EquipCard(BombType type, int slotIndex);

    /// <summary>Karte aus einem Deck-Slot entfernen</summary>
    void UnequipSlot(int slotIndex);

    /// <summary>Generiert einen Karten-Drop basierend auf Level/Kontext</summary>
    BombType? GenerateDrop(int worldNumber, bool isBossDrop = false);

    /// <summary>Erstellt EquippedCard-Liste für Gameplay aus den Deck-Slots</summary>
    List<EquippedCard> GetEquippedCardsForGameplay();

    /// <summary>Migriert alte Shop-Bomben (Ice/Fire/Sticky) zu Karten</summary>
    void MigrateFromShop(bool hasIce, bool hasFire, bool hasSticky);

    /// <summary>Ob die Shop-Migration bereits durchgeführt wurde</summary>
    bool HasMigrated { get; }

    /// <summary>Wird ausgelöst wenn sich Sammlung oder Deck ändert</summary>
    event EventHandler? CollectionChanged;

    /// <summary>Ob der 5. Deck-Slot freigeschaltet ist</summary>
    bool IsSlot5Unlocked { get; }

    /// <summary>5. Deck-Slot für Gems freischalten. Gibt true zurück bei Erfolg</summary>
    bool TryUnlockSlot5(IGemService gemService);

    // ═══════════════════════════════════════════════════════════════════════
    // CRAFTING (v2.0.40, Plan Task 3.5)
    // ═══════════════════════════════════════════════════════════════════════
    // 5 Karten der gleichen Rarity + Coins → 1 Karte naechsthoeherer Rarity.
    // Reduziert Common-Stau (Spieler haben mit der Zeit zu viele Common-Duplikate).
    // Cost-Tabelle:
    //   5 Common + 2.000 Coins → 1 Rare      (Rare-Pool: 4 Karten Smoke/Lightning/Gravity/Poison)
    //   5 Rare + 8.000 Coins → 1 Epic      (Epic-Pool: TimeWarp/Mirror/Vortex/Phantom)
    //   5 Epic + 25.000 Coins → 1 Legendary (Legendary-Pool: Nova/BlackHole)

    /// <summary>Anzahl Quell-Karten die fuer ein Crafting benoetigt werden (5).</summary>
    int CraftCardCount { get; }

    /// <summary>Coin-Kosten fuer Crafting der angegebenen Ziel-Rarity. 0 = nicht craftbar.</summary>
    int GetCraftCoinCost(Rarity targetRarity);

    /// <summary>Wie viele Karten der angegebenen Rarity sind als Quelle verfuegbar (Stack-Summe).</summary>
    int GetCraftableCount(Rarity sourceRarity);

    /// <summary>
    /// Pruef-Funktion: Hat der Spieler genug Quell-Karten und Coins fuer das angegebene Ziel-Crafting?
    /// </summary>
    bool CanCraft(Rarity targetRarity, ICoinService coinService);

    /// <summary>
    /// Crafted eine Karte der Ziel-Rarity. Verbraucht 5 Karten der niedrigeren Rarity + Coins.
    /// </summary>
    /// <returns>BombType der gecrafteten Karte oder null bei Fehler (zu wenig Quell-Karten oder Coins).</returns>
    BombType? CraftCard(Rarity targetRarity, ICoinService coinService);
}
