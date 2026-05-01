using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using HandwerkerRechner.Resources.Strings;

namespace HandwerkerRechner.ViewModels;

/// <summary>
/// ViewModel for the main navigation hub page with tab navigation
/// </summary>
public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly ILocalizationService _localization;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IFavoritesService _favoritesService;

    // Zähler für Berechnungen: nach jeder 3. Berechnung wird ein Rewarded Video gezeigt
    private int _calculationCount;

    // Sub-ViewModels
    public ProjectTemplatesViewModel ProjectTemplatesViewModel { get; }
    public QuoteViewModel QuoteViewModel { get; }

    // Zentraler Factory-Service für alle 19 Calculator-VMs
    private readonly ICalculatorFactoryService _calculatorFactory;

    /// <summary>
    /// Event für Alerts/Nachrichten an den Benutzer (Titel, Nachricht)
    /// </summary>
    [ObservableProperty]
    private bool _isAdBannerVisible;

    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action<string>? ClipboardRequested;

    // Sub-ViewModels for embedded tabs
    public SettingsViewModel SettingsViewModel { get; }
    public ProjectsViewModel ProjectsViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }

    public MainViewModel(
        IPurchaseService purchaseService,
        IAdService adService,
        ILocalizationService localization,
        SettingsViewModel settingsViewModel,
        ProjectsViewModel projectsViewModel,
        HistoryViewModel historyViewModel,
        IRewardedAdService rewardedAdService,
        ICalculatorFactoryService calculatorFactory,
        IFavoritesService favoritesService,
        ProjectTemplatesViewModel projectTemplatesViewModel,
        QuoteViewModel quoteViewModel)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _localization = localization;
        _rewardedAdService = rewardedAdService;
        _calculatorFactory = calculatorFactory;
        _favoritesService = favoritesService;
        ProjectTemplatesViewModel = projectTemplatesViewModel;
        QuoteViewModel = quoteViewModel;
        SettingsViewModel = settingsViewModel;
        ProjectsViewModel = projectsViewModel;
        HistoryViewModel = historyViewModel;

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += OnAdsStateChanged;

        // Banner beim Start anzeigen (fuer Desktop + Fallback falls AdMobHelper fehlschlaegt)
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();

        // Wire Projects navigation und Messages
        ProjectsViewModel.NavigationRequested += OnProjectNavigation;
        ProjectsViewModel.MessageRequested += OnChildMessage;

        // Wire History navigation und Messages
        HistoryViewModel.NavigationRequested += OnHistoryNavigation;
        HistoryViewModel.MessageRequested += OnChildMessage;

        // Wire Settings Messages
        SettingsViewModel.MessageRequested += OnChildMessage;

        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;
        _rewardedAdService.AdUnavailable += OnAdUnavailable;

        // Subscribe to language changes
        SettingsViewModel.LanguageChanged += OnLanguageChanged;

        // Wire feedback to open email
        SettingsViewModel.FeedbackRequested += OnFeedbackRequested;

        // Wire Templates navigation
        ProjectTemplatesViewModel.NavigationRequested += OnTemplateNavigation;
        ProjectTemplatesViewModel.MessageRequested += OnChildMessage;
        ProjectTemplatesViewModel.FloatingTextRequested += OnChildFloatingText;

        // Wire Quote navigation
        QuoteViewModel.NavigationRequested += OnQuoteNavigation;
        QuoteViewModel.MessageRequested += OnChildMessage;
        QuoteViewModel.FloatingTextRequested += OnChildFloatingText;

        // Favoriten
        _favoritesService.FavoritesChanged += OnFavoritesChanged;
        UpdateFavorites();

        // Back-Press Helper verdrahten
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        UpdateStatus();
        UpdateNavTexts();
    }

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

    #region Tab Navigation

    [ObservableProperty]
    private int _selectedTab;

    public bool IsHomeTab => SelectedTab == 0;
    public bool IsProjectsTab => SelectedTab == 1;
    public bool IsHistoryTab => SelectedTab == 2;
    public bool IsSettingsTab => SelectedTab == 3;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsHomeTab));
        OnPropertyChanged(nameof(IsProjectsTab));
        OnPropertyChanged(nameof(IsHistoryTab));
        OnPropertyChanged(nameof(IsSettingsTab));
    }

    [RelayCommand]
    private void SelectHomeTab() { CurrentPage = null; SelectedTab = 0; }

    [RelayCommand]
    private void SelectProjectsTab()
    {
        CurrentPage = null;
        SelectedTab = 1;
        // Projektliste beim Tab-Wechsel aktualisieren
        ProjectsViewModel.LoadProjectsCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectHistoryTab()
    {
        CurrentPage = null;
        SelectedTab = 2;
        HistoryViewModel.LoadHistoryCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectSettingsTab() { CurrentPage = null; SelectedTab = 3; }

    #endregion

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
        SettingsViewModel.UpdateLocalizedTexts();
        HistoryViewModel.UpdateLocalizedTexts();
    }

    #endregion

    #region Calculator Page Navigation

    [ObservableProperty]
    private string? _currentPage;

    [ObservableProperty]
    private ObservableObject? _currentCalculatorVm;

    public bool IsCalculatorOpen => CurrentPage != null;

    partial void OnCurrentPageChanged(string? value)
    {
        // Altes Calculator-VM aufräumen (Event-Subscriptions entfernen)
        CleanupCurrentCalculator();

        OnPropertyChanged(nameof(IsCalculatorOpen));
        if (value == "ProjectTemplatesPage")
        {
            CurrentCalculatorVm = ProjectTemplatesViewModel;
            _ = LoadSafeAsync(ProjectTemplatesViewModel.LoadTemplatesAsync());
        }
        else if (value == "QuotePage")
        {
            CurrentCalculatorVm = QuoteViewModel;
            _ = LoadSafeAsync(QuoteViewModel.LoadQuotesAsync());
        }
        else if (value != null)
            CurrentCalculatorVm = CreateCalculatorVm(value);
        else
            CurrentCalculatorVm = null;
    }

    /// <summary>
    /// Sicherer Wrapper fuer fire-and-forget Ladeaufrufe.
    /// Verhindert unbehandelte Exceptions bei discarded Tasks.
    /// </summary>
    private async Task LoadSafeAsync(Task loadTask)
    {
        try
        {
            await loadTask;
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[HandwerkerRechner] Laden fehlgeschlagen: {ex.Message}");
#endif
        }
    }

    private void CleanupCurrentCalculator()
    {
        if (CurrentCalculatorVm is ICalculatorViewModel calc)
            calc.Cleanup();

        // Transient-VMs disposen (Timer, Subscriptions freigeben)
        if (CurrentCalculatorVm is IDisposable d)
            d.Dispose();
    }

    private ObservableObject? CreateCalculatorVm(string page)
    {
        // Parse route for projectId (e.g. "TileCalculatorPage?projectId=abc123")
        var route = page;
        string? projectId = null;
        var qIdx = page.IndexOf('?');
        if (qIdx >= 0)
        {
            route = page[..qIdx];
            var query = page[(qIdx + 1)..];
            if (query.StartsWith("projectId="))
                projectId = query["projectId=".Length..];
        }

        ObservableObject? vm = _calculatorFactory.Create(route);

        if (vm != null)
            WireCalculatorEvents(vm, projectId);

        return vm;
    }

    private void WireCalculatorEvents(ObservableObject vm, string? projectId)
    {
        if (vm is ICalculatorViewModel calc)
        {
            calc.NavigationRequested += OnCalculatorGoBack;
            calc.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
            calc.FloatingTextRequested += OnChildFloatingText;
            calc.ClipboardRequested += OnClipboardRequested;
            calc.CalculationPerformed += OnCalculationPerformed;
            if (projectId != null) _ = calc.LoadFromProjectIdAsync(projectId);
        }
    }

    private void OnChildFloatingText(string text, string category)
    {
        FloatingTextRequested?.Invoke(text, category);
    }

    private void OnClipboardRequested(string text)
    {
        ClipboardRequested?.Invoke(text);
    }

    private void OnCalculatorGoBack(string route)
    {
        if (route == "..")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentPage = null);
        }
    }

    private void OnProjectNavigation(string route)
    {
        if (route == "..") return;
        CurrentPage = route;
    }

    private void OnHistoryNavigation(string route)
    {
        if (route == "..") return;
        CurrentPage = route;
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

    #region Premium Status

    [ObservableProperty]
    private bool _isAdFree;

    [ObservableProperty]
    private bool _isPremium;

    public void UpdateStatus()
    {
        IsAdFree = _purchaseService.IsPremium;
        IsPremium = _purchaseService.IsPremium;
    }

    #endregion

    private void NavigateTo(string route) => CurrentPage = route;

    // FREE Calculator Navigation Commands
    [RelayCommand]
    private void NavigateToTiles() => NavigateTo("TileCalculatorPage");

    [RelayCommand]
    private void NavigateToWallpaper() => NavigateTo("WallpaperCalculatorPage");

    [RelayCommand]
    private void NavigateToPaint() => NavigateTo("PaintCalculatorPage");

    [RelayCommand]
    private void NavigateToFlooring() => NavigateTo("FlooringCalculatorPage");

    // Alle Rechner direkt zugänglich (kein Premium-Gate mehr)
    [RelayCommand]
    private void NavigateToDrywall() => NavigateTo("DrywallPage");

    [RelayCommand]
    private void NavigateToElectrical() => NavigateTo("ElectricalPage");

    [RelayCommand]
    private void NavigateToMetal() => NavigateTo("MetalPage");

    [RelayCommand]
    private void NavigateToGarden() => NavigateTo("GardenPage");

    [RelayCommand]
    private void NavigateToRoofSolar() => NavigateTo("RoofSolarPage");

    [RelayCommand]
    private void NavigateToConcrete() => NavigateTo("ConcretePage");

    [RelayCommand]
    private void NavigateToStairs() => NavigateTo("StairsPage");

    [RelayCommand]
    private void NavigateToPlaster() => NavigateTo("PlasterPage");

    [RelayCommand]
    private void NavigateToScreed() => NavigateTo("ScreedPage");

    [RelayCommand]
    private void NavigateToInsulation() => NavigateTo("InsulationPage");

    [RelayCommand]
    private void NavigateToCableSizing() => NavigateTo("CableSizingPage");

    [RelayCommand]
    private void NavigateToGrout() => NavigateTo("GroutPage");

    [RelayCommand]
    private void NavigateToHourlyRate() => NavigateTo("HourlyRatePage");

    [RelayCommand]
    private void NavigateToMaterialCompare() => NavigateTo("MaterialComparePage");

    [RelayCommand]
    private void NavigateToAreaMeasure() => NavigateTo("AreaMeasurePage");

    // Vorlagen + Angebote Navigation
    [RelayCommand]
    private void NavigateToTemplates() => CurrentPage = "ProjectTemplatesPage";

    [RelayCommand]
    private void NavigateToQuotes() => CurrentPage = "QuotePage";

    #region Ad-Counter (alle 3 Berechnungen ein Rewarded Video)

    /// <summary>
    /// Wird nach jeder erfolgreichen Berechnung aufgerufen.
    /// Zeigt alle 3 Berechnungen ein Rewarded Video (außer Premium-User).
    /// </summary>
    private async void OnCalculationPerformed()
    {
        if (_purchaseService.IsPremium) return;
        _calculationCount++;
        if (_calculationCount >= 3)
        {
            _calculationCount = 0;
            await _rewardedAdService.ShowAdAsync("calculation_ad");
        }
    }

    #endregion

    [RelayCommand]
    private async Task PurchaseRemoveAds()
    {
        if (_purchaseService.IsPremium)
        {
            MessageRequested?.Invoke(_localization.GetString("AlreadyAdFree"), _localization.GetString("AlreadyAdFreeMessage"));
            return;
        }

        var success = await _purchaseService.PurchaseRemoveAdsAsync();
        if (success)
        {
            MessageRequested?.Invoke(_localization.GetString("PurchaseSuccessful"), _localization.GetString("RemoveAdsPurchaseSuccessMessage"));
            UpdateStatus();
        }
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateStatus);
    }

    private void OnAdsStateChanged(object? sender, EventArgs e)
        => IsAdBannerVisible = _adService.BannerVisible;

    private void OnChildMessage(string title, string msg)
        => MessageRequested?.Invoke(title, msg);

    private void OnAdUnavailable()
        => MessageRequested?.Invoke(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);

    private void OnTemplateNavigation(string route)
    {
        if (route == "..") { CurrentPage = null; return; }
        CurrentPage = route;
    }

    private void OnQuoteNavigation(string route)
    {
        if (route == "..") { CurrentPage = null; return; }
    }

    private void OnFeedbackRequested(string appName)
    {
        var uri = $"mailto:info@rs-digital.org?subject={Uri.EscapeDataString(appName + " Feedback")}";
        MeineApps.Core.Ava.Services.UriLauncher.OpenUri(uri);
    }

    #region Back-Navigation

    /// <summary>
    /// Event für Toast-Hinweis "Nochmal drücken zum Beenden"
    /// </summary>
    public event Action<string>? ExitHintRequested;

    private readonly BackPressHelper _backPressHelper = new();

    /// <summary>
    /// Behandelt Android-Zurücktaste. Gibt true zurück wenn die App NICHT beendet werden soll.
    /// Reihenfolge: Overlays → SaveDialog → Calculator → Tab → Home (Double-Tap-Exit)
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. SaveDialog im aktuellen Calculator schließen
        if (CurrentCalculatorVm is ICalculatorViewModel calc)
        {
            if (calc.ShowSaveDialog)
            {
                calc.ShowSaveDialog = false;
                return true;
            }

            // 3. Calculator schließen → zurück zur Tab-Ansicht
            CurrentPage = null;
            return true;
        }

        // Nicht-Calculator-Seiten (Templates, Quotes) → zurück zur Tab-Ansicht
        if (CurrentCalculatorVm != null)
        {
            CurrentPage = null;
            return true;
        }

        // 4. Vom Projekt-/Settings-Tab zurück zum Home-Tab
        if (SelectedTab != 0)
        {
            SelectHomeTab();
            return true;
        }

        // 5. Auf Home-Tab: Double-Tap-Exit
        var msg = _localization.GetString("PressBackToExit") ?? "Press back again to exit";
        return _backPressHelper.HandleDoubleBack(msg);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        // Aktiven Rechner aufräumen (Timer stoppen, Events abmelden)
        CleanupCurrentCalculator();

        _adService.AdsStateChanged -= OnAdsStateChanged;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        _rewardedAdService.AdUnavailable -= OnAdUnavailable;
        SettingsViewModel.LanguageChanged -= OnLanguageChanged;
        SettingsViewModel.FeedbackRequested -= OnFeedbackRequested;
        SettingsViewModel.MessageRequested -= OnChildMessage;
        ProjectsViewModel.NavigationRequested -= OnProjectNavigation;
        ProjectsViewModel.MessageRequested -= OnChildMessage;
        HistoryViewModel.NavigationRequested -= OnHistoryNavigation;
        HistoryViewModel.MessageRequested -= OnChildMessage;

        ProjectTemplatesViewModel.NavigationRequested -= OnTemplateNavigation;
        ProjectTemplatesViewModel.MessageRequested -= OnChildMessage;
        ProjectTemplatesViewModel.FloatingTextRequested -= OnChildFloatingText;
        QuoteViewModel.NavigationRequested -= OnQuoteNavigation;
        QuoteViewModel.MessageRequested -= OnChildMessage;
        QuoteViewModel.FloatingTextRequested -= OnChildFloatingText;
        _favoritesService.FavoritesChanged -= OnFavoritesChanged;

        _disposed = true;
    }
}

/// <summary>Favorit-Eintrag für die Schnellzugriff-Leiste im HomeTab</summary>
public record FavoriteItem(string Route, string Label, string IconKind, bool IsPremium);
