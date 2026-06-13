using CommunityToolkit.Mvvm.ComponentModel;

namespace HandwerkerRechner.ViewModels;

public sealed partial class MainViewModel
{
    #region Localized Nav Texts

    [ObservableProperty]
    private string _tabHomeText = "Home";

    [ObservableProperty]
    private string _tabProjectsText = "Projects";

    [ObservableProperty]
    private string _tabHistoryText = "History";

    [ObservableProperty]
    private string _tabSettingsText = "Settings";

    private void UpdateNavTexts()
    {
        TabHomeText = _localization.GetString("TabHome") ?? "Home";
        TabProjectsText = _localization.GetString("TabProjects") ?? "Projects";
        TabHistoryText = _localization.GetString("TabHistory") ?? "History";
        TabSettingsText = _localization.GetString("TabSettings") ?? "Settings";
    }

    // Alle lokalisierten Properties auf MainViewModel - bei Sprachwechsel gezielt invalidieren
    // (statt OnPropertyChanged(string.Empty) das ALLE Bindings im Visual-Tree neu evaluiert →
    //  50-150ms Stutter auf Mid-Tier-Android)
    private static readonly string[] LocalizedPropertyNames =
    {
        nameof(AppTitle), nameof(AppDescription),
        nameof(CategoryFloorWallLabel), nameof(CalcTilesLabel), nameof(CalcWallpaperLabel),
        nameof(CalcPaintLabel), nameof(CalcFlooringLabel), nameof(MoreCategoriesLabel),
        nameof(CategoryDrywallLabel), nameof(CategoryElectricalLabel), nameof(CategoryMetalLabel),
        nameof(CategoryGardenLabel), nameof(CategoryRoofSolarLabel),
        nameof(CalcTilesDescLabel), nameof(CalcWallpaperDescLabel), nameof(CalcPaintDescLabel),
        nameof(CalcFlooringDescLabel), nameof(CategoryDrywallDescLabel),
        nameof(CategoryElectricalDescLabel), nameof(CategoryMetalDescLabel),
        nameof(CategoryGardenDescLabel), nameof(CategoryRoofSolarDescLabel),
        nameof(CalcConcreteLabel), nameof(CalcConcreteDescLabel),
        nameof(CalcStairsLabel), nameof(CalcStairsDescLabel),
        nameof(CalcPlasterLabel), nameof(CalcPlasterDescLabel),
        nameof(CalcScreedLabel), nameof(CalcScreedDescLabel),
        nameof(CalcInsulationLabel), nameof(CalcInsulationDescLabel),
        nameof(CalcCableSizingLabel), nameof(CalcCableSizingDescLabel),
        nameof(CalcGroutLabel), nameof(CalcGroutDescLabel),
        nameof(CalcHourlyRateLabel), nameof(CalcHourlyRateDescLabel),
        nameof(CalcMaterialCompareLabel), nameof(CalcMaterialCompareDescLabel),
        nameof(CalcAreaMeasureLabel), nameof(CalcAreaMeasureDescLabel),
        nameof(FavoritesTitleText),
        nameof(TemplatesLabel), nameof(QuotesLabel), nameof(SectionBusinessText),
        nameof(SectionFloorWallText), nameof(SectionPremiumToolsText),
        nameof(CalculatorCountText), nameof(GetPremiumText), nameof(PremiumPriceText)
    };

    private void UpdateHomeTexts()
    {
        // Gezielt nur die ~51 Home-Properties invalidieren (nicht alle Bindings im Tree)
        foreach (var name in LocalizedPropertyNames)
            OnPropertyChanged(name);
    }

    private void OnLanguageChanged()
    {
        UpdateNavTexts();
        UpdateHomeTexts();
        UpdateFavorites();   // Favoriten-Labels in der neuen Sprache neu aufbauen
        SettingsViewModel.UpdateLocalizedTexts();
        HistoryViewModel.UpdateLocalizedTexts();
        QuoteViewModel.UpdateLocalizedTexts();
    }

    #endregion

    #region Localized Labels

    public string AppTitle => _localization.GetString("AppTitle") ?? "HandwerkerRechner";
    public string AppDescription => _localization.GetString("AppDescription");
    public string CategoryFloorWallLabel => _localization.GetString("CategoryFloorWall");
    public string CalcTilesLabel => _localization.GetString("CalcTiles");
    public string CalcWallpaperLabel => _localization.GetString("CalcWallpaper");
    public string CalcPaintLabel => _localization.GetString("CalcPaint");
    public string CalcFlooringLabel => _localization.GetString("CalcFlooring");
    public string MoreCategoriesLabel => _localization.GetString("MoreCategories");
    public string CategoryDrywallLabel => _localization.GetString("CategoryDrywall");
    public string CategoryElectricalLabel => _localization.GetString("CategoryElectrical");
    public string CategoryMetalLabel => _localization.GetString("CategoryMetal");
    public string CategoryGardenLabel => _localization.GetString("CategoryGarden");
    public string CategoryRoofSolarLabel => _localization.GetString("CategoryRoofSolar");

    // Kategorie-Beschreibungen
    public string CalcTilesDescLabel => _localization.GetString("CalcTilesDesc") ?? "";
    public string CalcWallpaperDescLabel => _localization.GetString("CalcWallpaperDesc") ?? "";
    public string CalcPaintDescLabel => _localization.GetString("CalcPaintDesc") ?? "";
    public string CalcFlooringDescLabel => _localization.GetString("CalcFlooringDesc") ?? "";
    public string CategoryDrywallDescLabel => _localization.GetString("CategoryDrywallDesc") ?? "";
    public string CategoryElectricalDescLabel => _localization.GetString("CategoryElectricalDesc") ?? "";
    public string CategoryMetalDescLabel => _localization.GetString("CategoryMetalDesc") ?? "";
    public string CategoryGardenDescLabel => _localization.GetString("CategoryGardenDesc") ?? "";
    public string CategoryRoofSolarDescLabel => _localization.GetString("CategoryRoofSolarDesc") ?? "";
    public string CalcConcreteLabel => _localization.GetString("CalcConcrete") ?? "Concrete";
    public string CalcConcreteDescLabel => _localization.GetString("CalcConcreteDesc") ?? "";
    public string CalcStairsLabel => _localization.GetString("CalcStairs") ?? "Stairs";
    public string CalcStairsDescLabel => _localization.GetString("CalcStairsDesc") ?? "";
    public string CalcPlasterLabel => _localization.GetString("CalcPlaster") ?? "Plaster";
    public string CalcPlasterDescLabel => _localization.GetString("CalcPlasterDesc") ?? "";
    public string CalcScreedLabel => _localization.GetString("CalcScreed") ?? "Screed";
    public string CalcScreedDescLabel => _localization.GetString("CalcScreedDesc") ?? "";
    public string CalcInsulationLabel => _localization.GetString("CalcInsulation") ?? "Insulation";
    public string CalcInsulationDescLabel => _localization.GetString("CalcInsulationDesc") ?? "";
    public string CalcCableSizingLabel => _localization.GetString("CalcCableSizing") ?? "Cable Sizing";
    public string CalcCableSizingDescLabel => _localization.GetString("CalcCableSizingDesc") ?? "";
    public string CalcGroutLabel => _localization.GetString("CalcGrout") ?? "Grout";
    public string CalcGroutDescLabel => _localization.GetString("CalcGroutDesc") ?? "";

    // Profi-Werkzeuge Labels
    public string CalcHourlyRateLabel => _localization.GetString("CalcHourlyRate") ?? "Stundenrechner";
    public string CalcHourlyRateDescLabel => _localization.GetString("CalcHourlyRateDesc") ?? "";
    public string CalcMaterialCompareLabel => _localization.GetString("CalcMaterialCompare") ?? "Material-Vergleich";
    public string CalcMaterialCompareDescLabel => _localization.GetString("CalcMaterialCompareDesc") ?? "";
    public string CalcAreaMeasureLabel => _localization.GetString("CalcAreaMeasure") ?? "Aufmaß-Rechner";
    public string CalcAreaMeasureDescLabel => _localization.GetString("CalcAreaMeasureDesc") ?? "";

    // Favoriten Labels
    public string FavoritesTitleText => _localization.GetString("FavoritesTitle") ?? "Schnellzugriff";

    // Business Labels
    public string TemplatesLabel => _localization.GetString("ProjectTemplates") ?? "Vorlagen";
    public string QuotesLabel => _localization.GetString("Quotes") ?? "Angebote";
    public string SectionBusinessText => _localization.GetString("SectionBusiness") ?? "Business";

    // Design-Redesign Properties
    public string SectionFloorWallText => _localization.GetString("SectionFloorWall") ?? "Floor & Wall";
    public string SectionPremiumToolsText => _localization.GetString("SectionPremiumTools") ?? "Pro Tools";
    public string CalculatorCountText => _localization.GetString("CalculatorCount") ?? "9 Pro Calculators";
    public string GetPremiumText => _localization.GetString("GetPremium") ?? "Go Ad-Free";
    public string PremiumPriceText => _localization.GetString("PremiumPrice") ?? "From 3.99 €";

    #endregion
}
