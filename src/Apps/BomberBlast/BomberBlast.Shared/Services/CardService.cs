using System.Text.Json;
using BomberBlast.Models;
using BomberBlast.Models.Cards;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistente Karten-Sammlung und Deck-Verwaltung.
/// JSON-Persistenz via IPreferencesService, Key "CardCollection".
/// </summary>
public sealed class CardService : ICardService
{
    private const string CARD_DATA_KEY = "CardCollection";
    private static readonly JsonSerializerOptions JsonOptions = new();
    private static readonly Random _random = new();

    private readonly IPreferencesService _preferences;
    private readonly Lazy<IAchievementService> _achievementService;
    private readonly Lazy<IWeeklyChallengeService> _weeklyService;
    private readonly Lazy<IDailyMissionService> _dailyMissionService;
    private CardCollectionData _data;

    public IReadOnlyList<OwnedCard> OwnedCards => _data.Cards;
    public IReadOnlyList<BombType> EquippedSlots => _data.DeckSlots;
    public bool HasMigrated => _data.ShopMigrated;
    public bool IsSlot5Unlocked => _data.Slot5Unlocked;

    public event EventHandler? CollectionChanged;

    public CardService(
        IPreferencesService preferences,
        Lazy<IAchievementService> achievementService,
        Lazy<IWeeklyChallengeService> weeklyService,
        Lazy<IDailyMissionService> dailyMissionService)
    {
        _preferences = preferences;
        _achievementService = achievementService;
        _weeklyService = weeklyService;
        _dailyMissionService = dailyMissionService;
        _data = Load();
    }

    public bool HasCard(BombType type)
    {
        foreach (var card in _data.Cards)
        {
            if (card.BombType == type) return true;
        }
        return false;
    }

    public OwnedCard? GetOwnedCard(BombType type)
    {
        foreach (var card in _data.Cards)
        {
            if (card.BombType == type) return card;
        }
        return null;
    }

    public void AddCard(BombType type)
    {
        var existing = GetOwnedCard(type);
        if (existing != null)
        {
            existing.Count++;
        }
        else
        {
            _data.Cards.Add(new OwnedCard { BombType = type, Count = 1, Level = 1 });
        }

        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);

        // Achievement: Karten-Sammlung prüfen
        int uniqueCount = _data.Cards.Count;
        int maxLevel = 0;
        foreach (var c in _data.Cards)
            if (c.Level > maxLevel) maxLevel = c.Level;
        _achievementService.Value.OnCardCollected(uniqueCount, maxLevel);

        // Mission-Tracking: Karte gesammelt
        _weeklyService.Value.TrackProgress(WeeklyMissionType.CollectCards);
        _dailyMissionService.Value.TrackProgress(WeeklyMissionType.CollectCards);
    }

    public bool TryUpgradeCard(BombType type)
    {
        var owned = GetOwnedCard(type);
        if (owned == null || !owned.CanUpgrade) return false;

        var cardDef = CardCatalog.GetCard(type);
        if (cardDef == null) return false;

        int requiredDuplicates = cardDef.GetDuplicatesForUpgrade(owned.Level);
        if (owned.Count < requiredDuplicates) return false;

        // Duplikate verbrauchen und Level erhöhen
        owned.Count -= requiredDuplicates;
        owned.Level++;

        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);

        // Achievement: Karten-Level prüfen (Gold = Level 3)
        int maxLevel = 0;
        foreach (var c in _data.Cards)
            if (c.Level > maxLevel) maxLevel = c.Level;
        _achievementService.Value.OnCardCollected(_data.Cards.Count, maxLevel);

        // Mission-Tracking: Karte geupgraded
        _weeklyService.Value.TrackProgress(WeeklyMissionType.UpgradeCards);
        _dailyMissionService.Value.TrackProgress(WeeklyMissionType.UpgradeCards);

        return true;
    }

    public void EquipCard(BombType type, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= CardCatalog.MaxDeckSlots) return;
        // Slot 5 (Index 4) nur erlauben wenn freigeschaltet
        if (slotIndex >= CardCatalog.DefaultDeckSlots && !_data.Slot5Unlocked) return;
        if (!HasCard(type)) return;

        // Karte aus eventuell anderem Slot entfernen
        for (int i = 0; i < _data.DeckSlots.Count; i++)
        {
            if (_data.DeckSlots[i] == type)
            {
                _data.DeckSlots[i] = BombType.Normal; // Leerer Slot
            }
        }

        // Slots auffüllen falls nötig
        while (_data.DeckSlots.Count <= slotIndex)
        {
            _data.DeckSlots.Add(BombType.Normal);
        }

        _data.DeckSlots[slotIndex] = type;

        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UnequipSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _data.DeckSlots.Count) return;

        _data.DeckSlots[slotIndex] = BombType.Normal;

        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public BombType? GenerateDrop(int worldNumber, bool isBossDrop = false)
    {
        // Drop-Chancen: Welt beeinflusst Epic/Legendary-Chance leicht
        float roll = (float)_random.NextDouble();

        Rarity targetRarity;
        if (isBossDrop)
        {
            // Boss-Drop: Garantiert Rare+
            if (roll < 0.03f + worldNumber * 0.01f)
                targetRarity = Rarity.Legendary;
            else if (roll < 0.20f + worldNumber * 0.02f)
                targetRarity = Rarity.Epic;
            else
                targetRarity = Rarity.Rare;
        }
        else
        {
            // Normaler Drop: 60% Common, 25% Rare, 12% Epic, 3% Legendary
            float legendaryChance = 0.03f + worldNumber * 0.005f;
            float epicChance = legendaryChance + 0.12f + worldNumber * 0.01f;
            float rareChance = epicChance + 0.25f;

            if (roll < legendaryChance)
                targetRarity = Rarity.Legendary;
            else if (roll < epicChance)
                targetRarity = Rarity.Epic;
            else if (roll < rareChance)
                targetRarity = Rarity.Rare;
            else
                targetRarity = Rarity.Common;
        }

        // Zufällige Karte der gewählten Rarität
        var candidates = CardCatalog.GetByRarity(targetRarity);
        if (candidates.Length == 0) return null;

        return candidates[_random.Next(candidates.Length)].BombType;
    }

    public List<EquippedCard> GetEquippedCardsForGameplay()
    {
        var result = new List<EquippedCard>();

        foreach (var slotType in _data.DeckSlots)
        {
            if (slotType == BombType.Normal) continue;

            var owned = GetOwnedCard(slotType);
            var cardDef = CardCatalog.GetCard(slotType);
            if (owned == null || cardDef == null) continue;

            result.Add(new EquippedCard
            {
                BombType = slotType,
                RemainingUses = cardDef.GetUsesForLevel(owned.Level),
                CardLevel = owned.Level,
                Rarity = cardDef.Rarity
            });
        }

        return result;
    }

    public bool TryUnlockSlot5(IGemService gemService)
    {
        if (_data.Slot5Unlocked) return false;
        if (!gemService.TrySpendGems(CardCatalog.Slot5UnlockCost)) return false;

        _data.Slot5Unlocked = true;

        // 5. Slot zur DeckSlots-Liste hinzufügen
        while (_data.DeckSlots.Count < CardCatalog.MaxDeckSlots)
            _data.DeckSlots.Add(BombType.Normal);

        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void MigrateFromShop(bool hasIce, bool hasFire, bool hasSticky)
    {
        if (_data.ShopMigrated) return;

        if (hasIce && !HasCard(BombType.Ice))
            _data.Cards.Add(new OwnedCard { BombType = BombType.Ice, Count = 1, Level = 1 });

        if (hasFire && !HasCard(BombType.Fire))
            _data.Cards.Add(new OwnedCard { BombType = BombType.Fire, Count = 1, Level = 1 });

        if (hasSticky && !HasCard(BombType.Sticky))
            _data.Cards.Add(new OwnedCard { BombType = BombType.Sticky, Count = 1, Level = 1 });

        _data.ShopMigrated = true;
        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private CardCollectionData Load()
    {
        try
        {
            string json = _preferences.Get<string>(CARD_DATA_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<CardCollectionData>(json, JsonOptions)
                       ?? new CardCollectionData();
            }
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new CardCollectionData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(CARD_DATA_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CRAFTING (v2.0.40, Plan Task 3.5)
    // ═══════════════════════════════════════════════════════════════════════

    public int CraftCardCount => 5;

    public int GetCraftCoinCost(Rarity targetRarity) => targetRarity switch
    {
        Rarity.Rare => 2_000,
        Rarity.Epic => 8_000,
        Rarity.Legendary => 25_000,
        _ => 0  // Common ist nicht craftbar (es gibt keine niedrigere Rarity)
    };

    public int GetCraftableCount(Rarity sourceRarity)
    {
        int total = 0;
        foreach (var owned in _data.Cards)
        {
            var def = CardCatalog.GetCard(owned.BombType);
            if (def == null) continue;
            if (def.Rarity != sourceRarity) continue;
            total += owned.Count;
        }
        return total;
    }

    public bool CanCraft(Rarity targetRarity, ICoinService coinService)
    {
        if (targetRarity == Rarity.Common) return false;
        var sourceRarity = (Rarity)((int)targetRarity - 1);
        int cost = GetCraftCoinCost(targetRarity);
        if (cost <= 0) return false;
        if (!coinService.CanAfford(cost)) return false;
        return GetCraftableCount(sourceRarity) >= CraftCardCount;
    }

    public BombType? CraftCard(Rarity targetRarity, ICoinService coinService)
    {
        if (!CanCraft(targetRarity, coinService)) return null;
        var sourceRarity = (Rarity)((int)targetRarity - 1);

        // Kosten zahlen — TrySpend gibt false bei Race; das wuerde nur bei paralleler Mutation passieren.
        int cost = GetCraftCoinCost(targetRarity);
        if (!coinService.TrySpendCoins(cost)) return null;

        // 5 Quell-Karten verbrauchen — bevorzugt von Karten mit hoechstem Count
        // (verhindert dass eine seltene Karte komplett aufgebraucht wird waehrend andere haengen).
        int remaining = CraftCardCount;
        var sortedSources = _data.Cards
            .Where(c => CardCatalog.GetCard(c.BombType)?.Rarity == sourceRarity)
            .OrderByDescending(c => c.Count)
            .ToList();
        foreach (var owned in sortedSources)
        {
            if (remaining <= 0) break;
            int take = Math.Min(remaining, owned.Count);
            owned.Count -= take;
            remaining -= take;
        }
        if (remaining > 0)
        {
            // Sollte durch CanCraft abgefangen sein — defensiv: Coins zurueckerstatten und abbrechen
            coinService.AddCoins(cost);
            return null;
        }

        // Ziel-Karte zufaellig aus dem Pool waehlen (deterministisch durch Time-Seed)
        var pool = CardCatalog.All.Where(c => c.Rarity == targetRarity).ToList();
        if (pool.Count == 0)
        {
            coinService.AddCoins(cost);
            return null;
        }
        var random = new Random();
        var picked = pool[random.Next(pool.Count)];
        AddCard(picked.BombType);  // erhoeht den Count und persistiert via Save()/CollectionChanged

        return picked.BombType;
    }

    /// <summary>
    /// Persistenz-Datenstruktur für die Karten-Sammlung
    /// </summary>
    private class CardCollectionData
    {
        public List<OwnedCard> Cards { get; set; } = new();
        public List<BombType> DeckSlots { get; set; } = new() { BombType.Normal, BombType.Normal, BombType.Normal, BombType.Normal };
        public bool ShopMigrated { get; set; }
        public bool Slot5Unlocked { get; set; }
    }
}
