using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HandwerkerRechner.ViewModels;

public sealed partial class MainViewModel
{
    #region Favoriten

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<FavoriteItem> _favoriteCalculators = [];

    public bool HasFavorites => FavoriteCalculators.Count > 0;

    [RelayCommand]
    private void ToggleFavorite(string key)
    {
        _favoritesService.Toggle(key);
        var msg = _favoritesService.IsFavorite(key)
            ? _localization.GetString("FavoriteAdded") ?? "Favorit hinzugefügt"
            : _localization.GetString("FavoriteRemoved") ?? "Favorit entfernt";
        FloatingTextRequested?.Invoke(msg, "info");
    }

    public bool IsFavorite(string key) => _favoritesService.IsFavorite(key);

    /// <summary>
    /// Öffnet einen Favoriten-Rechner über die Schnellzugriff-Leiste.
    /// Alle Rechner sind frei zugänglich.
    /// </summary>
    [RelayCommand]
    private void OpenFavorite(string route)
    {
        if (string.IsNullOrEmpty(route)) return;
        NavigateTo(route);
    }

    private void OnFavoritesChanged(object? sender, EventArgs e) => UpdateFavorites();

    private void UpdateFavorites()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FavoriteCalculators.Clear();
            foreach (var key in _favoritesService.Favorites)
            {
                var (label, icon, isPremium) = GetCalculatorInfo(key);
                FavoriteCalculators.Add(new FavoriteItem(key, label, icon, isPremium));
            }
            OnPropertyChanged(nameof(HasFavorites));
            // Favoriten-Status aller 19 Rechner-Cards aktualisieren (Stern-Toggle)
            NotifyFavoriteProperties();
        });
    }

    /// <summary>
    /// Benachrichtigt alle Favoriten-Properties damit die Sterne im UI korrekt aktualisiert werden.
    /// </summary>
    private void NotifyFavoriteProperties()
    {
        OnPropertyChanged(nameof(IsFavTileCalculator));
        OnPropertyChanged(nameof(IsFavWallpaper));
        OnPropertyChanged(nameof(IsFavPaint));
        OnPropertyChanged(nameof(IsFavFlooring));
        OnPropertyChanged(nameof(IsFavConcrete));
        OnPropertyChanged(nameof(IsFavDrywall));
        OnPropertyChanged(nameof(IsFavElectrical));
        OnPropertyChanged(nameof(IsFavMetal));
        OnPropertyChanged(nameof(IsFavGarden));
        OnPropertyChanged(nameof(IsFavRoofSolar));
        OnPropertyChanged(nameof(IsFavStairs));
        OnPropertyChanged(nameof(IsFavPlaster));
        OnPropertyChanged(nameof(IsFavScreed));
        OnPropertyChanged(nameof(IsFavInsulation));
        OnPropertyChanged(nameof(IsFavCableSizing));
        OnPropertyChanged(nameof(IsFavGrout));
        OnPropertyChanged(nameof(IsFavHourlyRate));
        OnPropertyChanged(nameof(IsFavMaterialCompare));
        OnPropertyChanged(nameof(IsFavAreaMeasure));
    }

    // Favoriten-Status je Rechner (Compiled-Binding-kompatibel, kein Converter nötig)
    public bool IsFavTileCalculator  => _favoritesService.IsFavorite("TileCalculatorPage");
    public bool IsFavWallpaper       => _favoritesService.IsFavorite("WallpaperCalculatorPage");
    public bool IsFavPaint           => _favoritesService.IsFavorite("PaintCalculatorPage");
    public bool IsFavFlooring        => _favoritesService.IsFavorite("FlooringCalculatorPage");
    public bool IsFavConcrete        => _favoritesService.IsFavorite("ConcretePage");
    public bool IsFavDrywall         => _favoritesService.IsFavorite("DrywallPage");
    public bool IsFavElectrical      => _favoritesService.IsFavorite("ElectricalPage");
    public bool IsFavMetal           => _favoritesService.IsFavorite("MetalPage");
    public bool IsFavGarden          => _favoritesService.IsFavorite("GardenPage");
    public bool IsFavRoofSolar       => _favoritesService.IsFavorite("RoofSolarPage");
    public bool IsFavStairs          => _favoritesService.IsFavorite("StairsPage");
    public bool IsFavPlaster         => _favoritesService.IsFavorite("PlasterPage");
    public bool IsFavScreed          => _favoritesService.IsFavorite("ScreedPage");
    public bool IsFavInsulation      => _favoritesService.IsFavorite("InsulationPage");
    public bool IsFavCableSizing     => _favoritesService.IsFavorite("CableSizingPage");
    public bool IsFavGrout           => _favoritesService.IsFavorite("GroutPage");
    public bool IsFavHourlyRate      => _favoritesService.IsFavorite("HourlyRatePage");
    public bool IsFavMaterialCompare => _favoritesService.IsFavorite("MaterialComparePage");
    public bool IsFavAreaMeasure     => _favoritesService.IsFavorite("AreaMeasurePage");

    private (string Label, string Icon, bool IsPremium) GetCalculatorInfo(string route) => route switch
    {
        "TileCalculatorPage" => (CalcTilesLabel, "ViewGrid", false),
        "WallpaperCalculatorPage" => (CalcWallpaperLabel, "Wallpaper", false),
        "PaintCalculatorPage" => (CalcPaintLabel, "FormatPaint", false),
        "FlooringCalculatorPage" => (CalcFlooringLabel, "Layers", false),
        "ConcretePage" => (CalcConcreteLabel, "CubeOutline", false),
        "DrywallPage" => (CategoryDrywallLabel, "Wall", false),
        "ElectricalPage" => (CategoryElectricalLabel, "Flash", false),
        "MetalPage" => (CategoryMetalLabel, "Wrench", false),
        "GardenPage" => (CategoryGardenLabel, "Flower", false),
        "RoofSolarPage" => (CategoryRoofSolarLabel, "SolarPanel", false),
        "StairsPage" => (CalcStairsLabel, "Stairs", false),
        "PlasterPage" => (CalcPlasterLabel, "FormatPaint", false),
        "ScreedPage" => (CalcScreedLabel, "Layers", false),
        "InsulationPage" => (CalcInsulationLabel, "Snowflake", false),
        "CableSizingPage" => (CalcCableSizingLabel, "CableData", false),
        "GroutPage" => (CalcGroutLabel, "Texture", false),
        "HourlyRatePage" => (CalcHourlyRateLabel, "ClockOutline", false),
        "MaterialComparePage" => (CalcMaterialCompareLabel, "ScaleBalance", false),
        "AreaMeasurePage" => (CalcAreaMeasureLabel, "RulerSquare", false),
        _ => (route, "Calculator", false)
    };

    #endregion
}
