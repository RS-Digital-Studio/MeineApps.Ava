using BomberBlast.Models;
using BomberBlast.Models.Cosmetics;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistiert Skin-Auswahl via IPreferencesService.
/// Premium-Skins nur nutzbar wenn IPurchaseService.IsPremium == true.
/// Bomben-/Explosions-Skins werden per Coins gekauft und persistent gespeichert.
/// </summary>
public class CustomizationService : ICustomizationService
{
    private const string PLAYER_SKIN_KEY = "PlayerSkin";
    private const string ENEMY_SKIN_KEY = "EnemySkinSet";
    private const string BOMB_SKIN_KEY = "BombSkin";
    private const string EXPLOSION_SKIN_KEY = "ExplosionSkin";
    private const string OWNED_BOMB_SKINS_KEY = "OwnedBombSkins";
    private const string OWNED_EXPLOSION_SKINS_KEY = "OwnedExplosionSkins";
    private const string OWNED_PLAYER_SKINS_KEY = "OwnedPlayerSkins";
    private const string TRAIL_KEY = "ActiveTrail";
    private const string OWNED_TRAILS_KEY = "OwnedTrails";
    private const string VICTORY_KEY = "ActiveVictory";
    private const string OWNED_VICTORIES_KEY = "OwnedVictories";
    private const string FRAME_KEY = "ActiveFrame";
    private const string OWNED_FRAMES_KEY = "OwnedFrames";

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private SkinDefinition _playerSkin;
    private BombSkinDefinition _bombSkin;
    private ExplosionSkinDefinition _explosionSkin;
    private TrailDefinition? _activeTrail;
    private VictoryDefinition? _activeVictory;
    private FrameDefinition? _activeFrame;
    private readonly HashSet<string> _ownedPlayerSkins;
    private readonly HashSet<string> _ownedBombSkins;
    private readonly HashSet<string> _ownedExplosionSkins;
    private readonly HashSet<string> _ownedTrails;
    private readonly HashSet<string> _ownedVictories;
    private readonly HashSet<string> _ownedFrames;

    public SkinDefinition PlayerSkin => _playerSkin;
    public string EnemySkinSet { get; private set; }
    public BombSkinDefinition BombSkin => _bombSkin;
    public ExplosionSkinDefinition ExplosionSkin => _explosionSkin;
    public TrailDefinition? ActiveTrail => _activeTrail;
    public VictoryDefinition? ActiveVictory => _activeVictory;
    public FrameDefinition? ActiveFrame => _activeFrame;
    public IReadOnlyList<SkinDefinition> AvailablePlayerSkins => PlayerSkins.All;
    public IReadOnlyList<BombSkinDefinition> AvailableBombSkins => BombSkins.All;
    public IReadOnlyList<ExplosionSkinDefinition> AvailableExplosionSkins => ExplosionSkins.All;
    public IReadOnlyList<TrailDefinition> AvailableTrails => TrailDefinitions.All;
    public IReadOnlyList<VictoryDefinition> AvailableVictories => VictoryDefinitions.All;
    public IReadOnlyList<FrameDefinition> AvailableFrames => FrameDefinitions.All;

    public CustomizationService(IPreferencesService preferences, ICoinService coinService)
    {
        _preferences = preferences;
        _coinService = coinService;

        // Gespeicherte Auswahl laden
        var savedSkinId = _preferences.Get(PLAYER_SKIN_KEY, "default");
        _playerSkin = FindPlayerSkin(savedSkinId);

        EnemySkinSet = _preferences.Get(ENEMY_SKIN_KEY, "default");

        var savedBombSkinId = _preferences.Get(BOMB_SKIN_KEY, "bomb_default");
        _bombSkin = FindBombSkin(savedBombSkinId);

        var savedExplosionSkinId = _preferences.Get(EXPLOSION_SKIN_KEY, "expl_default");
        _explosionSkin = FindExplosionSkin(savedExplosionSkinId);

        // Gekaufte Skins laden (kommaseparierte IDs)
        var ownedBombs = _preferences.Get(OWNED_BOMB_SKINS_KEY, "bomb_default");
        _ownedBombSkins = new HashSet<string>(ownedBombs.Split(',', StringSplitOptions.RemoveEmptyEntries));
        _ownedBombSkins.Add("bomb_default"); // Standard immer verfuegbar

        var ownedExplosions = _preferences.Get(OWNED_EXPLOSION_SKINS_KEY, "expl_default");
        _ownedExplosionSkins = new HashSet<string>(ownedExplosions.Split(',', StringSplitOptions.RemoveEmptyEntries));
        _ownedExplosionSkins.Add("expl_default"); // Standard immer verfuegbar

        // Gekaufte Spieler-Skins laden
        var ownedPlayers = _preferences.Get(OWNED_PLAYER_SKINS_KEY, "default");
        _ownedPlayerSkins = new HashSet<string>(ownedPlayers.Split(',', StringSplitOptions.RemoveEmptyEntries));
        _ownedPlayerSkins.Add("default"); // Standard immer verfuegbar

        // Trail laden
        var savedTrailId = _preferences.Get(TRAIL_KEY, "");
        _activeTrail = string.IsNullOrEmpty(savedTrailId) ? null : FindTrail(savedTrailId);
        var ownedTrails = _preferences.Get(OWNED_TRAILS_KEY, "");
        _ownedTrails = new HashSet<string>(ownedTrails.Split(',', StringSplitOptions.RemoveEmptyEntries));

        // Sieges-Animation laden
        var savedVictoryId = _preferences.Get(VICTORY_KEY, "");
        _activeVictory = string.IsNullOrEmpty(savedVictoryId) ? null : FindVictory(savedVictoryId);
        var ownedVictories = _preferences.Get(OWNED_VICTORIES_KEY, "");
        _ownedVictories = new HashSet<string>(ownedVictories.Split(',', StringSplitOptions.RemoveEmptyEntries));

        // Profilrahmen laden
        var savedFrameId = _preferences.Get(FRAME_KEY, "");
        _activeFrame = string.IsNullOrEmpty(savedFrameId) ? null : FindFrame(savedFrameId);
        var ownedFrames = _preferences.Get(OWNED_FRAMES_KEY, "");
        _ownedFrames = new HashSet<string>(ownedFrames.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    // === Spieler-Skins ===

    public void SetPlayerSkin(string skinId)
    {
        _playerSkin = FindPlayerSkin(skinId);
        _preferences.Set(PLAYER_SKIN_KEY, _playerSkin.Id);
    }

    public bool IsPlayerSkinOwned(string skinId)
    {
        var skin = FindPlayerSkin(skinId);
        // Premium-Only Skins ohne Preis = Premium-Feature, keine Coin-Zahlung
        if (skin.IsPremiumOnly && skin.CoinPrice <= 0) return true; // Verfuegbar wenn Premium aktiv
        // Kostenlose Skins (CoinPrice=0, nicht premium) = immer owned
        if (skin.CoinPrice <= 0) return true;
        return _ownedPlayerSkins.Contains(skinId);
    }

    public bool TryPurchasePlayerSkin(string skinId)
    {
        if (_ownedPlayerSkins.Contains(skinId)) return false;

        var skin = FindPlayerSkin(skinId);
        if (skin.CoinPrice <= 0) return false;

        if (!_coinService.TrySpendCoins(skin.CoinPrice)) return false;

        _ownedPlayerSkins.Add(skinId);
        SaveOwnedPlayerSkins();
        return true;
    }

    public void SetEnemySkinSet(string setId)
    {
        EnemySkinSet = EnemySkinSets.All.Contains(setId) ? setId : EnemySkinSets.Default;
        _preferences.Set(ENEMY_SKIN_KEY, EnemySkinSet);
    }

    // === Bomben-Skins ===

    public void SetBombSkin(string skinId)
    {
        _bombSkin = FindBombSkin(skinId);
        _preferences.Set(BOMB_SKIN_KEY, _bombSkin.Id);
    }

    public bool IsBombSkinOwned(string skinId)
    {
        return _ownedBombSkins.Contains(skinId);
    }

    public bool TryPurchaseBombSkin(string skinId)
    {
        if (_ownedBombSkins.Contains(skinId)) return false;

        var skin = FindBombSkin(skinId);
        if (skin.CoinPrice <= 0) return false;

        if (!_coinService.TrySpendCoins(skin.CoinPrice)) return false;

        _ownedBombSkins.Add(skinId);
        SaveOwnedBombSkins();
        return true;
    }

    // === Explosions-Skins ===

    public void SetExplosionSkin(string skinId)
    {
        _explosionSkin = FindExplosionSkin(skinId);
        _preferences.Set(EXPLOSION_SKIN_KEY, _explosionSkin.Id);
    }

    public bool IsExplosionSkinOwned(string skinId)
    {
        return _ownedExplosionSkins.Contains(skinId);
    }

    public bool TryPurchaseExplosionSkin(string skinId)
    {
        if (_ownedExplosionSkins.Contains(skinId)) return false;

        var skin = FindExplosionSkin(skinId);
        if (skin.CoinPrice <= 0) return false;

        if (!_coinService.TrySpendCoins(skin.CoinPrice)) return false;

        _ownedExplosionSkins.Add(skinId);
        SaveOwnedExplosionSkins();
        return true;
    }

    // === Trails ===

    public void SetTrail(string? trailId)
    {
        if (string.IsNullOrEmpty(trailId))
        {
            _activeTrail = null;
            _preferences.Set(TRAIL_KEY, "");
            return;
        }
        _activeTrail = FindTrail(trailId);
        _preferences.Set(TRAIL_KEY, _activeTrail?.Id ?? "");
    }

    public bool IsTrailOwned(string trailId) => _ownedTrails.Contains(trailId);

    public bool TryPurchaseTrail(string trailId)
    {
        if (_ownedTrails.Contains(trailId)) return false;
        var trail = FindTrail(trailId);
        if (trail == null || trail.CoinPrice <= 0) return false;
        if (!_coinService.TrySpendCoins(trail.CoinPrice)) return false;
        _ownedTrails.Add(trailId);
        _preferences.Set(OWNED_TRAILS_KEY, string.Join(",", _ownedTrails));
        return true;
    }

    // === Sieges-Animationen ===

    public void SetVictory(string? victoryId)
    {
        if (string.IsNullOrEmpty(victoryId))
        {
            _activeVictory = null;
            _preferences.Set(VICTORY_KEY, "");
            return;
        }
        _activeVictory = FindVictory(victoryId);
        _preferences.Set(VICTORY_KEY, _activeVictory?.Id ?? "");
    }

    public bool IsVictoryOwned(string victoryId) => _ownedVictories.Contains(victoryId);

    public bool TryPurchaseVictory(string victoryId)
    {
        if (_ownedVictories.Contains(victoryId)) return false;
        var victory = FindVictory(victoryId);
        if (victory == null || victory.CoinPrice <= 0) return false;
        if (!_coinService.TrySpendCoins(victory.CoinPrice)) return false;
        _ownedVictories.Add(victoryId);
        _preferences.Set(OWNED_VICTORIES_KEY, string.Join(",", _ownedVictories));
        return true;
    }

    // === Profilrahmen ===

    public void SetFrame(string? frameId)
    {
        if (string.IsNullOrEmpty(frameId))
        {
            _activeFrame = null;
            _preferences.Set(FRAME_KEY, "");
            return;
        }
        _activeFrame = FindFrame(frameId);
        _preferences.Set(FRAME_KEY, _activeFrame?.Id ?? "");
    }

    public bool IsFrameOwned(string frameId) => _ownedFrames.Contains(frameId);

    public bool TryPurchaseFrame(string frameId)
    {
        if (_ownedFrames.Contains(frameId)) return false;
        var frame = FindFrame(frameId);
        if (frame == null || frame.CoinPrice <= 0) return false;
        if (!_coinService.TrySpendCoins(frame.CoinPrice)) return false;
        _ownedFrames.Add(frameId);
        _preferences.Set(OWNED_FRAMES_KEY, string.Join(",", _ownedFrames));
        return true;
    }

    // === Persistenz-Helfer ===

    private void SaveOwnedPlayerSkins()
    {
        _preferences.Set(OWNED_PLAYER_SKINS_KEY, string.Join(",", _ownedPlayerSkins));
    }

    private void SaveOwnedBombSkins()
    {
        _preferences.Set(OWNED_BOMB_SKINS_KEY, string.Join(",", _ownedBombSkins));
    }

    private void SaveOwnedExplosionSkins()
    {
        _preferences.Set(OWNED_EXPLOSION_SKINS_KEY, string.Join(",", _ownedExplosionSkins));
    }

    // === Lookup-Helfer ===

    private static SkinDefinition FindPlayerSkin(string skinId)
    {
        foreach (var skin in PlayerSkins.All)
            if (skin.Id == skinId) return skin;
        return PlayerSkins.Default;
    }

    private static BombSkinDefinition FindBombSkin(string skinId)
    {
        foreach (var skin in BombSkins.All)
            if (skin.Id == skinId) return skin;
        return BombSkins.Default;
    }

    private static ExplosionSkinDefinition FindExplosionSkin(string skinId)
    {
        foreach (var skin in ExplosionSkins.All)
            if (skin.Id == skinId) return skin;
        return ExplosionSkins.Default;
    }

    private static TrailDefinition? FindTrail(string trailId)
    {
        foreach (var trail in TrailDefinitions.All)
            if (trail.Id == trailId) return trail;
        return null;
    }

    private static VictoryDefinition? FindVictory(string victoryId)
    {
        foreach (var victory in VictoryDefinitions.All)
            if (victory.Id == victoryId) return victory;
        return null;
    }

    private static FrameDefinition? FindFrame(string frameId)
    {
        foreach (var frame in FrameDefinitions.All)
            if (frame.Id == frameId) return frame;
        return null;
    }
}
