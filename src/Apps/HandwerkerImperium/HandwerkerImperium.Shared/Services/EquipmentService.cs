using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet Ausrüstungsgegenstände: Drops nach MiniGames (10% Chance),
/// Inventar-Verwaltung, Zuweisung an Arbeiter, Shop-Rotation.
/// </summary>
public sealed class EquipmentService : IEquipmentService
{
    private readonly IGameStateService _gameStateService;

    /// <summary>
    /// Drop-Chance nach einem MiniGame (10%).
    /// </summary>
    private const double DropChance = 0.10;

    /// <summary>
    /// Shop-Rotation: 3-4 zufällige Gegenstände.
    /// </summary>
    private const int MinShopItems = 3;
    private const int MaxShopItems = 4;

    public event Action? EquipmentDropped;

    public EquipmentService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public void EquipItem(string workerId, Equipment equipment)
    {
        var state = _gameStateService.State;

        // Arbeiter in allen Workshops suchen
        Worker? worker = FindWorker(workerId);
        if (worker == null) return;

        // Equipment aus Inventar entfernen
        var inventoryItem = state.EquipmentInventory.FirstOrDefault(e => e.Id == equipment.Id);
        if (inventoryItem == null) return;

        // Wenn der Arbeiter bereits etwas trägt, zurück ins Inventar
        if (worker.EquippedItem != null)
        {
            state.EquipmentInventory.Add(worker.EquippedItem);
        }

        // Neues Equipment ausrüsten
        worker.EquippedItem = inventoryItem;
        state.EquipmentInventory.Remove(inventoryItem);

        _gameStateService.MarkDirty();
    }

    public void UnequipItem(string workerId)
    {
        var state = _gameStateService.State;

        Worker? worker = FindWorker(workerId);
        if (worker?.EquippedItem == null) return;

        // Zurück ins Inventar
        state.EquipmentInventory.Add(worker.EquippedItem);
        worker.EquippedItem = null;

        _gameStateService.MarkDirty();
    }

    public Equipment? TryGenerateDrop(int difficulty)
    {
        // 10% Drop-Chance
        if (Random.Shared.NextDouble() >= DropChance)
            return null;

        var equipment = Equipment.GenerateRandom(difficulty);
        _gameStateService.State.EquipmentInventory.Add(equipment);
        _gameStateService.MarkDirty();

        EquipmentDropped?.Invoke();
        return equipment;
    }

    public List<Equipment> GetShopItems()
    {
        int count = Random.Shared.Next(MinShopItems, MaxShopItems + 1);
        var items = new List<Equipment>(count);

        for (int i = 0; i < count; i++)
        {
            // Shop-Items haben höhere Qualität (difficulty 1-3)
            int shopDifficulty = Random.Shared.Next(1, 4);
            items.Add(Equipment.GenerateRandom(shopDifficulty));
        }

        return items;
    }

    public void BuyEquipment(Equipment equipment)
    {
        int cost = equipment.ShopPrice;

        if (!_gameStateService.TrySpendGoldenScrews(cost))
            return;

        _gameStateService.State.EquipmentInventory.Add(equipment);
        _gameStateService.MarkDirty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht einen Arbeiter über alle Workshops hinweg.
    /// </summary>
    private Worker? FindWorker(string workerId)
    {
        foreach (var workshop in _gameStateService.State.Workshops)
        {
            var worker = workshop.Workers.FirstOrDefault(w => w.Id == workerId);
            if (worker != null)
                return worker;
        }
        return null;
    }
}
