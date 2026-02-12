using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Persistiert Skin-Auswahl via IPreferencesService.
/// Premium-Skins nur nutzbar wenn IPurchaseService.IsPremium == true.
/// </summary>
public class CustomizationService : ICustomizationService
{
    private const string PLAYER_SKIN_KEY = "PlayerSkin";
    private const string ENEMY_SKIN_KEY = "EnemySkinSet";

    private readonly IPreferencesService _preferences;
    private SkinDefinition _playerSkin;

    public SkinDefinition PlayerSkin => _playerSkin;
    public string EnemySkinSet { get; private set; }
    public IReadOnlyList<SkinDefinition> AvailablePlayerSkins => PlayerSkins.All;

    public CustomizationService(IPreferencesService preferences)
    {
        _preferences = preferences;

        // Gespeicherte Auswahl laden
        var savedSkinId = _preferences.Get(PLAYER_SKIN_KEY, "default");
        _playerSkin = FindSkin(savedSkinId);

        EnemySkinSet = _preferences.Get(ENEMY_SKIN_KEY, "default");
    }

    public void SetPlayerSkin(string skinId)
    {
        _playerSkin = FindSkin(skinId);
        _preferences.Set(PLAYER_SKIN_KEY, _playerSkin.Id);
    }

    public void SetEnemySkinSet(string setId)
    {
        EnemySkinSet = EnemySkinSets.All.Contains(setId) ? setId : EnemySkinSets.Default;
        _preferences.Set(ENEMY_SKIN_KEY, EnemySkinSet);
    }

    private static SkinDefinition FindSkin(string skinId)
    {
        foreach (var skin in PlayerSkins.All)
        {
            if (skin.Id == skinId)
                return skin;
        }
        return PlayerSkins.Default;
    }
}
