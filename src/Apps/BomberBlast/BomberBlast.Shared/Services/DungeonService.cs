using System.Globalization;
using System.Text.Json;
using BomberBlast.Models;
using BomberBlast.Models.Dungeon;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet Dungeon-Run Roguelike-Modus: Run-State, Floor-Belohnungen, Buff-Auswahl, Statistiken.
/// Persistenz via IPreferencesService (JSON).
/// </summary>
public class DungeonService : IDungeonService
{
    private const string RUN_STATE_KEY = "DungeonRunData";
    private const string STATS_KEY = "DungeonStatsData";
    private const int PAID_RUN_COIN_COST = 500;
    private const int PAID_RUN_GEM_COST = 10;

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;

    private DungeonRunState? _runState;
    private DungeonStats _stats;
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DungeonRunState? RunState => _runState;
    public DungeonStats Stats => _stats;
    public int PaidRunCoinCost => PAID_RUN_COIN_COST;
    public int PaidRunGemCost => PAID_RUN_GEM_COST;
    public bool IsRunActive => _runState is { IsActive: true };

    public bool IsBuffFloorNext =>
        _runState != null && DungeonBuffCatalog.IsBuffFloor(_runState.CurrentFloor);

    public bool IsCurrentFloorBoss =>
        _runState != null && DungeonBuffCatalog.IsBossFloor(_runState.CurrentFloor);

    public event Action? RunStateChanged;

    public DungeonService(
        IPreferencesService preferences,
        ICoinService coinService,
        IGemService gemService,
        ICardService cardService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _cardService = cardService;

        // Aktiven Run laden (falls App geschlossen wurde während Run)
        _runState = LoadRunState();
        _stats = LoadStats();
    }

    // === Eintritts-Prüfungen ===

    public bool CanStartFreeRun
    {
        get
        {
            if (_runState is { IsActive: true }) return false;
            if (string.IsNullOrEmpty(_stats.TotalRuns == 0 ? "" : _runState?.LastFreeRunDate ?? ""))
                return true;
            var lastFree = _runState?.LastFreeRunDate ?? "";
            if (string.IsNullOrEmpty(lastFree)) return true;
            var lastDate = DateTime.Parse(lastFree, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return lastDate.Date < DateTime.UtcNow.Date;
        }
    }

    public bool CanStartAdRun
    {
        get
        {
            if (_runState is { IsActive: true }) return false;
            var lastAd = _runState?.LastAdRunDate ?? "";
            if (string.IsNullOrEmpty(lastAd)) return true;
            var lastDate = DateTime.Parse(lastAd, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return lastDate.Date < DateTime.UtcNow.Date;
        }
    }

    // === Run-Management ===

    public bool StartRun(DungeonEntryType entryType)
    {
        if (_runState is { IsActive: true }) return false;

        switch (entryType)
        {
            case DungeonEntryType.Free:
                if (!CanStartFreeRun) return false;
                break;
            case DungeonEntryType.Coins:
                if (!_coinService.TrySpendCoins(PAID_RUN_COIN_COST)) return false;
                break;
            case DungeonEntryType.Gems:
                if (!_gemService.TrySpendGems(PAID_RUN_GEM_COST)) return false;
                break;
            case DungeonEntryType.Ad:
                if (!CanStartAdRun) return false;
                break;
        }

        _runState = new DungeonRunState
        {
            CurrentFloor = 1,
            Lives = 1,
            IsActive = true,
            RunSeed = Environment.TickCount,
            LastFreeRunDate = entryType == DungeonEntryType.Free
                ? DateTime.UtcNow.ToString("O")
                : _runState?.LastFreeRunDate ?? "",
            LastAdRunDate = entryType == DungeonEntryType.Ad
                ? DateTime.UtcNow.ToString("O")
                : _runState?.LastAdRunDate ?? ""
        };

        SaveRunState();
        RunStateChanged?.Invoke();
        return true;
    }

    public DungeonFloorReward CompleteFloor()
    {
        if (_runState == null || !_runState.IsActive)
            return new DungeonFloorReward();

        var floor = _runState.CurrentFloor;
        var reward = CalculateFloorReward(floor);

        // Buff-basierte Coin-Bonus-Berechnung
        if (_runState.ActiveBuffs.Contains(DungeonBuffType.CoinBonus))
        {
            reward.Coins = (int)(reward.Coins * 1.5);
            reward.ChestBonus = (int)(reward.ChestBonus * 1.5);
        }

        // Belohnungen zum Run-State hinzufügen
        _runState.CollectedCoins += reward.Coins + reward.ChestBonus;
        _runState.CollectedGems += reward.Gems;

        if (reward.CardDrop >= 0)
            _runState.CollectedCardDrops.Add(reward.CardDrop);

        // Nächster Floor
        _runState.CurrentFloor++;

        SaveRunState();
        RunStateChanged?.Invoke();
        return reward;
    }

    public DungeonRunSummary EndRun()
    {
        if (_runState == null)
            return new DungeonRunSummary();

        var floorsCompleted = _runState.CurrentFloor - 1;
        var isNewBest = floorsCompleted > _stats.BestFloor;

        // Belohnungen auszahlen
        if (_runState.CollectedCoins > 0)
            _coinService.AddCoins(_runState.CollectedCoins);
        if (_runState.CollectedGems > 0)
            _gemService.AddGems(_runState.CollectedGems);

        // Karten hinzufügen
        foreach (var cardType in _runState.CollectedCardDrops)
            _cardService.AddCard((BombType)cardType);

        var summary = new DungeonRunSummary
        {
            FloorsCompleted = floorsCompleted,
            TotalCoins = _runState.CollectedCoins,
            TotalGems = _runState.CollectedGems,
            TotalCards = _runState.CollectedCardDrops.Count,
            IsNewBestFloor = isNewBest,
            UsedBuffs = [.._runState.ActiveBuffs]
        };

        // Statistiken aktualisieren
        _stats.TotalRuns++;
        if (isNewBest) _stats.BestFloor = floorsCompleted;
        _stats.TotalCoinsEarned += _runState.CollectedCoins;
        _stats.TotalGemsEarned += _runState.CollectedGems;
        _stats.TotalCardsEarned += _runState.CollectedCardDrops.Count;

        // Run beenden
        _runState.IsActive = false;
        _runState.ActiveBuffs.Clear();
        _runState.CollectedCoins = 0;
        _runState.CollectedGems = 0;
        _runState.CollectedCardDrops.Clear();
        _runState.CurrentFloor = 0;

        SaveRunState();
        SaveStats();
        RunStateChanged?.Invoke();
        return summary;
    }

    // === Buff-System ===

    public List<DungeonBuffDefinition> GenerateBuffChoices()
    {
        if (_runState == null) return [];

        // Seed basierend auf Run-Seed + Floor für Determinismus
        var buffRandom = new Random(_runState.RunSeed + _runState.CurrentFloor * 100);
        var available = DungeonBuffCatalog.All
            .Where(b => !_runState.ActiveBuffs.Contains(b.Type) ||
                        b.Type == DungeonBuffType.ExtraBomb ||
                        b.Type == DungeonBuffType.ExtraFire ||
                        b.Type == DungeonBuffType.ExtraLife)
            .ToList();

        // Gewichtete Auswahl von 3 verschiedenen Buffs
        var choices = new List<DungeonBuffDefinition>(3);
        var totalWeight = available.Sum(b => b.Weight);

        for (int i = 0; i < 3 && available.Count > 0; i++)
        {
            int roll = buffRandom.Next(totalWeight);
            int cumulative = 0;

            for (int j = 0; j < available.Count; j++)
            {
                cumulative += available[j].Weight;
                if (roll < cumulative)
                {
                    choices.Add(available[j]);
                    totalWeight -= available[j].Weight;
                    available.RemoveAt(j);
                    break;
                }
            }
        }

        return choices;
    }

    public void ApplyBuff(DungeonBuffType buffType)
    {
        if (_runState == null || !_runState.IsActive) return;

        _runState.ActiveBuffs.Add(buffType);

        // Sofort-Effekte
        switch (buffType)
        {
            case DungeonBuffType.ExtraLife:
                _runState.Lives++;
                break;
            case DungeonBuffType.Shield:
                // Wird in GameEngine.ApplyUpgrades() angewendet
                break;
        }

        SaveRunState();
        RunStateChanged?.Invoke();
    }

    // === Belohnungs-Berechnung ===

    private DungeonFloorReward CalculateFloorReward(int floor)
    {
        var reward = new DungeonFloorReward();
        var isBoss = DungeonBuffCatalog.IsBossFloor(floor);
        reward.WasBossFloor = isBoss;

        // Skalierungsfaktor für Floors > 10 (+50% pro 10er-Zyklus)
        float scaleFactor = floor > 10 ? 1f + (floor - 10) * 0.05f : 1f;

        if (isBoss)
        {
            if (floor % 10 == 5) // Mini-Boss (Floor 5, 15, 25, ...)
            {
                reward.Coins = (int)(800 * scaleFactor);
                reward.Gems = 5;
                // 100% Rare-Karten-Drop
                reward.CardDrop = (int)GenerateCardDrop(minRarity: 1);
            }
            else // End-Boss (Floor 10, 20, 30, ...)
            {
                reward.Coins = (int)(2000 * scaleFactor);
                reward.Gems = 15;
                reward.ChestBonus = (int)(3000 * scaleFactor);
                // 100% Epic-Karten-Drop + 50% Bonus-Drop
                reward.CardDrop = (int)GenerateCardDrop(minRarity: 2);
            }
        }
        else if (floor <= 4 || (floor > 10 && floor % 10 <= 4))
        {
            // Frühe Floors: 200-500 Coins, 30-45% Karten-Drop
            reward.Coins = (int)((200 + (floor % 5) * 75) * scaleFactor);
            if (_random.NextDouble() < 0.3 + (floor % 5) * 0.0375)
                reward.CardDrop = (int)GenerateCardDrop(minRarity: 0);
        }
        else
        {
            // Späte Floors (6-9): 600-1000 Coins, 50-70% Karten-Drop
            reward.Coins = (int)((600 + (floor % 5) * 100) * scaleFactor);
            if (_random.NextDouble() < 0.5 + (floor % 5) * 0.05)
                reward.CardDrop = (int)GenerateCardDrop(minRarity: 1);
        }

        return reward;
    }

    /// <summary>
    /// Generiert einen Karten-Drop mit Mindest-Rarität.
    /// 0=Common, 1=Rare, 2=Epic, 3=Legendary
    /// </summary>
    private BombType GenerateCardDrop(int minRarity)
    {
        // Gewichtete Auswahl basierend auf Rarität
        var cards = Models.Cards.CardCatalog.All;
        var eligible = new List<Models.Cards.BombCard>();

        foreach (var card in cards)
        {
            int rarityValue = card.Rarity switch
            {
                Rarity.Common => 0,
                Rarity.Rare => 1,
                Rarity.Epic => 2,
                Rarity.Legendary => 3,
                _ => 0
            };

            if (rarityValue >= minRarity)
                eligible.Add(card);
        }

        if (eligible.Count == 0)
            return BombType.Normal;

        // Höhere Rarität = niedrigeres Gewicht
        int totalWeight = 0;
        var weights = new int[eligible.Count];
        for (int i = 0; i < eligible.Count; i++)
        {
            weights[i] = eligible[i].Rarity switch
            {
                Rarity.Common => 60,
                Rarity.Rare => 25,
                Rarity.Epic => 12,
                Rarity.Legendary => 3,
                _ => 60
            };
            totalWeight += weights[i];
        }

        int roll = _random.Next(totalWeight);
        int cumulative = 0;
        for (int i = 0; i < eligible.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return eligible[i].BombType;
        }

        return eligible[^1].BombType;
    }

    // === Persistenz ===

    private DungeonRunState? LoadRunState()
    {
        var json = _preferences.Get(RUN_STATE_KEY, "");
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var state = JsonSerializer.Deserialize<DungeonRunState>(json, JsonOptions);
            return state is { IsActive: true } ? state : null;
        }
        catch
        {
            return null;
        }
    }

    private DungeonStats LoadStats()
    {
        var json = _preferences.Get(STATS_KEY, "");
        if (string.IsNullOrEmpty(json)) return new DungeonStats();

        try
        {
            return JsonSerializer.Deserialize<DungeonStats>(json, JsonOptions) ?? new DungeonStats();
        }
        catch
        {
            return new DungeonStats();
        }
    }

    private void SaveRunState()
    {
        var json = JsonSerializer.Serialize(_runState ?? new DungeonRunState(), JsonOptions);
        _preferences.Set(RUN_STATE_KEY, json);
    }

    private void SaveStats()
    {
        var json = JsonSerializer.Serialize(_stats, JsonOptions);
        _preferences.Set(STATS_KEY, json);
    }
}
