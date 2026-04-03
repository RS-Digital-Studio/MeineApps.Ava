using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet Ausrüstungsgegenstände: Drops, Inventar, Zuweisung an Arbeiter, Shop.
/// </summary>
public interface IEquipmentService
{
    /// <summary>
    /// Wird ausgelöst wenn ein neues Equipment gedroppt wurde.
    /// </summary>
    event Action? EquipmentDropped;

    /// <summary>
    /// Rüstet einen Arbeiter mit einem Equipment-Gegenstand aus.
    /// </summary>
    void EquipItem(string workerId, Equipment equipment);

    /// <summary>
    /// Entfernt die Ausrüstung eines Arbeiters und legt sie zurück ins Inventar.
    /// </summary>
    void UnequipItem(string workerId);

    /// <summary>
    /// Versucht basierend auf Schwierigkeit einen zufälligen Drop zu generieren.
    /// Drop-Chance skaliert: Easy=5%, Medium=10%, Hard=15%, Expert=20%. Perfect-Rating: +5%.
    /// </summary>
    Equipment? TryGenerateDrop(int difficulty, bool isPerfect = false);

    /// <summary>
    /// Gibt die aktuellen Shop-Angebote zurück (3-4 zufällige Ausrüstungsgegenstände).
    /// </summary>
    List<Equipment> GetShopItems();

    /// <summary>
    /// Kauft einen Ausrüstungsgegenstand für Goldschrauben.
    /// </summary>
    void BuyEquipment(Equipment equipment);
}
