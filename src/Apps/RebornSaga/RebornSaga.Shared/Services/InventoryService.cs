namespace RebornSaga.Services;

using RebornSaga.Models;
using RebornSaga.Models.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Verwaltet Item-Definitionen, Spieler-Inventar und Equipment.
/// </summary>
public class InventoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    // Alle Item-Definitionen (ID → Item)
    private readonly Dictionary<string, Item> _itemDefinitions = new();

    // Spieler-Inventar (ID → Anzahl)
    private readonly Dictionary<string, int> _inventory = new();

    // Ausgerüstete Items (Slot → Item-ID)
    private readonly Dictionary<EquipSlot, string> _equipment = new();

    /// <summary>
    /// Lädt alle Item-Definitionen aus Embedded JSON.
    /// </summary>
    public void LoadItems()
    {
        _itemDefinitions.Clear();

        try
        {
            var resourceName = "RebornSaga.Data.Items.items.json";
            using var stream = typeof(InventoryService).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var items = JsonSerializer.Deserialize<List<Item>>(json, JsonOptions);
            if (items == null) return;

            foreach (var item in items)
                _itemDefinitions[item.Id] = item;
        }
        catch (Exception)
        {
            // Items konnten nicht geladen werden - leeres Inventar
        }
    }

    /// <summary>
    /// Gibt ein Item dem Inventar hinzu.
    /// </summary>
    public void AddItem(string itemId, int count = 1)
    {
        if (!_itemDefinitions.ContainsKey(itemId)) return;
        _inventory.TryGetValue(itemId, out var current);
        _inventory[itemId] = current + count;
    }

    /// <summary>
    /// Entfernt ein Item aus dem Inventar. Gibt false zurück wenn nicht genug vorhanden.
    /// </summary>
    public bool RemoveItem(string itemId, int count = 1)
    {
        if (!_inventory.TryGetValue(itemId, out var current) || current < count)
            return false;

        current -= count;
        if (current <= 0)
            _inventory.Remove(itemId);
        else
            _inventory[itemId] = current;

        return true;
    }

    /// <summary>
    /// Prüft ob ein Item im Inventar vorhanden ist.
    /// </summary>
    public bool HasItem(string itemId, int count = 1)
    {
        return _inventory.TryGetValue(itemId, out var current) && current >= count;
    }

    /// <summary>
    /// Gibt die Anzahl eines Items im Inventar zurück.
    /// </summary>
    public int GetItemCount(string itemId)
    {
        _inventory.TryGetValue(itemId, out var count);
        return count;
    }

    /// <summary>
    /// Rüstet ein Item aus. Gibt das vorher ausgerüstete Item zurück (null wenn Slot leer war).
    /// </summary>
    public string? EquipItem(string itemId, Player player)
    {
        if (!_itemDefinitions.TryGetValue(itemId, out var item) || !item.IsEquippable)
            return null;

        // Bereits ausgerüstet? Nichts tun
        if (IsEquipped(itemId)) return null;

        if (!HasItem(itemId)) return null;

        // Prüfe Klassen-Einschränkung
        if (!string.IsNullOrEmpty(item.ClassRestriction))
        {
            var classPrefix = player.Class switch
            {
                ClassName.Swordmaster => "Swordmaster",
                ClassName.Arcanist => "Arcanist",
                ClassName.Shadowblade => "Shadowblade",
                _ => ""
            };
            if (item.ClassRestriction != classPrefix) return null;
        }

        var slot = item.Slot;
        string? previousItemId = null;

        // Altes Equipment entfernen
        if (_equipment.TryGetValue(slot, out var oldItemId))
        {
            previousItemId = oldItemId;
            UnequipSlot(slot, player);
            AddItem(oldItemId); // Zurück ins Inventar
        }

        // Neues Equipment anlegen
        RemoveItem(itemId);
        _equipment[slot] = itemId;
        ApplyItemStats(item, player, true);

        return previousItemId;
    }

    /// <summary>
    /// Entfernt Equipment aus einem Slot.
    /// </summary>
    public void UnequipSlot(EquipSlot slot, Player player)
    {
        if (!_equipment.TryGetValue(slot, out var itemId)) return;
        if (!_itemDefinitions.TryGetValue(itemId, out var item)) return;

        ApplyItemStats(item, player, false);
        _equipment.Remove(slot);
        AddItem(itemId);
    }

    /// <summary>
    /// Benutzt einen Verbrauchsgegenstand auf einen Spieler.
    /// </summary>
    public bool UseItem(string itemId, Player player)
    {
        if (!_itemDefinitions.TryGetValue(itemId, out var item)) return false;
        if (!item.IsUsable) return false;
        if (!RemoveItem(itemId)) return false;

        // Heilung anwenden
        if (item.HealPercent > 0)
        {
            player.Hp = Math.Min(player.MaxHp, player.Hp + player.MaxHp * item.HealPercent / 100);
            player.Mp = Math.Min(player.MaxMp, player.Mp + player.MaxMp * item.HealPercent / 100);
        }
        else
        {
            if (item.HealHp > 0)
                player.Hp = Math.Min(player.MaxHp, player.Hp + item.HealHp);
            if (item.HealMp > 0)
                player.Mp = Math.Min(player.MaxMp, player.Mp + item.HealMp);
        }

        return true;
    }

    /// <summary>
    /// Fügt ein Item dem Inventar hinzu (Überladung für Item-Objekt).
    /// </summary>
    public void AddItem(Item item, int count = 1)
    {
        // Definition registrieren falls noch nicht vorhanden
        _itemDefinitions.TryAdd(item.Id, item);
        AddItem(item.Id, count);
    }

    /// <summary>
    /// Prüft ob ein Item aktuell ausgerüstet ist.
    /// </summary>
    public bool IsEquipped(string itemId)
    {
        return _equipment.ContainsValue(itemId);
    }

    /// <summary>
    /// Gibt das ausgerüstete Item in einem Slot zurück.
    /// </summary>
    public Item? GetEquipped(EquipSlot slot)
    {
        if (!_equipment.TryGetValue(slot, out var itemId)) return null;
        _itemDefinitions.TryGetValue(itemId, out var item);
        return item;
    }

    /// <summary>
    /// Gibt die ID des ausgerüsteten Items zurück.
    /// </summary>
    public string? GetEquippedId(EquipSlot slot)
    {
        _equipment.TryGetValue(slot, out var id);
        return id;
    }

    /// <summary>
    /// Gibt alle Items im Inventar zurück (Definition + Anzahl).
    /// </summary>
    public List<(Item item, int count)> GetInventoryItems()
    {
        var result = new List<(Item, int)>();
        foreach (var (id, count) in _inventory)
        {
            if (_itemDefinitions.TryGetValue(id, out var item))
                result.Add((item, count));
        }
        return result;
    }

    /// <summary>
    /// Gibt alle Items eines bestimmten Typs zurück.
    /// </summary>
    public List<(Item item, int count)> GetItemsByType(ItemType type)
    {
        return GetInventoryItems().Where(x => x.item.Type == type).ToList();
    }

    /// <summary>
    /// Gibt eine Item-Definition anhand der ID zurück.
    /// </summary>
    public Item? GetItemDefinition(string itemId)
    {
        _itemDefinitions.TryGetValue(itemId, out var item);
        return item;
    }

    /// <summary>
    /// Gibt den internen Inventar-Zustand zurück (für Save).
    /// </summary>
    public Dictionary<string, int> GetRawInventory() => new(_inventory);

    /// <summary>
    /// Gibt den internen Equipment-Zustand zurück (für Save).
    /// </summary>
    public Dictionary<EquipSlot, string> GetRawEquipment() => new(_equipment);

    /// <summary>
    /// Stellt den Inventar-Zustand aus einem Save wieder her.
    /// </summary>
    public void RestoreState(Dictionary<string, int> inventory, Dictionary<EquipSlot, string> equipment)
    {
        _inventory.Clear();
        _equipment.Clear();

        foreach (var (id, count) in inventory)
            _inventory[id] = count;

        foreach (var (slot, itemId) in equipment)
            _equipment[slot] = itemId;
    }

    /// <summary>
    /// Berechnet den Gesamtverkaufswert aller Items im Inventar.
    /// </summary>
    public int GetTotalSellValue()
    {
        var total = 0;
        foreach (var (id, count) in _inventory)
        {
            if (_itemDefinitions.TryGetValue(id, out var item) && item.Type != ItemType.KeyItem)
                total += item.SellPrice * count;
        }
        return total;
    }

    /// <summary>
    /// Wendet Item-Stat-Boni auf den Spieler an (oder entfernt sie).
    /// </summary>
    private static void ApplyItemStats(Item item, Player player, bool apply)
    {
        var sign = apply ? 1 : -1;
        player.Atk = Math.Max(1, player.Atk + item.AtkBonus * sign);
        player.Def = Math.Max(1, player.Def + item.DefBonus * sign);
        player.Int = Math.Max(1, player.Int + item.IntBonus * sign);
        player.Spd = Math.Max(1, player.Spd + item.SpdBonus * sign);
        player.Luk = Math.Max(0, player.Luk + item.LukBonus * sign);
        player.MaxHp = Math.Max(1, player.MaxHp + item.HpBonus * sign);
        player.MaxMp = Math.Max(0, player.MaxMp + item.MpBonus * sign);

        // HP/MP begrenzen (nicht über Max, nicht unter 1 für HP)
        player.Hp = Math.Clamp(player.Hp, 1, player.MaxHp);
        player.Mp = Math.Clamp(player.Mp, 0, player.MaxMp);
    }
}
