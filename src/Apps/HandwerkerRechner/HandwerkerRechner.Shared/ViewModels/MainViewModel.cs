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
    private readonly IPremiumAccessService _premiumAccessService;
    private readonly IFavoritesService _favoritesService;

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
    public event Action? CelebrationRequested;
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
        IPremiumAccessService premiumAccessService,
        ICalculatorFactoryService calculatorFactory,
        IFavoritesService favoritesService,
        ProjectTemplatesViewModel projectTemplatesViewModel,
        QuoteViewModel quoteViewModel)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _localization = localization;
        _rewardedAdService = rewardedAdService;
        _premiumAccessService = premiumAccessService;
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
        _premiumAccessService.AccessExpired += OnAccessExpired;
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
    /// Premium-Check wird berücksichtigt.
    /// </summary>
    [RelayCommand]
    private void OpenFavorite(string route)
    {
        if (string.IsNullOrEmpty(route)) return;

        if (IsPremiumRoute(route))
            NavigatePremium(route);
        else
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
        });
    }

    private (string Label, string Icon, bool IsPremium) GetCalculatorInfo(string route) => route switch
    {
        "TileCalculatorPage" => (CalcTilesLabel, "ViewGrid", false),
        "WallpaperCalculatorPage" => (CalcWallpaperLabel, "Wallpaper", false),
        "PaintCalculatorPage" => (CalcPaintLabel, "FormatPaint", false),
        "FlooringCalculatorPage" => (CalcFlooringLabel, "Layers", false),
        "ConcretePage" => (CalcConcreteLabel, "CubeOutline", false),
        "DrywallPage" => (CategoryDrywallLabel, "Wall", true),
        "ElectricalPage" => (CategoryElectricalLabel, "Flash", true),
        "MetalPage" => (CategoryMetalLabel, "Wrench", true),
        "GardenPage" => (CategoryGardenLabel, "Flower", true),
        "RoofSolarPage" => (CategoryRoofSolarLabel, "SolarPanel", true),
        "StairsPage" => (CalcStairsLabel, "Stairs", true),
        "PlasterPage" => (CalcPlasterLabel, "FormatPaint", true),
        "ScreedPage" => (CalcScreedLabel, "Layers", true),
        "InsulationPage" => (CalcInsulationLabel, "Snowflake", true),
        "CableSizingPage" => (CalcCableSizingLabel, "CableData", true),
        "GroutPage" => (CalcGroutLabel, "Texture", true),
        "HourlyRatePage" => (CalcHourlyRateLabel, "ClockOutline", true),
        "MaterialComparePage" => (CalcMaterialCompareLabel, "ScaleBalance", true),
        "AreaMeasurePage" => (CalcAreaMeasureLabel, "RulerSquare", true),
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

    private void UpdateHomeTexts()
    {
        // Alle Home-Properties auf einmal invalidieren (statt 46 einzelne Aufrufe)
        OnPropertyChanged(string.Empty);
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
            _ = ProjectTemplatesViewModel.LoadTemplatesAsync();
        }
        else if (value == "QuotePage")
        {
            CurrentCalculatorVm = QuoteViewModel;
            _ = QuoteViewModel.LoadQuotesAsync();
        }
        else if (value != null)
            CurrentCalculatorVm = CreateCalculatorVm(value);
        else
            CurrentCalculatorVm = null;
    }

    private void CleanupCurrentCalculator()
    {
        if (CurrentCalculatorVm is ICalculatorViewModel calc)
            calc.Cleanup();
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
            if (projectId != null) _ = calc.LoadFromProjectIdAsync(projectId);
        }
    }

    private void OnChildFloatingText(string text, string category)
    {
        FloatingTextRequested?.Invoke(text, category);
        if (category == "success")
            CelebrationRequested?.Invoke();
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

    /// <summary>
    /// Prüft ob eine Route zu einem Premium-Rechner führt
    /// </summary>
    private static bool IsPremiumRoute(string route)
    {
        // Route kann Query-Parameter enthalten (z.B. "DrywallPage?projectId=abc")
        var routeName = route;
        var qIdx = route.IndexOf('?');
        if (qIdx >= 0)
            routeName = route[..qIdx];

        return routeName is "DrywallPage" or "ElectricalPage" or "MetalPage" or "GardenPage" or "RoofSolarPage" or "StairsPage" or "PlasterPage" or "ScreedPage" or "InsulationPage" or "CableSizingPage" or "GroutPage" or "HourlyRatePage" or "MaterialComparePage" or "AreaMeasurePage";
    }

    private void OnProjectNavigation(string route)
    {
        if (route == "..") return;

        // Premium-Check: Projekt-Laden darf Premium-Sperre nicht umgehen
        if (IsPremiumRoute(route) && !_premiumAccessService.HasAccess)
        {
            PendingPremiumRoute = route;
            ShowPremiumAccessOverlay = true;
            return;
        }

        CurrentPage = route;
    }

    private void OnHistoryNavigation(string route)
    {
        if (route == "..") return;

        // Premium-Check: History-Navigation darf Premium-Sperre nicht umgehen
        if (IsPremiumRoute(route) && !_premiumAccessService.HasAccess)
        {
            PendingPremiumRoute = route;
            ShowPremiumAccessOverlay = true;
            return;
        }

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

    // Premium Calculator Navigation (gated mit PremiumAccess oder Ad)
    [RelayCommand]
    private void NavigateToDrywall() => NavigatePremium("DrywallPage");

    [RelayCommand]
    private void NavigateToElectrical() => NavigatePremium("ElectricalPage");

    [RelayCommand]
    private void NavigateToMetal() => NavigatePremium("MetalPage");

    [RelayCommand]
    private void NavigateToGarden() => NavigatePremium("GardenPage");

    [RelayCommand]
    private void NavigateToRoofSolar() => NavigatePremium("RoofSolarPage");

    [RelayCommand]
    private void NavigateToConcrete() => NavigateTo("ConcretePage");

    [RelayCommand]
    private void NavigateToStairs() => NavigatePremium("StairsPage");

    [RelayCommand]
    private void NavigateToPlaster() => NavigatePremium("PlasterPage");

    [RelayCommand]
    private void NavigateToScreed() => NavigatePremium("ScreedPage");

    [RelayCommand]
    private void NavigateToInsulation() => NavigatePremium("InsulationPage");

    [RelayCommand]
    private void NavigateToCableSizing() => NavigatePremium("CableSizingPage");

    [RelayCommand]
    private void NavigateToGrout() => NavigatePremium("GroutPage");

    // Profi-Werkzeuge Navigation
    [RelayCommand]
    private void NavigateToHourlyRate() => NavigatePremium("HourlyRatePage");

    [RelayCommand]
    private void NavigateToMaterialCompare() => NavigatePremium("MaterialComparePage");

    [RelayCommand]
    private void NavigateToAreaMeasure() => NavigatePremium("AreaMeasurePage");

    // Vorlagen + Angebote Navigation
    [RelayCommand]
    private void NavigateToTemplates() => CurrentPage = "ProjectTemplatesPage";

    [RelayCommand]
    private void NavigateToQuotes() => CurrentPage = "QuotePage";

    /// <summary>
    /// Prueft Premium-Zugang vor Navigation zu Premium-Rechnern.
    /// Premium oder temporaerer Zugang → direkt. Sonst → Ad-Overlay.
    /// </summary>
    private void NavigatePremium(string route)
    {
        if (_premiumAccessService.HasAccess)
        {
            NavigateTo(route);
            return;
        }
        PendingPremiumRoute = route;
        ShowPremiumAccessOverlay = true;
    }

    #region Premium Access Overlay

    [ObservableProperty]
    private bool _showPremiumAccessOverlay;

    [ObservableProperty]
    private string _pendingPremiumRoute = "";

    [ObservableProperty]
    private bool _hasTemporaryAccess;

    [ObservableProperty]
    private string _accessTimerText = "";

    // Lokalisierte Texte fuer das Overlay
    public string PremiumLockedText => _localization.GetString("PremiumCalculatorsLocked") ?? "Unlock Premium Calculators";
    public string VideoFor30MinText => _localization.GetString("VideoFor30Min") ?? "Watch Video → 30 Min Access";
    public string PremiumLockedDescText => _localization.GetString("WatchVideoFor30Min") ?? "Watch a video for 30 min access to all premium calculators.";

    [RelayCommand]
    private async Task ConfirmPremiumAdAsync()
    {
        ShowPremiumAccessOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("premium_access");
        if (success)
        {
            _premiumAccessService.GrantTemporaryAccess(TimeSpan.FromMinutes(30));
            HasTemporaryAccess = true;

            var msg = _localization.GetString("AccessGranted") ?? "Access granted!";
            MessageRequested?.Invoke(msg, "");

            // Gemerkten Rechner oeffnen
            if (!string.IsNullOrEmpty(PendingPremiumRoute))
                NavigateTo(PendingPremiumRoute);
        }
        PendingPremiumRoute = "";
    }

    [RelayCommand]
    private void CancelPremiumAd()
    {
        ShowPremiumAccessOverlay = false;
        PendingPremiumRoute = "";
    }

    private void OnAccessExpired(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            HasTemporaryAccess = false;
            AccessTimerText = "";
        });
    }

    #endregion

    #region Extended History

    [ObservableProperty]
    private bool _showExtendedHistoryOverlay;

    public string ExtendedHistoryTitleText => _localization.GetString("ExtendedHistoryTitle") ?? "Extended History";
    public string ExtendedHistoryDescText => _localization.GetString("ExtendedHistoryDesc") ?? "Watch a video to unlock 30 saved calculations for 24 hours (instead of 5).";

    /// <summary>
    /// Zeigt Overlay zum Freischalten der erweiterten History
    /// </summary>
    [RelayCommand]
    private void ShowExtendedHistoryAd()
    {
        if (_premiumAccessService.HasExtendedHistory)
        {
            MessageRequested?.Invoke(
                _localization.GetString("ExtendedHistoryTitle") ?? "Extended History",
                _localization.GetString("AccessGranted") ?? "Already active!");
            return;
        }
        ShowExtendedHistoryOverlay = true;
    }

    [RelayCommand]
    private async Task ConfirmExtendedHistoryAdAsync()
    {
        ShowExtendedHistoryOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("extended_history");
        if (success)
        {
            _premiumAccessService.GrantExtendedHistory();
            MessageRequested?.Invoke(
                _localization.GetString("AccessGranted") ?? "Access granted!",
                _localization.GetString("ExtendedHistoryDesc") ?? "30 entries for 24h!");
        }
    }

    [RelayCommand]
    private void CancelExtendedHistoryAd()
    {
        ShowExtendedHistoryOverlay = false;
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
        // Template-Navigation: Kann Premium-Routen enthalten
        if (IsPremiumRoute(route) && !_premiumAccessService.HasAccess)
        {
            PendingPremiumRoute = route;
            ShowPremiumAccessOverlay = true;
            return;
        }
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
        // 1. Overlays schließen
        if (ShowPremiumAccessOverlay) { CancelPremiumAd(); return true; }
        if (ShowExtendedHistoryOverlay) { CancelExtendedHistoryAd(); return true; }

        // 2. SaveDialog im aktuellen Calculator schließen
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
        _premiumAccessService.AccessExpired -= OnAccessExpired;
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
