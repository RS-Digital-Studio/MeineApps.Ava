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

    /// <summary>Ausgerüstete Deck-Slots (max 4, kann leer sein)</summary>
    IReadOnlyList<BombType> EquippedSlots { get; }

    /// <summary>Prüft ob der Spieler eine bestimmte Karte besitzt</summary>
    bool HasCard(BombType type);

    /// <summary>Gibt die besessene Karte zurück (oder null)</summary>
    OwnedCard? GetOwnedCard(BombType type);

    /// <summary>Fügt eine Karte zur Sammlung hinzu (oder erhöht Duplikat-Zähler)</summary>
    void AddCard(BombType type);

    /// <summary>Karte upgraden (Bronze→Silber→Gold). Gibt true zurück bei Erfolg</summary>
    bool TryUpgradeCard(BombType type);

    /// <summary>Karte in einen Deck-Slot ausrüsten (0-3)</summary>
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
}
