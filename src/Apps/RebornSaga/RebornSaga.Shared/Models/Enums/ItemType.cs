namespace RebornSaga.Models.Enums;

/// <summary>
/// Kategorie eines Items. Bestimmt Verhalten und UI-Darstellung.
/// </summary>
public enum ItemType
{
    Weapon,      // Waffe (Schwert, Stab, Dolch)
    Armor,       // Rüstung/Robe
    Accessory,   // Ring, Amulett, Stiefel
    Consumable,  // Verbrauchsgegenstand (Trank, Bombe)
    KeyItem      // Story-Item (nicht verkäuflich)
}

/// <summary>
/// Equipment-Slots für Ausrüstung.
/// </summary>
public enum EquipSlot
{
    Weapon,
    Armor,
    Accessory
}
