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
public class CardService : ICardService
{
    private const string CARD_DATA_KEY = "CardCollection";
    private static readonly JsonSerializerOptions JsonOptions = new();
    private static readonly Random _random = new();

    private readonly IPreferencesService _preferences;
    private IAchievementService? _achievementService;
    private IWeeklyChallengeService? _weeklyService;
    private IDailyMissionService? _dailyMissionService;
    private CardCollectionData _data;

    public IReadOnlyList<OwnedCard> OwnedCards => _data.Cards;
    public IReadOnlyList<BombType> EquippedSlots => _data.DeckSlots;
    public bool HasMigrated => _data.ShopMigrated;

    public event EventHandler? CollectionChanged;

    /// <summary>Lazy-Injection um zirkuläre DI-Abhängigkeit zu vermeiden</summary>
    public void SetAchievementService(IAchievementService achievementService) => _achievementService = achievementService;

    /// <summary>Lazy-Injection für Mission-Tracking (Phase 9.4)</summary>
    public void SetMissionServices(IWeeklyChallengeService weeklyService, IDailyMissionService dailyMissionService)
    {
        _weeklyService = weeklyService;
        _dailyMissionService = dailyMissionService;
    }

    public CardService(IPreferencesService preferences)
    {
        _preferences = preferences;
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
        _achievementService?.OnCardCollected(uniqueCount, maxLevel);

        // Mission-Tracking: Karte gesammelt
        _weeklyService?.TrackProgress(WeeklyMissionType.CollectCards);
        _dailyMissionService?.TrackProgress(WeeklyMissionType.CollectCards);
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
        _achievementService?.OnCardCollected(_data.Cards.Count, maxLevel);

        // Mission-Tracking: Karte geupgraded
        _weeklyService?.TrackProgress(WeeklyMissionType.UpgradeCards);
        _dailyMissionService?.TrackProgress(WeeklyMissionType.UpgradeCards);

        return true;
    }

    public void EquipCard(BombType type, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= CardCatalog.MaxDeckSlots) return;
        if (!HasCard(type)) return;

        // Karte aus eventuell anderem Slot entfernen
        for (int i = 0; i < _data.DeckSlots.Count; i++)
        {
            if (_data.DeckSlots[i] == type)
            {
                _data.DeckSlots[i] = BombType.Normal; // Leerer Slot
            }
        }

        // Slots auf 4 auffüllen falls nötig
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

    /// <summary>
    /// Persistenz-Datenstruktur für die Karten-Sammlung
    /// </summary>
    private class CardCollectionData
    {
        public List<OwnedCard> Cards { get; set; } = new();
        public List<BombType> DeckSlots { get; set; } = new() { BombType.Normal, BombType.Normal, BombType.Normal, BombType.Normal };
        public bool ShopMigrated { get; set; }
    }
}
