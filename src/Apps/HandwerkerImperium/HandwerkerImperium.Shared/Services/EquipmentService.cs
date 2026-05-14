using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet Ausrüstungsgegenstände: Drops nach MiniGames (10% Chance),
/// Inventar-Verwaltung, Zuweisung an Arbeiter, Shop-Rotation.
/// </summary>
public sealed class EquipmentService : IEquipmentService
{
    private readonly IGameStateService _gameStateService;
    private readonly IAnalyticsService? _analyticsService;
    // v2.1.1 (Audit C-C03): Eigener Lock entfernt — Mutationen via IGameStateService.ExecuteWithLock.

    /// <summary>
    /// Basis-Drop-Chance nach einem MiniGame (skaliert nach Schwierigkeit).
    /// Easy=5%, Medium=10%, Hard=15%, Expert=20%. Perfect-Rating: +5%.
    /// </summary>
    private const double BaseDropChance = 0.05;

    /// <summary>
    /// Shop-Rotation: 3-4 zufällige Gegenstände.
    /// </summary>
    private const int MinShopItems = 3;
    private const int MaxShopItems = 4;

    public event Action? EquipmentDropped;

    public EquipmentService(IGameStateService gameStateService, IAnalyticsService? analyticsService = null)
    {
        _gameStateService = gameStateService;
        _analyticsService = analyticsService;
    }

    public void EquipItem(string workerId, Equipment equipment)
    {
        Equipment? equippedItem = null;
        WorkerTier? workerTier = null;

        _gameStateService.ExecuteWithLock(() =>
        {
            var state = _gameStateService.State;

            // Arbeiter in allen Workshops suchen
            Worker? worker = FindWorker(workerId);
            if (worker == null) return;

            // Equipment aus Inventar entfernen (For-Schleife statt LINQ)
            Equipment? inventoryItem = null;
            var inventory = state.EquipmentInventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].Id == equipment.Id)
                {
                    inventoryItem = inventory[i];
                    break;
                }
            }
            if (inventoryItem == null) return;

            // Wenn der Arbeiter bereits etwas trägt, zurück ins Inventar
            if (worker.EquippedItem != null)
            {
                inventory.Add(worker.EquippedItem);
            }

            // Neues Equipment ausrüsten
            worker.EquippedItem = inventoryItem;
            inventory.Remove(inventoryItem);

            state.InvalidateIncomeCache();
            equippedItem = inventoryItem;
            workerTier = worker.Tier;
        });

        // Telemetrie ausserhalb des Locks
        if (equippedItem != null && workerTier != null)
        {
            _analyticsService?.TrackEvent(AnalyticsEvents.EquipmentEquipped, new System.Collections.Generic.Dictionary<string, object?>
            {
                ["equipment_id"] = equippedItem.Id,
                ["rarity"] = equippedItem.Rarity.ToString(),
                ["worker_tier"] = workerTier.ToString()
            });
        }
    }

    public void UnequipItem(string workerId)
    {
        _gameStateService.ExecuteWithLock(() =>
        {
            var state = _gameStateService.State;

            Worker? worker = FindWorker(workerId);
            if (worker?.EquippedItem == null) return;

            // Zurück ins Inventar
            state.EquipmentInventory.Add(worker.EquippedItem);
            worker.EquippedItem = null;

            state.InvalidateIncomeCache();
        });
    }

    public Equipment? TryGenerateDrop(int difficulty, bool isPerfect = false)
    {
        // Drop-Chance skaliert nach Schwierigkeit: +5% pro Stufe, Perfect +5%
        double dropChance = BaseDropChance + difficulty * 0.05 + (isPerfect ? 0.05 : 0.0);
        if (Random.Shared.NextDouble() >= dropChance)
            return null;

        var result = Equipment.GenerateRandom(difficulty);

        _gameStateService.ExecuteWithLock(() =>
        {
            _gameStateService.State.EquipmentInventory.Add(result);
        });

        // Event AUSSERHALB des Locks aufrufen (Deadlock-Prävention)
        EquipmentDropped?.Invoke();

        // P1.1 AAA-Audit: Equipment-Drop-Rate-Tracking fuer Difficulty-Tuning.
        _analyticsService?.TrackEvent(AnalyticsEvents.EquipmentDropped, new System.Collections.Generic.Dictionary<string, object?>
        {
            ["equipment_id"] = result.Id,
            ["rarity"] = result.Rarity.ToString(),
            ["difficulty"] = difficulty,
            ["is_perfect"] = isPerfect
        });
        return result;
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
        _gameStateService.ExecuteWithLock(() =>
        {
            int cost = equipment.ShopPrice;

            if (!_gameStateService.TrySpendGoldenScrews(cost))
                return;

            _gameStateService.State.EquipmentInventory.Add(equipment);
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht einen Arbeiter über alle Workshops hinweg (For-Schleife, kein LINQ).
    /// </summary>
    private Worker? FindWorker(string workerId)
    {
        var workshops = _gameStateService.State.Workshops;
        for (int i = 0; i < workshops.Count; i++)
        {
            var workers = workshops[i].Workers;
            for (int j = 0; j < workers.Count; j++)
            {
                if (workers[j].Id == workerId)
                    return workers[j];
            }
        }
        return null;
    }
}
