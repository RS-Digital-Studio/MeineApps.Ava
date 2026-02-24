using HandwerkerImperium.Models.Cosmetics;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet kosmetische Anpassungen (City-Themes, Workshop-Skins).
/// Freigeschaltete Kosmetiken werden im GameState persistiert.
/// Quellen: BattlePass-Capstone, Ascension, Gilden-Krieg, Prestige-Shop.
/// </summary>
public class CosmeticService : ICosmeticService
{
    private readonly IGameStateService _gameState;

    public event Action? CosmeticChanged;

    public CosmeticService(IGameStateService gameState)
    {
        _gameState = gameState;

        // Standard-Theme ist immer freigeschaltet
        if (!_gameState.State.UnlockedCosmetics.Contains("ct_default"))
            _gameState.State.UnlockedCosmetics.Add("ct_default");
    }

    public CityTheme? ActiveCityTheme
    {
        get
        {
            var activeId = _gameState.State.ActiveCityThemeId;
            if (string.IsNullOrEmpty(activeId) || activeId == "ct_default") return null;
            return CityTheme.GetAllThemes().FirstOrDefault(t => t.Id == activeId);
        }
    }

    public List<CityTheme> GetAvailableCityThemes() => CityTheme.GetAllThemes();

    public List<WorkshopSkin> GetAvailableWorkshopSkins() => WorkshopSkin.GetAllSkins();

    public void SetCityTheme(string? themeId)
    {
        if (themeId != null && !IsCosmeticUnlocked(themeId)) return;

        _gameState.State.ActiveCityThemeId = themeId ?? "ct_default";
        _gameState.MarkDirty();
        CosmeticChanged?.Invoke();
    }

    public void SetWorkshopSkin(string workshopType, string? skinId)
    {
        if (skinId != null && !IsCosmeticUnlocked(skinId)) return;

        var skins = _gameState.State.ActiveWorkshopSkins;
        if (skinId == null)
            skins.Remove(workshopType);
        else
            skins[workshopType] = skinId;

        _gameState.MarkDirty();
        CosmeticChanged?.Invoke();
    }

    public WorkshopSkin? GetActiveWorkshopSkin(string workshopType)
    {
        if (!_gameState.State.ActiveWorkshopSkins.TryGetValue(workshopType, out var skinId))
            return null;

        return WorkshopSkin.GetAllSkins().FirstOrDefault(s => s.Id == skinId);
    }

    public void UnlockCosmetic(string cosmeticId)
    {
        if (_gameState.State.UnlockedCosmetics.Contains(cosmeticId)) return;

        _gameState.State.UnlockedCosmetics.Add(cosmeticId);
        _gameState.MarkDirty();
        CosmeticChanged?.Invoke();
    }

    public bool IsCosmeticUnlocked(string cosmeticId) =>
        _gameState.State.UnlockedCosmetics.Contains(cosmeticId);
}
