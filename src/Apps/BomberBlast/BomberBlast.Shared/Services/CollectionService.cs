using System.Text.Json;
using BomberBlast.Models;
using BomberBlast.Models.Cards;
using BomberBlast.Models.Collection;
using BomberBlast.Models.Cosmetics;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Sammlungs-Album Service: Aggregiert Daten aus Card/Customization/Discovery + eigenes Tracking.
/// Persistenz: JSON in IPreferencesService (Key: "CollectionData").
/// </summary>
public class CollectionService : ICollectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string PersistenceKey = "CollectionData";

    private readonly IPreferencesService _preferences;
    private readonly ICardService _cardService;
    private readonly ICustomizationService _customizationService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;

    private CollectionData _data;

    public event EventHandler? CollectionChanged;

    public CollectionService(
        IPreferencesService preferences,
        ICardService cardService,
        ICustomizationService customizationService,
        ICoinService coinService,
        IGemService gemService)
    {
        _preferences = preferences;
        _cardService = cardService;
        _customizationService = customizationService;
        _coinService = coinService;
        _gemService = gemService;

        _data = Load();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EINTRÄGE ABFRAGEN
    // ═══════════════════════════════════════════════════════════════════════

    public IReadOnlyList<CollectionEntry> GetEntries(CollectionCategory category)
    {
        return category switch
        {
            CollectionCategory.Enemies => BuildEnemyEntries(),
            CollectionCategory.Bosses => BuildBossEntries(),
            CollectionCategory.PowerUps => BuildPowerUpEntries(),
            CollectionCategory.BombCards => BuildCardEntries(),
            CollectionCategory.Cosmetics => BuildCosmeticEntries(),
            _ => []
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITT
    // ═══════════════════════════════════════════════════════════════════════

    public int GetTotalProgressPercent()
    {
        int discovered = 0;
        int total = 0;

        foreach (CollectionCategory cat in Enum.GetValues<CollectionCategory>())
        {
            discovered += GetDiscoveredCount(cat);
            total += GetTotalCount(cat);
        }

        return total > 0 ? (int)(discovered * 100.0 / total) : 0;
    }

    public int GetCategoryProgressPercent(CollectionCategory category)
    {
        int total = GetTotalCount(category);
        if (total == 0) return 0;
        return (int)(GetDiscoveredCount(category) * 100.0 / total);
    }

    public int GetDiscoveredCount(CollectionCategory category)
    {
        var entries = GetEntries(category);
        return entries.Count(e => e.IsDiscovered);
    }

    public int GetTotalCount(CollectionCategory category)
    {
        return category switch
        {
            CollectionCategory.Enemies => 12,   // 12 EnemyType Werte
            CollectionCategory.Bosses => 5,      // 5 BossType Werte
            CollectionCategory.PowerUps => 12,   // 12 PowerUpType Werte
            CollectionCategory.BombCards => 14,   // 14 BombType Werte
            CollectionCategory.Cosmetics => GetCosmeticTotal(),
            _ => 0
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TRACKING
    // ═══════════════════════════════════════════════════════════════════════

    public void RecordEnemyEncounter(EnemyType type)
    {
        var key = type.ToString();
        if (!_data.EnemyStats.TryGetValue(key, out var stats))
        {
            stats = new EnemyCollectionStats();
            _data.EnemyStats[key] = stats;
        }
        stats.TimesEncountered++;
        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordEnemyDefeat(EnemyType type)
    {
        var key = type.ToString();
        if (!_data.EnemyStats.TryGetValue(key, out var stats))
        {
            stats = new EnemyCollectionStats();
            _data.EnemyStats[key] = stats;
        }
        stats.TimesDefeated++;
        // Encounter implizit mitzählen falls noch nicht geschehen
        if (stats.TimesEncountered < stats.TimesDefeated)
            stats.TimesEncountered = stats.TimesDefeated;
        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordBossEncounter(BossType type)
    {
        var key = type.ToString();
        if (!_data.BossStats.TryGetValue(key, out var stats))
        {
            stats = new BossCollectionStats();
            _data.BossStats[key] = stats;
        }
        stats.TimesEncountered++;
        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordBossDefeat(BossType type, float timeSeconds)
    {
        var key = type.ToString();
        if (!_data.BossStats.TryGetValue(key, out var stats))
        {
            stats = new BossCollectionStats();
            _data.BossStats[key] = stats;
        }
        stats.TimesDefeated++;
        if (stats.TimesEncountered < stats.TimesDefeated)
            stats.TimesEncountered = stats.TimesDefeated;
        // Bestzeit tracken (nur wenn besser oder erste)
        if (stats.BestTimeSeconds <= 0 || timeSeconds < stats.BestTimeSeconds)
            stats.BestTimeSeconds = timeSeconds;
        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordPowerUpCollected(string powerUpId)
    {
        _data.PowerUpCollected.TryGetValue(powerUpId, out int count);
        _data.PowerUpCollected[powerUpId] = count + 1;
        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MEILENSTEINE
    // ═══════════════════════════════════════════════════════════════════════

    public IReadOnlyList<CollectionMilestone> GetMilestones()
    {
        int progress = GetTotalProgressPercent();
        var milestones = CollectionMilestone.All.Select(m => new CollectionMilestone
        {
            PercentRequired = m.PercentRequired,
            CoinReward = m.CoinReward,
            GemReward = m.GemReward,
            IsClaimed = _data.ClaimedMilestones.Contains(m.PercentRequired),
            IsReached = progress >= m.PercentRequired
        }).ToList();
        return milestones;
    }

    public bool TryClaimMilestone(int percentRequired)
    {
        if (_data.ClaimedMilestones.Contains(percentRequired))
            return false;

        int progress = GetTotalProgressPercent();
        if (progress < percentRequired)
            return false;

        var milestone = CollectionMilestone.All.FirstOrDefault(m => m.PercentRequired == percentRequired);
        if (milestone == null)
            return false;

        // Belohnungen vergeben
        if (milestone.CoinReward > 0)
            _coinService.AddCoins(milestone.CoinReward);
        if (milestone.GemReward > 0)
            _gemService.AddGems(milestone.GemReward);

        _data.ClaimedMilestones.Add(percentRequired);
        Save();
        CollectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENTRY BUILDER
    // ═══════════════════════════════════════════════════════════════════════

    private List<CollectionEntry> BuildEnemyEntries()
    {
        var entries = new List<CollectionEntry>();
        foreach (EnemyType type in Enum.GetValues<EnemyType>())
        {
            var key = type.ToString();
            _data.EnemyStats.TryGetValue(key, out var stats);
            bool discovered = stats != null && stats.TimesEncountered > 0;

            entries.Add(new CollectionEntry
            {
                Id = $"enemy_{key.ToLowerInvariant()}",
                Category = CollectionCategory.Enemies,
                NameKey = $"Enemy{key}Name",
                LoreKey = $"Enemy{key}Lore",
                IsDiscovered = discovered,
                TimesEncountered = stats?.TimesEncountered ?? 0,
                TimesDefeated = stats?.TimesDefeated ?? 0,
                IconName = GetEnemyIcon(type)
            });
        }
        return entries;
    }

    private List<CollectionEntry> BuildBossEntries()
    {
        var entries = new List<CollectionEntry>();
        foreach (BossType type in Enum.GetValues<BossType>())
        {
            var key = type.ToString();
            _data.BossStats.TryGetValue(key, out var stats);
            bool discovered = stats != null && stats.TimesEncountered > 0;

            entries.Add(new CollectionEntry
            {
                Id = $"boss_{key.ToLowerInvariant()}",
                Category = CollectionCategory.Bosses,
                NameKey = $"Boss{key}",
                LoreKey = $"Boss{key}Lore",
                IsDiscovered = discovered,
                TimesEncountered = stats?.TimesEncountered ?? 0,
                TimesDefeated = stats?.TimesDefeated ?? 0,
                IconName = GetBossIcon(type)
            });
        }
        return entries;
    }

    private List<CollectionEntry> BuildPowerUpEntries()
    {
        // PowerUp-IDs entsprechen den PowerUpType Enum-Werten
        string[] powerUpIds = ["BombUp", "Fire", "Speed", "Wallpass", "Detonator", "Bombpass",
            "Flamepass", "Mystery", "Kick", "LineBomb", "PowerBomb", "Skull"];
        string[] icons = ["Bomb", "Fire", "FlashOn", "WallOutline", "RadioButtonChecked", "ArrowRightBoldCircleOutline",
            "ShieldFireOutline", "HelpCircle", "ShoePrint", "DotsHorizontal", "StarFourPoints", "SkullOutline"];

        var entries = new List<CollectionEntry>();
        for (int i = 0; i < powerUpIds.Length; i++)
        {
            var id = powerUpIds[i];
            _data.PowerUpCollected.TryGetValue(id, out int collected);

            entries.Add(new CollectionEntry
            {
                Id = $"powerup_{id.ToLowerInvariant()}",
                Category = CollectionCategory.PowerUps,
                NameKey = $"PowerUp{id.ToLowerInvariant()}Name",
                LoreKey = $"PowerUp{id.ToLowerInvariant()}Lore",
                IsDiscovered = collected > 0,
                TimesCollected = collected,
                IconName = icons[i]
            });
        }
        return entries;
    }

    private List<CollectionEntry> BuildCardEntries()
    {
        var entries = new List<CollectionEntry>();
        foreach (var cardDef in CardCatalog.All)
        {
            var owned = _cardService.GetOwnedCard(cardDef.BombType);
            entries.Add(new CollectionEntry
            {
                Id = $"card_{cardDef.BombType.ToString().ToLowerInvariant()}",
                Category = CollectionCategory.BombCards,
                NameKey = cardDef.NameKey,
                LoreKey = cardDef.DescriptionKey,
                IsDiscovered = owned != null,
                IsOwned = owned != null,
                IconName = "CardsPlaying"
            });
        }
        return entries;
    }

    private List<CollectionEntry> BuildCosmeticEntries()
    {
        var entries = new List<CollectionEntry>();

        // Spieler-Skins
        foreach (var skin in _customizationService.AvailablePlayerSkins)
        {
            entries.Add(new CollectionEntry
            {
                Id = $"skin_{skin.Id}",
                Category = CollectionCategory.Cosmetics,
                NameKey = skin.NameKey,
                LoreKey = skin.NameKey, // Skins haben keinen separaten Lore-Text
                IsDiscovered = true, // Skins sind immer im Shop sichtbar
                IsOwned = _customizationService.IsPlayerSkinOwned(skin.Id),
                IconName = "Account"
            });
        }

        // Bomben-Skins
        foreach (var skin in _customizationService.AvailableBombSkins)
        {
            entries.Add(new CollectionEntry
            {
                Id = $"bombskin_{skin.Id}",
                Category = CollectionCategory.Cosmetics,
                NameKey = skin.NameKey,
                LoreKey = skin.NameKey,
                IsDiscovered = true,
                IsOwned = _customizationService.IsBombSkinOwned(skin.Id),
                IconName = "Bomb"
            });
        }

        // Explosions-Skins
        foreach (var skin in _customizationService.AvailableExplosionSkins)
        {
            entries.Add(new CollectionEntry
            {
                Id = $"explosionskin_{skin.Id}",
                Category = CollectionCategory.Cosmetics,
                NameKey = skin.NameKey,
                LoreKey = skin.NameKey,
                IsDiscovered = true,
                IsOwned = _customizationService.IsExplosionSkinOwned(skin.Id),
                IconName = "Flare"
            });
        }

        // Trails
        foreach (var trail in _customizationService.AvailableTrails)
        {
            entries.Add(new CollectionEntry
            {
                Id = $"trail_{trail.Id}",
                Category = CollectionCategory.Cosmetics,
                NameKey = trail.NameKey,
                LoreKey = trail.NameKey,
                IsDiscovered = true,
                IsOwned = _customizationService.IsTrailOwned(trail.Id),
                IconName = "Shimmer"
            });
        }

        // Sieges-Animationen
        foreach (var victory in _customizationService.AvailableVictories)
        {
            entries.Add(new CollectionEntry
            {
                Id = $"victory_{victory.Id}",
                Category = CollectionCategory.Cosmetics,
                NameKey = victory.NameKey,
                LoreKey = victory.NameKey,
                IsDiscovered = true,
                IsOwned = _customizationService.IsVictoryOwned(victory.Id),
                IconName = "PartyPopper"
            });
        }

        // Profilrahmen
        foreach (var frame in _customizationService.AvailableFrames)
        {
            entries.Add(new CollectionEntry
            {
                Id = $"frame_{frame.Id}",
                Category = CollectionCategory.Cosmetics,
                NameKey = frame.NameKey,
                LoreKey = frame.NameKey,
                IsDiscovered = true,
                IsOwned = _customizationService.IsFrameOwned(frame.Id),
                IconName = "ImageFilterFrames"
            });
        }

        return entries;
    }

    private int GetCosmeticTotal()
    {
        return _customizationService.AvailablePlayerSkins.Count
             + _customizationService.AvailableBombSkins.Count
             + _customizationService.AvailableExplosionSkins.Count
             + _customizationService.AvailableTrails.Count
             + _customizationService.AvailableVictories.Count
             + _customizationService.AvailableFrames.Count;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ICONS
    // ═══════════════════════════════════════════════════════════════════════

    private static string GetEnemyIcon(EnemyType type) => type switch
    {
        EnemyType.Ballom => "EmoticonOutline",
        EnemyType.Onil => "EmoticonAngryOutline",
        EnemyType.Doll => "EmoticonCoolOutline",
        EnemyType.Minvo => "EmoticonDevilOutline",
        EnemyType.Kondoria => "Ghost",
        EnemyType.Ovapi => "Jellyfish",
        EnemyType.Pass => "Run",
        EnemyType.Pontan => "Skull",
        EnemyType.Tanker => "Shield",
        EnemyType.Ghost => "GhostOutline",
        EnemyType.Splitter => "ContentCut",
        EnemyType.Mimic => "CubeOutline",
        _ => "HelpCircleOutline"
    };

    private static string GetBossIcon(BossType type) => type switch
    {
        BossType.StoneGolem => "Mountain",
        BossType.IceDragon => "Snowflake",
        BossType.FireDemon => "Fire",
        BossType.ShadowMaster => "WeatherNight",
        BossType.FinalBoss => "Crown",
        _ => "HelpCircleOutline"
    };

    // ═══════════════════════════════════════════════════════════════════════
    // PERSISTENZ
    // ═══════════════════════════════════════════════════════════════════════

    private CollectionData Load()
    {
        var json = _preferences.Get<string>(PersistenceKey, "");
        if (string.IsNullOrEmpty(json))
            return new CollectionData();

        try
        {
            return JsonSerializer.Deserialize<CollectionData>(json, JsonOptions) ?? new CollectionData();
        }
        catch
        {
            return new CollectionData();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        _preferences.Set(PersistenceKey, json);
    }
}
