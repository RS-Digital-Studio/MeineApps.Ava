using HandwerkerImperium.Models.Cosmetics;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet kosmetische Anpassungen (City-Themes, Workshop-Skins).
/// Rein visuell, keine Gameplay-Auswirkungen.
/// </summary>
public interface ICosmeticService
{
    /// <summary>Aktives City-Theme (null = Standard).</summary>
    CityTheme? ActiveCityTheme { get; }

    /// <summary>Alle verfügbaren City-Themes.</summary>
    List<CityTheme> GetAvailableCityThemes();

    /// <summary>Alle verfügbaren Workshop-Skins.</summary>
    List<WorkshopSkin> GetAvailableWorkshopSkins();

    /// <summary>City-Theme aktivieren (Id oder null für Standard).</summary>
    void SetCityTheme(string? themeId);

    /// <summary>Workshop-Skin aktivieren.</summary>
    void SetWorkshopSkin(string workshopType, string? skinId);

    /// <summary>Aktiven Workshop-Skin für einen Typ holen.</summary>
    WorkshopSkin? GetActiveWorkshopSkin(string workshopType);

    /// <summary>Kosmetik freischalten (durch Prestige, BattlePass, etc.).</summary>
    void UnlockCosmetic(string cosmeticId);

    /// <summary>Ob eine Kosmetik freigeschaltet ist.</summary>
    bool IsCosmeticUnlocked(string cosmeticId);

    /// <summary>Feuert wenn sich die aktive Kosmetik ändert.</summary>
    event Action? CosmeticChanged;
}
