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
    private IDungeonUpgradeService? _upgradeService;

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

    public int CurrentAscension => _stats.AscensionLevel;

    public float AscensionCoinMultiplier => _stats.AscensionLevel switch
    {
        1 => 1.25f,
        2 => 1.50f,
        3 => 1.75f,
        4 => 2.00f,
        5 => 2.50f,
        _ => 1.00f
    };

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

    /// <summary>
    /// Lazy-Injection: IDungeonUpgradeService wird nach ServiceProvider-Build gesetzt
    /// (vermeidet zirkuläre DI: DungeonService ↔ DungeonUpgradeService)
    /// </summary>
    public void SetUpgradeService(IDungeonUpgradeService upgradeService)
    {
        _upgradeService = upgradeService;
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

        var runSeed = Environment.TickCount;

        // Node-Map generieren (Slay the Spire-Inspiration)
        var mapData = GenerateMap(runSeed);

        // Floor 1: Raum-Typ + Modifikator aus erstem erreichbaren Node lesen
        var firstRow = mapData.Rows[0];
        var firstNode = firstRow.Count == 1 ? firstRow[0] : firstRow[0]; // Erster verfügbarer Node
        var roomType = firstNode.RoomType;
        var modifier = firstNode.Modifier;

        _runState = new DungeonRunState
        {
            CurrentFloor = 1,
            Lives = 1,
            IsActive = true,
            RunSeed = runSeed,
            RunAscension = _stats.AscensionLevel,
            LastFreeRunDate = entryType == DungeonEntryType.Free
                ? DateTime.UtcNow.ToString("O")
                : _runState?.LastFreeRunDate ?? "",
            LastAdRunDate = entryType == DungeonEntryType.Ad
                ? DateTime.UtcNow.ToString("O")
                : _runState?.LastAdRunDate ?? "",
            CurrentRoomType = roomType,
            CurrentModifier = modifier,
            MapData = mapData
        };

        if (_runState.CurrentRoomType == DungeonRoomType.Challenge)
            _runState.CurrentChallengeMode = GenerateChallengeMode(1, runSeed);

        SaveRunState();
        RunStateChanged?.Invoke();
        return true;
    }

    public DungeonFloorReward CompleteFloor()
    {
        if (_runState == null || !_runState.IsActive)
            return new DungeonFloorReward();

        var floor = _runState.CurrentFloor;
        var isBoss = DungeonBuffCatalog.IsBossFloor(floor);
        var reward = CalculateFloorReward(floor);

        // Raum-Typ-Bonus: Elite +50% Coins, Treasure garantierter Card-Drop
        if (_runState.CurrentRoomType == DungeonRoomType.Elite)
        {
            reward.Coins = (int)(reward.Coins * 1.5f);
        }
        else if (_runState.CurrentRoomType == DungeonRoomType.Treasure && reward.CardDrop < 0)
        {
            // Treasure: Garantierter Karten-Drop
            reward.CardDrop = (int)GenerateCardDrop(minRarity: 1);
        }

        // Modifikator-Bonus: Wealthy = 3x Coins (stackt mit anderen Boni)
        if (_runState.CurrentModifier == DungeonFloorModifier.Wealthy)
        {
            reward.Coins *= 3;
        }

        // Permanentes Upgrade: BossGoldBonus (+25%/+50% Boss-Belohnungen)
        if (isBoss && _upgradeService != null)
        {
            int bossGoldLevel = _upgradeService.GetUpgradeLevel(DungeonUpgradeCatalog.BossGoldBonus);
            if (bossGoldLevel > 0)
            {
                float bossMultiplier = 1f + bossGoldLevel * 0.25f;
                reward.Coins = (int)(reward.Coins * bossMultiplier);
                reward.ChestBonus = (int)(reward.ChestBonus * bossMultiplier);
            }
        }

        // Buff-basierte Coin-Bonus-Berechnung
        if (_runState.ActiveBuffs.Contains(DungeonBuffType.GoldRush))
        {
            // Legendärer GoldRush: 3x Coins (stärker als CoinBonus, stackt nicht)
            reward.Coins *= 3;
            reward.ChestBonus *= 3;
        }
        else if (_runState.ActiveBuffs.Contains(DungeonBuffType.CoinBonus))
        {
            reward.Coins = (int)(reward.Coins * 1.5);
            reward.ChestBonus = (int)(reward.ChestBonus * 1.5);
        }

        // Ascension Coin-Multiplikator anwenden
        int runAscension = _runState.RunAscension;
        float ascensionMultiplier = runAscension switch
        {
            1 => 1.25f,
            2 => 1.50f,
            3 => 1.75f,
            4 => 2.00f,
            5 => 2.50f,
            _ => 1.00f
        };
        reward.Coins = (int)(reward.Coins * ascensionMultiplier);
        reward.ChestBonus = (int)(reward.ChestBonus * ascensionMultiplier);

        // DungeonCoins berechnen (separate Währung für permanente Upgrades)
        int dungeonCoins = isBoss
            ? (floor % 10 == 5 ? 50 : 100) // Mini-Boss 50, End-Boss 100
            : 10 + floor * 2;               // Normal: 10-30 DC
        reward.DungeonCoins = dungeonCoins;

        // Belohnungen zum Run-State hinzufügen
        _runState.CollectedCoins += reward.Coins + reward.ChestBonus;
        _runState.CollectedGems += reward.Gems;
        _runState.CollectedDungeonCoins += dungeonCoins;

        if (reward.CardDrop >= 0)
            _runState.CollectedCardDrops.Add(reward.CardDrop);

        // Permanentes Upgrade: CardDropBoost (+15%/+30% Karten-Drop-Chance)
        // (schon in CalculateFloorReward berücksichtigt wäre komplex, stattdessen Bonus-Drop-Chance)
        if (reward.CardDrop < 0 && _upgradeService != null)
        {
            int cardDropLevel = _upgradeService.GetUpgradeLevel(DungeonUpgradeCatalog.CardDropBoost);
            if (cardDropLevel > 0)
            {
                double bonusChance = cardDropLevel * 0.15;
                if (_random.NextDouble() < bonusChance)
                {
                    reward.CardDrop = (int)GenerateCardDrop(minRarity: 0);
                    _runState.CollectedCardDrops.Add(reward.CardDrop);
                }
            }
        }

        // Map-Node als abgeschlossen markieren
        CompleteMapNode(floor);

        // Nächster Floor
        _runState.CurrentFloor++;

        // Rest-Raum-Tracking aktualisieren
        if (_runState.CurrentRoomType == DungeonRoomType.Rest)
            _runState.RestRoomsInLastFive++;
        // Alle 5 Floors den Zähler zurücksetzen
        if (_runState.CurrentFloor % 5 == 1)
            _runState.RestRoomsInLastFive = 0;

        // Raum-Typ + Modifikator für nächsten Floor:
        // Wenn Map vorhanden und nächster Floor nur 1 erreichbaren Node hat → automatisch wählen
        // Sonst wird der Spieler per SelectMapNode in DungeonViewModel entscheiden
        if (_runState.MapData != null && _runState.CurrentFloor <= _runState.MapData.Rows.Count)
        {
            var nextRow = _runState.MapData.Rows[_runState.CurrentFloor - 1];
            var reachable = nextRow.FindAll(n => n.IsReachable);
            if (reachable.Count == 1)
            {
                // Einziger Node: automatisch auswählen (z.B. Boss-Floors)
                SelectMapNode(_runState.CurrentFloor, reachable[0].Column);
            }
            // Bei mehreren Nodes: Spieler muss wählen (CurrentRoomType wird in SelectMapNode gesetzt)
        }
        else
        {
            // Fallback ohne Map
            _runState.CurrentRoomType = GenerateRoomType(_runState.CurrentFloor, _runState.RunSeed);
            _runState.CurrentModifier = GenerateFloorModifier(_runState.CurrentFloor, _runState.RunSeed);
            if (_runState.CurrentRoomType == DungeonRoomType.Challenge)
                _runState.CurrentChallengeMode = GenerateChallengeMode(_runState.CurrentFloor, _runState.RunSeed);
        }

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

        // DungeonCoins auszahlen (permanente Upgrade-Währung)
        if (_runState.CollectedDungeonCoins > 0)
            _upgradeService?.AddDungeonCoins(_runState.CollectedDungeonCoins);

        // Karten hinzufügen
        foreach (var cardType in _runState.CollectedCardDrops)
            _cardService.AddCard((BombType)cardType);

        // Statistiken aktualisieren
        _stats.TotalRuns++;
        if (isNewBest) _stats.BestFloor = floorsCompleted;
        _stats.TotalCoinsEarned += _runState.CollectedCoins;
        _stats.TotalGemsEarned += _runState.CollectedGems;
        _stats.TotalCardsEarned += _runState.CollectedCardDrops.Count;
        _stats.TotalDungeonCoinsEarned += _runState.CollectedDungeonCoins;

        // Ascension-Aufstieg: Floor 10 bei aktuellem Ascension-Level geschafft
        bool ascensionLevelUp = false;
        int newAscension = _stats.AscensionLevel;
        if (floorsCompleted >= 10 &&
            _runState.RunAscension == _stats.AscensionLevel &&
            _stats.AscensionLevel < 5)
        {
            _stats.AscensionLevel++;
            newAscension = _stats.AscensionLevel;
            ascensionLevelUp = true;
            if (_stats.AscensionLevel > _stats.HighestAscension)
                _stats.HighestAscension = _stats.AscensionLevel;
        }

        var summary = new DungeonRunSummary
        {
            FloorsCompleted = floorsCompleted,
            TotalCoins = _runState.CollectedCoins,
            TotalGems = _runState.CollectedGems,
            TotalCards = _runState.CollectedCardDrops.Count,
            TotalDungeonCoins = _runState.CollectedDungeonCoins,
            IsNewBestFloor = isNewBest,
            UsedBuffs = [.._runState.ActiveBuffs],
            AscensionLevelUp = ascensionLevelUp,
            NewAscensionLevel = newAscension
        };

        // Run beenden
        _runState.IsActive = false;
        _runState.ActiveBuffs.Clear();
        _runState.CollectedCoins = 0;
        _runState.CollectedGems = 0;
        _runState.CollectedDungeonCoins = 0;
        _runState.CollectedCardDrops.Clear();
        _runState.FreeRerollsUsed = 0;
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

        // Permanentes Upgrade: ExtraBuffChoice → 4 statt 3 Optionen
        int choiceCount = 3;
        if (_upgradeService != null &&
            _upgradeService.GetUpgradeLevel(DungeonUpgradeCatalog.ExtraBuffChoice) >= 1)
            choiceCount = 4;

        // Seed basierend auf Run-Seed + Floor für Determinismus
        var buffRandom = new Random(_runState.RunSeed + _runState.CurrentFloor * 100);
        var available = DungeonBuffCatalog.All
            .Where(b => !_runState.ActiveBuffs.Contains(b.Type) ||
                        b.Type == DungeonBuffType.ExtraBomb ||
                        b.Type == DungeonBuffType.ExtraFire ||
                        b.Type == DungeonBuffType.ExtraLife)
            .ToList();

        // Ascension 4+: Legendäre Buffs halb so wahrscheinlich
        int ascension = _runState?.RunAscension ?? _stats.AscensionLevel;
        if (ascension >= 4)
        {
            for (int i = 0; i < available.Count; i++)
            {
                if (available[i].Rarity == DungeonBuffRarity.Legendary)
                {
                    // Neues Objekt mit halbiertem Weight erstellen (Original nicht mutieren)
                    available[i] = new DungeonBuffDefinition
                    {
                        Type = available[i].Type,
                        NameKey = available[i].NameKey,
                        DescKey = available[i].DescKey,
                        IconName = available[i].IconName,
                        Rarity = available[i].Rarity,
                        Weight = Math.Max(1, available[i].Weight / 2)
                    };
                }
            }
        }

        // Gewichtete Auswahl
        var choices = new List<DungeonBuffDefinition>(choiceCount);
        var totalWeight = available.Sum(b => b.Weight);

        for (int i = 0; i < choiceCount && available.Count > 0; i++)
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
            case DungeonBuffType.Berserker:
                // +2 Bomben, +2 Feuer, aber -1 Leben (riskant!)
                // Bomben/Feuer werden in GameEngine.ApplyDungeonBuffs() angewendet
                if (_runState.Lives > 1) _runState.Lives--;
                break;
            case DungeonBuffType.TimeFreeze:
                // Alle Gegner 3s eingefroren bei Floor-Start → GameEngine
                break;
            case DungeonBuffType.GoldRush:
                // 3x Coin-Belohnungen für Rest des Runs → CompleteFloor()
                break;
            case DungeonBuffType.Phantom:
                // Spieler kann 5s durch Wände laufen (Cooldown 30s) → GameEngine
                break;
        }

        SaveRunState();
        RunStateChanged?.Invoke();
    }

    // === Raum-Typ + Modifikator-Generierung (B3+B4) ===

    /// <summary>
    /// Generiert den Raum-Typ für einen Floor (gewichtete Zufallsauswahl, Seed-basiert).
    /// Boss-Floors sind immer Normal. Rest max 1 pro 5 Floors.
    /// </summary>
    public DungeonRoomType GenerateRoomType(int floor, int seed)
    {
        // Boss-Floors sind immer Normal
        if (DungeonBuffCatalog.IsBossFloor(floor))
            return DungeonRoomType.Normal;

        var rng = new Random(seed + floor * 777);

        // Rest-Raum Begrenzung: max 1 pro 5 Floors
        bool canRest = (_runState?.RestRoomsInLastFive ?? 0) < 1;

        // Ascension-Effekt: Ab Level 2 mehr Elite-Floors
        int ascension = _runState?.RunAscension ?? _stats.AscensionLevel;
        int normalW = 40;
        int eliteW = ascension >= 2 ? 30 : 20; // Ascension 2+: 30% statt 20%
        int treasureW = 15;
        int challengeW = 15;
        int restW = canRest ? 10 : 0;
        int total = normalW + eliteW + treasureW + challengeW + restW;

        int roll = rng.Next(total);
        if (roll < normalW) return DungeonRoomType.Normal;
        roll -= normalW;
        if (roll < eliteW) return DungeonRoomType.Elite;
        roll -= eliteW;
        if (roll < treasureW) return DungeonRoomType.Treasure;
        roll -= treasureW;
        if (roll < challengeW) return DungeonRoomType.Challenge;
        return DungeonRoomType.Rest;
    }

    /// <summary>
    /// Generiert einen Floor-Modifikator (30% Chance ab Floor 3, Seed-basiert).
    /// </summary>
    public DungeonFloorModifier GenerateFloorModifier(int floor, int seed)
    {
        int ascension = _runState?.RunAscension ?? _stats.AscensionLevel;

        // Ascension 3+: Modifikatoren auf JEDEM Floor (auch Floor 1-2)
        if (ascension < 3 && floor < 3) return DungeonFloorModifier.None;

        var rng = new Random(seed + floor * 333);

        // Ascension 3+: 100% Chance statt 30%
        double modifierChance = ascension >= 3 ? 1.0 : 0.3;
        if (rng.NextDouble() >= modifierChance) return DungeonFloorModifier.None;

        var modifiers = new[]
        {
            DungeonFloorModifier.LavaBorders,
            DungeonFloorModifier.Darkness,
            DungeonFloorModifier.DoubleSpawns,
            DungeonFloorModifier.FastBombs,
            DungeonFloorModifier.BigExplosions,
            DungeonFloorModifier.Regeneration,
            DungeonFloorModifier.Wealthy
        };

        return modifiers[rng.Next(modifiers.Length)];
    }

    /// <summary>
    /// Generiert Challenge-Modus für Challenge-Räume (Seed-basiert).
    /// </summary>
    public DungeonChallengeMode GenerateChallengeMode(int floor, int seed)
    {
        var rng = new Random(seed + floor * 555);
        return (DungeonChallengeMode)(rng.Next(3));
    }

    // === Node-Map-Generierung (B6) ===

    /// <summary>
    /// Generiert eine verzweigte Dungeon-Map (Slay the Spire-Inspiration).
    /// 10 Reihen (Floors), 2-3 Nodes pro Reihe, Boss-Floors (5+10) haben nur 1 Node.
    /// Seed-basiert für deterministische Generierung.
    /// </summary>
    public DungeonMapData GenerateMap(int seed)
    {
        var rng = new Random(seed + 12345);
        var mapData = new DungeonMapData();
        int restRoomsUsed = 0;

        for (int floor = 1; floor <= 10; floor++)
        {
            var row = new List<DungeonMapNode>();
            bool isBoss = DungeonBuffCatalog.IsBossFloor(floor);

            // Boss-Floors: Immer nur 1 zentraler Node
            int nodeCount = isBoss ? 1 : (rng.Next(100) < 40 ? 2 : 3);

            for (int col = 0; col < nodeCount; col++)
            {
                var roomType = DungeonRoomType.Normal;
                var modifier = DungeonFloorModifier.None;
                var challengeMode = DungeonChallengeMode.SpeedRun;

                if (isBoss)
                {
                    roomType = DungeonRoomType.Normal; // Boss ist immer "Normal"
                }
                else
                {
                    // Raum-Typ per Seed generieren (analog GenerateRoomType, aber eigener Seed pro Node)
                    var nodeRng = new Random(seed + floor * 777 + col * 111);
                    bool canRest = restRoomsUsed < (floor / 5 + 1); // Max ~1 pro 5 Floors
                    int ascension = _stats.AscensionLevel;
                    int normalW = 40, eliteW = ascension >= 2 ? 30 : 20, treasureW = 15, challengeW = 15;
                    int restW = canRest ? 10 : 0;
                    int total = normalW + eliteW + treasureW + challengeW + restW;
                    int roll = nodeRng.Next(total);

                    if (roll < normalW) roomType = DungeonRoomType.Normal;
                    else if ((roll -= normalW) < eliteW) roomType = DungeonRoomType.Elite;
                    else if ((roll -= eliteW) < treasureW) roomType = DungeonRoomType.Treasure;
                    else if ((roll - treasureW) < challengeW) roomType = DungeonRoomType.Challenge;
                    else roomType = DungeonRoomType.Rest;

                    if (roomType == DungeonRoomType.Rest)
                        restRoomsUsed++;

                    if (roomType == DungeonRoomType.Challenge)
                        challengeMode = (DungeonChallengeMode)(nodeRng.Next(3));

                    // Modifikator ab Floor 3 (Ascension 3+: ab Floor 1, 100% Chance)
                    bool canHaveModifier = ascension >= 3 || floor >= 3;
                    if (canHaveModifier)
                    {
                        var modRng = new Random(seed + floor * 333 + col * 222);
                        double modChance = ascension >= 3 ? 1.0 : 0.3;
                        if (modRng.NextDouble() < modChance)
                        {
                            var mods = new[]
                            {
                                DungeonFloorModifier.LavaBorders, DungeonFloorModifier.Darkness,
                                DungeonFloorModifier.DoubleSpawns, DungeonFloorModifier.FastBombs,
                                DungeonFloorModifier.BigExplosions, DungeonFloorModifier.Regeneration,
                                DungeonFloorModifier.Wealthy
                            };
                            modifier = mods[modRng.Next(mods.Length)];
                        }
                    }
                }

                row.Add(new DungeonMapNode
                {
                    Floor = floor,
                    Column = col,
                    RoomType = roomType,
                    Modifier = modifier,
                    ChallengeMode = challengeMode
                });
            }

            // Verbindungen zur nächsten Reihe generieren
            if (floor < 10)
            {
                bool nextIsBoss = DungeonBuffCatalog.IsBossFloor(floor + 1);
                int nextCount = nextIsBoss ? 1 : (rng.Next(100) < 40 ? 2 : 3);

                foreach (var node in row)
                {
                    if (nextIsBoss)
                    {
                        // Alle Nodes verbinden zum einzigen Boss-Node
                        node.ConnectsTo = [0];
                    }
                    else
                    {
                        // 1-2 Verbindungen, bevorzugt benachbarte Spalten
                        var connections = new HashSet<int>();
                        int preferredCol = Math.Min(node.Column, nextCount - 1);
                        connections.Add(preferredCol);

                        // Zweite Verbindung mit 50% Chance
                        if (rng.Next(100) < 50 && nextCount > 1)
                        {
                            int altCol = preferredCol + (rng.Next(2) == 0 ? -1 : 1);
                            altCol = Math.Clamp(altCol, 0, nextCount - 1);
                            connections.Add(altCol);
                        }

                        node.ConnectsTo = connections.ToList();
                    }
                }
            }

            mapData.Rows.Add(row);
        }

        // ChosenColumns initialisieren (alle -1 = nicht gewählt)
        mapData.ChosenColumns = Enumerable.Repeat(-1, 10).ToList();

        // Erreichbarkeit für Floor 1 setzen
        foreach (var node in mapData.Rows[0])
            node.IsReachable = true;

        return mapData;
    }

    /// <summary>
    /// Wählt einen Node auf der Map aus und setzt RoomType/Modifier entsprechend.
    /// </summary>
    public void SelectMapNode(int floor, int column)
    {
        if (_runState?.MapData == null) return;
        if (floor < 1 || floor > _runState.MapData.Rows.Count) return;

        var row = _runState.MapData.Rows[floor - 1];
        var node = row.FirstOrDefault(n => n.Column == column);
        if (node == null || !node.IsReachable) return;

        // Markiere als aktuell
        node.IsCurrent = true;

        // RunState aktualisieren
        _runState.CurrentRoomType = node.RoomType;
        _runState.CurrentModifier = node.Modifier;
        _runState.CurrentChallengeMode = node.ChallengeMode;
        _runState.MapData.ChosenColumns[floor - 1] = column;

        // Erreichbarkeit für nächste Reihe berechnen
        if (floor < _runState.MapData.Rows.Count)
        {
            var nextRow = _runState.MapData.Rows[floor];
            foreach (var nextNode in nextRow)
                nextNode.IsReachable = false;

            foreach (var connectedCol in node.ConnectsTo)
            {
                var target = nextRow.FirstOrDefault(n => n.Column == connectedCol);
                if (target != null)
                    target.IsReachable = true;
            }
        }

        SaveRunState();
        RunStateChanged?.Invoke();
    }

    /// <summary>
    /// Markiert den aktuellen Floor-Node als abgeschlossen.
    /// </summary>
    public void CompleteMapNode(int floor)
    {
        if (_runState?.MapData == null) return;
        if (floor < 1 || floor > _runState.MapData.Rows.Count) return;

        var row = _runState.MapData.Rows[floor - 1];
        foreach (var node in row)
        {
            if (node.IsCurrent)
            {
                node.IsCompleted = true;
                node.IsCurrent = false;
            }
        }

        SaveRunState();
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

    /// <inheritdoc />
    public void PersistRunState() => SaveRunState();

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
