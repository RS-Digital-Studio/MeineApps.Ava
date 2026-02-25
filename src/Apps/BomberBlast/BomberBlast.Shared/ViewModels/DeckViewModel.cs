using System.Collections.ObjectModel;
using BomberBlast.Models;
using BomberBlast.Models.Cards;
using BomberBlast.Models.Entities;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für den Deck-Builder: Karten-Sammlung anzeigen, Deck-Slots belegen,
/// Karten upgraden. Landscape 2-Spalten-Layout (Sammlung links, Deck+Detail rechts).
/// Zeigt ALLE 13 Karten (besessene + nicht-besessene als gesperrt).
/// </summary>
public partial class DeckViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly ICardService _cardService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localization;
    private readonly IBattlePassService _battlePassService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _titleText = "";

    [ObservableProperty]
    private string _coinsText = "";

    [ObservableProperty]
    private string _collectionProgressText = "";

    [ObservableProperty]
    private int _collectionProgressPercent;

    [ObservableProperty]
    private ObservableCollection<CardDisplayItem> _collectionCards = [];

    [ObservableProperty]
    private ObservableCollection<DeckSlotItem> _deckSlots = [];

    [ObservableProperty]
    private CardDisplayItem? _selectedCard;

    [ObservableProperty]
    private bool _hasSelectedCard;

    // Detail-Panel
    [ObservableProperty]
    private string _selectedCardName = "";

    [ObservableProperty]
    private string _selectedCardDescription = "";

    [ObservableProperty]
    private string _selectedCardRarityText = "";

    [ObservableProperty]
    private string _selectedCardRarityColor = "#FFFFFF";

    [ObservableProperty]
    private string _selectedCardLevelText = "";

    [ObservableProperty]
    private string _selectedCardUsesText = "";

    [ObservableProperty]
    private string _selectedCardStrengthText = "";

    [ObservableProperty]
    private string _selectedCardUpgradeText = "";

    [ObservableProperty]
    private bool _canUpgradeSelected;

    [ObservableProperty]
    private string _selectedCardIconName = "";

    [ObservableProperty]
    private string _selectedCardDropSourceText = "";

    [ObservableProperty]
    private int _upgradeProgressPercent;

    [ObservableProperty]
    private bool _showUpgradeProgress;

    [ObservableProperty]
    private bool _isSelectedCardOwned;

    [ObservableProperty]
    private string _deckCountText = "";

    [ObservableProperty]
    private string _selectCardHintText = "";

    // Lokalisierte Labels
    [ObservableProperty]
    private string _deckLabel = "Deck";

    [ObservableProperty]
    private string _strengthLabel = "";

    [ObservableProperty]
    private string _dropSourceLabel = "";

    [ObservableProperty]
    private string _upgradeLabel = "";

    [ObservableProperty]
    private string _notOwnedText = "";

    [ObservableProperty]
    private string _buyCardGemsText = "";

    [ObservableProperty]
    private bool _canBuyCardForGems;

    [ObservableProperty]
    private int _selectedCardGemPrice;

    [ObservableProperty]
    private string _gemsText = "";

    /// <summary>Ob der 5. Deck-Slot freigeschaltet ist</summary>
    [ObservableProperty]
    private bool _isSlot5Unlocked;

    /// <summary>Ob der 5. Slot kaufbar ist (genug Gems + noch nicht freigeschaltet)</summary>
    [ObservableProperty]
    private bool _canUnlockSlot5;

    /// <summary>Text für den Unlock-Button</summary>
    [ObservableProperty]
    private string _unlockSlot5Text = "";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public DeckViewModel(ICardService cardService, ICoinService coinService, IGemService gemService,
        ILocalizationService localization, IBattlePassService battlePassService)
    {
        _cardService = cardService;
        _coinService = coinService;
        _gemService = gemService;
        _localization = localization;
        _battlePassService = battlePassService;

        _cardService.CollectionChanged += (_, _) => RefreshAll();
        _coinService.BalanceChanged += (_, _) => UpdateCoins();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke(new GoBack());

    [RelayCommand]
    private void SelectCard(CardDisplayItem? card)
    {
        if (card == null) return;

        SelectedCard = card;
        HasSelectedCard = true;
        UpdateSelectedCardDetails();
    }

    [RelayCommand]
    private void EquipToSlot(string slotStr)
    {
        // XAML CommandParameter="0" übergibt string, nicht int
        if (!int.TryParse(slotStr, out var slotIndex)) return;
        if (SelectedCard == null || !SelectedCard.IsOwned) return;
        if (slotIndex < 0 || slotIndex >= CardCatalog.MaxDeckSlots) return;
        // Slot 5 (Index 4) nur wenn freigeschaltet
        if (slotIndex >= CardCatalog.DefaultDeckSlots && !_cardService.IsSlot5Unlocked) return;

        _cardService.EquipCard(SelectedCard.BombType, slotIndex);
        RefreshAll();
    }

    [RelayCommand]
    private void UnequipSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= CardCatalog.MaxDeckSlots) return;
        // Slot 5 (Index 4) nur wenn freigeschaltet
        if (slotIndex >= CardCatalog.DefaultDeckSlots && !_cardService.IsSlot5Unlocked) return;

        _cardService.UnequipSlot(slotIndex);
        RefreshAll();
    }

    [RelayCommand]
    private void UpgradeCard()
    {
        if (SelectedCard == null || !SelectedCard.IsOwned) return;

        var cardDef = CardCatalog.GetCard(SelectedCard.BombType);
        var owned = _cardService.GetOwnedCard(SelectedCard.BombType);
        if (cardDef == null || owned == null) return;

        int coinCost = cardDef.GetCoinCostForUpgrade(owned.Level);
        if (!_coinService.TrySpendCoins(coinCost)) return;

        if (_cardService.TryUpgradeCard(SelectedCard.BombType))
        {
            // Battle Pass XP für Karten-Upgrade
            _battlePassService.AddXp(BattlePassXpSources.CardUpgrade, "card_upgrade");

            string levelName = owned.Level switch
            {
                2 => _localization.GetString("CardLevelSilver") ?? "Silver",
                3 => _localization.GetString("CardLevelGold") ?? "Gold",
                _ => ""
            };
            FloatingTextRequested?.Invoke(levelName + "!", "#FFD700");
            CelebrationRequested?.Invoke();
            RefreshAll();
            UpdateSelectedCardDetails();
        }
    }

    /// <summary>Karte für Gems kaufen (Rare 15, Epic 30, Legendary 75)</summary>
    [RelayCommand]
    private void BuyCardForGems()
    {
        if (SelectedCard == null || !SelectedCard.IsOwned) return;

        var cardDef = CardCatalog.GetCard(SelectedCard.BombType);
        var owned = _cardService.GetOwnedCard(SelectedCard.BombType);
        if (cardDef == null || owned == null) return;

        // Nicht kaufen wenn max Level (3)
        if (!owned.CanUpgrade) return;

        int gemPrice = GetGemPriceForRarity(cardDef.Rarity);
        if (gemPrice <= 0) return;

        if (!_gemService.TrySpendGems(gemPrice)) return;

        // Duplikat hinzufügen (für Upgrades)
        _cardService.AddCard(SelectedCard.BombType);

        FloatingTextRequested?.Invoke($"+1 {_localization.GetString(cardDef.NameKey) ?? cardDef.NameKey}", "#00BCD4");
        RefreshAll();
        UpdateSelectedCardDetails();
        UpdateGemDisplay();
    }

    /// <summary>5. Deck-Slot für 20 Gems freischalten</summary>
    [RelayCommand]
    private void UnlockSlot5()
    {
        if (IsSlot5Unlocked) return;

        if (!_gemService.CanAfford(CardCatalog.Slot5UnlockCost))
        {
            FloatingTextRequested?.Invoke(
                _localization.GetString("InsufficientGems") ?? "Not enough Gems",
                "warning");
            return;
        }

        if (_cardService.TryUnlockSlot5(_gemService))
        {
            FloatingTextRequested?.Invoke($"-{CardCatalog.Slot5UnlockCost} Gems", "#00BCD4");
            FloatingTextRequested?.Invoke(
                _localization.GetString("DeckSlot5Unlocked") ?? "Slot #5 unlocked!",
                "#FFD700");
            CelebrationRequested?.Invoke();
            IsSlot5Unlocked = true;
            CanUnlockSlot5 = false;
            RefreshAll();
            UpdateGemDisplay();
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        SelectedCard = null;
        HasSelectedCard = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        RefreshAll();
    }

    public void UpdateLocalizedTexts()
    {
        TitleText = _localization.GetString("DeckTitle") ?? "Deck-Builder";
        SelectCardHintText = _localization.GetString("DeckSelectCardHint") ?? "Select a card";
        DeckLabel = _localization.GetString("DeckLabel") ?? "Deck";
        StrengthLabel = _localization.GetString("CardStrength") ?? "Strength";
        DropSourceLabel = _localization.GetString("CardDropSource") ?? "Drop Sources";
        UpgradeLabel = _localization.GetString("CardUpgradeLabel") ?? "Upgrade";
        NotOwnedText = _localization.GetString("CardNotOwned") ?? "Not found yet";
        UpdateCoins();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshAll()
    {
        RefreshCollection();
        RefreshDeckSlots();
        UpdateCoins();
        UpdateCollectionProgress();

        // Aktualisiere Detail wenn Karte noch ausgewählt
        if (HasSelectedCard && SelectedCard != null)
            UpdateSelectedCardDetails();
    }

    private void RefreshCollection()
    {
        CollectionCards.Clear();

        // ALLE Karten aus dem Katalog anzeigen (besessene + nicht-besessene)
        // Sortiert: Legendary → Epic → Rare → Common
        var sortedCards = CardCatalog.All
            .OrderByDescending(c => c.Rarity)
            .ThenBy(c => c.BombType);

        foreach (var cardDef in sortedCards)
        {
            var owned = _cardService.GetOwnedCard(cardDef.BombType);
            bool isOwned = owned != null;

            CollectionCards.Add(new CardDisplayItem
            {
                BombType = cardDef.BombType,
                Name = isOwned
                    ? (_localization.GetString(cardDef.NameKey) ?? cardDef.NameKey)
                    : "???",
                Rarity = cardDef.Rarity,
                Level = owned?.Level ?? 0,
                Count = owned?.Count ?? 0,
                CanUpgrade = owned?.CanUpgrade ?? false,
                IsEquipped = isOwned && IsCardEquipped(cardDef.BombType),
                IsOwned = isOwned,
                IconName = GetBombIcon(cardDef.BombType),
                UsesPerLevel = isOwned ? cardDef.GetUsesForLevel(owned!.Level) : cardDef.BaseBronzeUses
            });
        }
    }

    private void UpdateCollectionProgress()
    {
        int owned = _cardService.OwnedCards.Count;
        int total = CardCatalog.All.Length;
        CollectionProgressText = $"{owned} / {total}";
        CollectionProgressPercent = total > 0 ? owned * 100 / total : 0;
    }

    private void RefreshDeckSlots()
    {
        DeckSlots.Clear();
        var slots = _cardService.EquippedSlots;
        int equippedCount = 0;
        bool slot5Unlocked = _cardService.IsSlot5Unlocked;
        IsSlot5Unlocked = slot5Unlocked;
        CanUnlockSlot5 = !slot5Unlocked && _gemService.CanAfford(CardCatalog.Slot5UnlockCost);
        UnlockSlot5Text = string.Format(
            _localization.GetString("DeckUnlockSlot5") ?? "Unlock Slot #5 ({0} Gems)",
            CardCatalog.Slot5UnlockCost);

        // Verfügbare Slots: 4 Basis + ggf. freigeschalteter 5. Slot
        int visibleSlots = slot5Unlocked ? CardCatalog.MaxDeckSlots : CardCatalog.DefaultDeckSlots;

        for (int i = 0; i < visibleSlots; i++)
        {
            var slotType = i < slots.Count ? slots[i] : BombType.Normal;
            bool isEmpty = slotType == BombType.Normal;
            var cardDef = isEmpty ? null : CardCatalog.GetCard(slotType);
            var owned = isEmpty ? null : _cardService.GetOwnedCard(slotType);

            if (!isEmpty) equippedCount++;

            int uses = cardDef?.GetUsesForLevel(owned?.Level ?? 1) ?? 0;
            string usesLabel = _localization.GetString("CardUses") ?? "Uses";

            DeckSlots.Add(new DeckSlotItem
            {
                SlotIndex = i,
                BombType = slotType,
                IsEmpty = isEmpty,
                IsLocked = false,
                Name = cardDef != null ? (_localization.GetString(cardDef.NameKey) ?? "") : "",
                Rarity = cardDef?.Rarity ?? Rarity.Common,
                Level = owned?.Level ?? 0,
                Uses = uses,
                UsesText = isEmpty ? "" : $"{uses} {usesLabel}",
                IconName = isEmpty ? "" : GetBombIcon(slotType)
            });
        }

        DeckCountText = $"{equippedCount}/{visibleSlots}";
    }

    private void UpdateCoins()
    {
        CoinsText = $"{_coinService.Balance:N0}";
    }

    private void UpdateSelectedCardDetails()
    {
        if (SelectedCard == null)
        {
            HasSelectedCard = false;
            return;
        }

        var cardDef = CardCatalog.GetCard(SelectedCard.BombType);
        if (cardDef == null) return;

        var owned = _cardService.GetOwnedCard(SelectedCard.BombType);
        IsSelectedCardOwned = owned != null;

        // Name + Beschreibung (auch für nicht-besessene Karten sichtbar)
        SelectedCardName = _localization.GetString(cardDef.NameKey) ?? cardDef.NameKey;
        SelectedCardDescription = _localization.GetString(cardDef.DescriptionKey) ?? "";
        SelectedCardIconName = GetBombIcon(SelectedCard.BombType);

        // Rarität
        string rarityName = _localization.GetString(cardDef.Rarity.GetNameKey()) ?? cardDef.Rarity.ToString();
        SelectedCardRarityText = rarityName;
        SelectedCardRarityColor = SelectedCard.RarityColorHex;

        // Drop-Quellen
        SelectedCardDropSourceText = GetDropSourceText(cardDef.Rarity);

        if (owned != null)
        {
            // Level + Stärke
            string levelStr = owned.Level switch
            {
                1 => _localization.GetString("CardLevelBronze") ?? "Bronze",
                2 => _localization.GetString("CardLevelSilver") ?? "Silver",
                3 => _localization.GetString("CardLevelGold") ?? "Gold",
                _ => ""
            };
            SelectedCardLevelText = $"{levelStr} (Lv.{owned.Level})";

            float multiplier = owned.Level switch { 2 => 1.2f, 3 => 1.4f, _ => 1.0f };
            SelectedCardStrengthText = $"{multiplier:F1}x";

            // Einsätze
            int uses = cardDef.GetUsesForLevel(owned.Level);
            SelectedCardUsesText = $"{uses} {_localization.GetString("CardUses") ?? "Uses"}";

            // Upgrade-Info
            if (owned.CanUpgrade)
            {
                int dupsNeeded = cardDef.GetDuplicatesForUpgrade(owned.Level);
                int coinCost = cardDef.GetCoinCostForUpgrade(owned.Level);
                SelectedCardUpgradeText = $"{owned.Count}/{dupsNeeded} + {coinCost:N0} Coins";
                CanUpgradeSelected = owned.Count >= dupsNeeded && _coinService.Balance >= coinCost;
                UpgradeProgressPercent = Math.Min(100, owned.Count * 100 / Math.Max(1, dupsNeeded));
                ShowUpgradeProgress = true;
            }
            else
            {
                SelectedCardUpgradeText = _localization.GetString("CardMaxLevel") ?? "Max";
                CanUpgradeSelected = false;
                UpgradeProgressPercent = 100;
                ShowUpgradeProgress = false;
            }
        }
        else
        {
            // Nicht besessene Karte
            SelectedCardLevelText = "";
            SelectedCardStrengthText = "";
            SelectedCardUsesText = $"{cardDef.BaseBronzeUses} {_localization.GetString("CardUses") ?? "Uses"} (Lv.1)";
            SelectedCardUpgradeText = "";
            CanBuyCardForGems = false;
            CanUpgradeSelected = false;
            UpgradeProgressPercent = 0;
            ShowUpgradeProgress = false;
        }

        // Gem-Kauf-Status aktualisieren
        UpdateGemBuyState();
        UpdateGemDisplay();
    }

    private string GetDropSourceText(Rarity rarity)
    {
        // Drop-Quellen je nach Rarität
        string levelDrop = _localization.GetString("DropSourceLevel") ?? "Level Complete";
        string bossDrop = _localization.GetString("DropSourceBoss") ?? "Boss Fight";
        string dungeonDrop = _localization.GetString("DropSourceDungeon") ?? "Dungeon";

        return rarity switch
        {
            Rarity.Common => $"{levelDrop} (60%)",
            Rarity.Rare => $"{levelDrop} (25%)\n{bossDrop}",
            Rarity.Epic => $"{levelDrop} (12%)\n{bossDrop}\n{dungeonDrop}",
            Rarity.Legendary => $"{bossDrop} (3%)\n{dungeonDrop}",
            _ => levelDrop
        };
    }

    /// <summary>Mappt BombType auf ein Material Icon</summary>
    private static string GetBombIcon(BombType type) => type switch
    {
        BombType.Ice => "Snowflake",
        BombType.Fire => "Fire",
        BombType.Sticky => "Water",
        BombType.Smoke => "WeatherFog",
        BombType.Lightning => "LightningBolt",
        BombType.Gravity => "Magnet",
        BombType.Poison => "Skull",
        BombType.TimeWarp => "ClockFast",
        BombType.Mirror => "FlipHorizontal",
        BombType.Vortex => "Tornado",
        BombType.Phantom => "Ghost",
        BombType.Nova => "Flare",
        BombType.BlackHole => "CircleSlice8",
        _ => "Bomb"
    };

    private bool IsCardEquipped(BombType type)
    {
        var slots = _cardService.EquippedSlots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == type) return true;
        }
        return false;
    }

    /// <summary>Gem-Preis je nach Rarität: Rare 15, Epic 30, Legendary 75</summary>
    private static int GetGemPriceForRarity(Rarity rarity) => rarity switch
    {
        Rarity.Rare => 15,
        Rarity.Epic => 30,
        Rarity.Legendary => 75,
        _ => 0 // Common kann nicht für Gems gekauft werden
    };

    private void UpdateGemDisplay()
    {
        GemsText = $"{_gemService.Balance:N0}";
    }

    /// <summary>Aktualisiert die Gem-Kauf-Anzeige für die ausgewählte Karte</summary>
    private void UpdateGemBuyState()
    {
        if (SelectedCard == null || !SelectedCard.IsOwned)
        {
            CanBuyCardForGems = false;
            BuyCardGemsText = "";
            SelectedCardGemPrice = 0;
            return;
        }

        var cardDef = CardCatalog.GetCard(SelectedCard.BombType);
        var owned = _cardService.GetOwnedCard(SelectedCard.BombType);
        if (cardDef == null || owned == null || !owned.CanUpgrade)
        {
            CanBuyCardForGems = false;
            BuyCardGemsText = "";
            SelectedCardGemPrice = 0;
            return;
        }

        int gemPrice = GetGemPriceForRarity(cardDef.Rarity);
        if (gemPrice <= 0)
        {
            CanBuyCardForGems = false;
            return;
        }

        SelectedCardGemPrice = gemPrice;
        CanBuyCardForGems = _gemService.CanAfford(gemPrice);
        BuyCardGemsText = string.Format(
            _localization.GetString("BuyCardGems") ?? "Buy for {0} Gems", gemPrice);
    }
}

/// <summary>
/// Anzeige-Item für eine Karte in der Sammlung
/// </summary>
public class CardDisplayItem
{
    public BombType BombType { get; set; }
    public string Name { get; set; } = "";
    public Rarity Rarity { get; set; }
    public int Level { get; set; }
    public int Count { get; set; }
    public bool CanUpgrade { get; set; }
    public bool IsEquipped { get; set; }
    public bool IsOwned { get; set; } = true;
    public string IconName { get; set; } = "";
    public int UsesPerLevel { get; set; }

    /// <summary>Raritäts-Farbe als Hex-String für XAML-Binding</summary>
    public string RarityColorHex => Rarity switch
    {
        Rarity.Common => "#FFFFFF",
        Rarity.Rare => "#2196F3",
        Rarity.Epic => "#9C27B0",
        Rarity.Legendary => "#FFD700",
        _ => "#FFFFFF"
    };

    /// <summary>Raritäts-Glow-Farbe (heller)</summary>
    public string RarityGlowHex => Rarity switch
    {
        Rarity.Common => "#B0B0B0",
        Rarity.Rare => "#64B5F6",
        Rarity.Epic => "#CE93D8",
        Rarity.Legendary => "#FFE082",
        _ => "#B0B0B0"
    };

    /// <summary>Level-Bezeichnung</summary>
    public string LevelLabel => Level switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        _ => ""
    };

    /// <summary>Level-Farbe (Bronze/Silber/Gold)</summary>
    public string LevelColorHex => Level switch
    {
        1 => "#CD7F32",   // Bronze
        2 => "#C0C0C0",   // Silber
        3 => "#FFD700",   // Gold
        _ => "#666666"
    };

    /// <summary>Opacity für nicht-besessene Karten</summary>
    public double CardOpacity => IsOwned ? 1.0 : 0.4;

    /// <summary>Ob ein Upgrade-Badge angezeigt werden soll</summary>
    public bool ShowUpgradeBadge => IsOwned && CanUpgrade;

    /// <summary>Ob ein Equipped-Badge angezeigt werden soll</summary>
    public bool ShowEquippedBadge => IsOwned && IsEquipped;
}

/// <summary>
/// Anzeige-Item für einen Deck-Slot
/// </summary>
public class DeckSlotItem
{
    public int SlotIndex { get; set; }
    public BombType BombType { get; set; }
    public bool IsEmpty { get; set; }
    public bool IsLocked { get; set; }
    public string Name { get; set; } = "";
    public Rarity Rarity { get; set; }
    public int Level { get; set; }
    public int Uses { get; set; }
    public string IconName { get; set; } = "";

    /// <summary>1-basierte Slot-Nummer für Anzeige</summary>
    public string SlotNumber => $"#{SlotIndex + 1}";

    /// <summary>Uses-Anzeige für XAML-Binding</summary>
    public string UsesText { get; set; } = "";

    /// <summary>Raritäts-Farbe als Hex-String für XAML-Binding</summary>
    public string RarityColorHex => Rarity switch
    {
        Rarity.Common => "#FFFFFF",
        Rarity.Rare => "#2196F3",
        Rarity.Epic => "#9C27B0",
        Rarity.Legendary => "#FFD700",
        _ => "#FFFFFF"
    };
}
