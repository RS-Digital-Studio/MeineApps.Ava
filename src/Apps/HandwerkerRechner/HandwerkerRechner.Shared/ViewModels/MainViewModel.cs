using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Services;
using HandwerkerRechner.ViewModels.Floor;
using HandwerkerRechner.ViewModels.Premium;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using HandwerkerRechner.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;

namespace HandwerkerRechner.ViewModels;

/// <summary>
/// ViewModel for the main navigation hub page with tab navigation
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly ILocalizationService _localization;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPremiumAccessService _premiumAccessService;

    /// <summary>
    /// Event for showing alerts/messages to the user (title, message)
    /// </summary>
    [ObservableProperty]
    private bool _isAdBannerVisible;

    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // Sub-ViewModels for embedded tabs
    public SettingsViewModel SettingsViewModel { get; }
    public ProjectsViewModel ProjectsViewModel { get; }

    public MainViewModel(
        IPurchaseService purchaseService,
        IAdService adService,
        ILocalizationService localization,
        IThemeService themeService,
        SettingsViewModel settingsViewModel,
        ProjectsViewModel projectsViewModel,
        IRewardedAdService rewardedAdService,
        IPremiumAccessService premiumAccessService)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _localization = localization;
        _rewardedAdService = rewardedAdService;
        _premiumAccessService = premiumAccessService;
        SettingsViewModel = settingsViewModel;
        ProjectsViewModel = projectsViewModel;

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += (_, _) => IsAdBannerVisible = _adService.BannerVisible;

        // Banner beim Start anzeigen (fuer Desktop + Fallback falls AdMobHelper fehlschlaegt)
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();

        // Wire Projects navigation und Messages
        ProjectsViewModel.NavigationRequested += OnProjectNavigation;
        ProjectsViewModel.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);

        // Wire Settings Messages
        SettingsViewModel.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);

        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;
        _premiumAccessService.AccessExpired += OnAccessExpired;
        _rewardedAdService.AdUnavailable += () =>
            MessageRequested?.Invoke(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);

        // Subscribe to language changes
        SettingsViewModel.LanguageChanged += OnLanguageChanged;

        // Wire feedback to open email
        SettingsViewModel.FeedbackRequested += OnFeedbackRequested;

        UpdateStatus();
        UpdateNavTexts();
    }

    #region Tab Navigation

    [ObservableProperty]
    private int _selectedTab;

    public bool IsHomeTab => SelectedTab == 0;
    public bool IsProjectsTab => SelectedTab == 1;
    public bool IsSettingsTab => SelectedTab == 2;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsHomeTab));
        OnPropertyChanged(nameof(IsProjectsTab));
        OnPropertyChanged(nameof(IsSettingsTab));
    }

    [RelayCommand]
    private void SelectHomeTab() { CurrentPage = null; SelectedTab = 0; }

    [RelayCommand]
    private void SelectProjectsTab() { CurrentPage = null; SelectedTab = 1; }

    [RelayCommand]
    private void SelectSettingsTab() { CurrentPage = null; SelectedTab = 2; }

    #endregion

    #region Localized Nav Texts

    [ObservableProperty]
    private string _tabHomeText = "Home";

    [ObservableProperty]
    private string _tabProjectsText = "Projects";

    [ObservableProperty]
    private string _tabSettingsText = "Settings";

    private void UpdateNavTexts()
    {
        TabHomeText = _localization.GetString("TabHome") ?? "Home";
        TabProjectsText = _localization.GetString("TabProjects") ?? "Projects";
        TabSettingsText = _localization.GetString("TabSettings") ?? "Settings";
    }

    private void UpdateHomeTexts()
    {
        OnPropertyChanged(nameof(AppTitle));
        OnPropertyChanged(nameof(AppDescription));
        OnPropertyChanged(nameof(CategoryFloorWallLabel));
        OnPropertyChanged(nameof(CalcTilesLabel));
        OnPropertyChanged(nameof(CalcWallpaperLabel));
        OnPropertyChanged(nameof(CalcPaintLabel));
        OnPropertyChanged(nameof(CalcFlooringLabel));
        OnPropertyChanged(nameof(MoreCategoriesLabel));
        OnPropertyChanged(nameof(CategoryDrywallLabel));
        OnPropertyChanged(nameof(CategoryElectricalLabel));
        OnPropertyChanged(nameof(CategoryMetalLabel));
        OnPropertyChanged(nameof(CategoryGardenLabel));
        OnPropertyChanged(nameof(CategoryRoofSolarLabel));
        OnPropertyChanged(nameof(CalcTilesDescLabel));
        OnPropertyChanged(nameof(CalcWallpaperDescLabel));
        OnPropertyChanged(nameof(CalcPaintDescLabel));
        OnPropertyChanged(nameof(CalcFlooringDescLabel));
        OnPropertyChanged(nameof(CategoryDrywallDescLabel));
        OnPropertyChanged(nameof(CategoryElectricalDescLabel));
        OnPropertyChanged(nameof(CategoryMetalDescLabel));
        OnPropertyChanged(nameof(CategoryGardenDescLabel));
        OnPropertyChanged(nameof(CategoryRoofSolarDescLabel));
        OnPropertyChanged(nameof(SectionFloorWallText));
        OnPropertyChanged(nameof(SectionPremiumToolsText));
        OnPropertyChanged(nameof(CalculatorCountText));
        OnPropertyChanged(nameof(GetPremiumText));
        OnPropertyChanged(nameof(PremiumPriceText));
        OnPropertyChanged(nameof(MoreCategoriesLabel));
        OnPropertyChanged(nameof(PremiumLockedText));
        OnPropertyChanged(nameof(VideoFor30MinText));
        OnPropertyChanged(nameof(PremiumLockedDescText));
        OnPropertyChanged(nameof(ExtendedHistoryTitleText));
        OnPropertyChanged(nameof(ExtendedHistoryDescText));
    }

    private void OnLanguageChanged()
    {
        UpdateNavTexts();
        UpdateHomeTexts();
        SettingsViewModel.UpdateLocalizedTexts();
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
        if (value != null)
            CurrentCalculatorVm = CreateCalculatorVm(value);
        else
            CurrentCalculatorVm = null;
    }

    private void CleanupCurrentCalculator()
    {
        switch (CurrentCalculatorVm)
        {
            case TileCalculatorViewModel t: t.Cleanup(); break;
            case WallpaperCalculatorViewModel w: w.Cleanup(); break;
            case PaintCalculatorViewModel p: p.Cleanup(); break;
            case FlooringCalculatorViewModel f: f.Cleanup(); break;
        }
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

        ObservableObject? vm = route switch
        {
            "TileCalculatorPage" => App.Services.GetRequiredService<TileCalculatorViewModel>(),
            "WallpaperCalculatorPage" => App.Services.GetRequiredService<WallpaperCalculatorViewModel>(),
            "PaintCalculatorPage" => App.Services.GetRequiredService<PaintCalculatorViewModel>(),
            "FlooringCalculatorPage" => App.Services.GetRequiredService<FlooringCalculatorViewModel>(),
            "DrywallPage" => App.Services.GetRequiredService<DrywallViewModel>(),
            "ElectricalPage" => App.Services.GetRequiredService<ElectricalViewModel>(),
            "MetalPage" => App.Services.GetRequiredService<MetalViewModel>(),
            "GardenPage" => App.Services.GetRequiredService<GardenViewModel>(),
            "RoofSolarPage" => App.Services.GetRequiredService<RoofSolarViewModel>(),
            _ => null
        };

        if (vm != null)
            WireCalculatorEvents(vm, projectId);

        return vm;
    }

    private void WireCalculatorEvents(ObservableObject vm, string? projectId)
    {
        // Wire NavigationRequested + MessageRequested events per VM type
        switch (vm)
        {
            case TileCalculatorViewModel t:
                t.NavigationRequested += OnCalculatorGoBack;
                t.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                t.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = t.LoadFromProjectIdAsync(projectId);
                break;
            case WallpaperCalculatorViewModel w:
                w.NavigationRequested += OnCalculatorGoBack;
                w.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                w.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = w.LoadFromProjectIdAsync(projectId);
                break;
            case PaintCalculatorViewModel p:
                p.NavigationRequested += OnCalculatorGoBack;
                p.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                p.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = p.LoadFromProjectIdAsync(projectId);
                break;
            case FlooringCalculatorViewModel f:
                f.NavigationRequested += OnCalculatorGoBack;
                f.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                f.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = f.LoadFromProjectIdAsync(projectId);
                break;
            case DrywallViewModel d:
                d.NavigationRequested += OnCalculatorGoBack;
                d.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                d.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = d.LoadFromProjectIdAsync(projectId);
                break;
            case ElectricalViewModel e:
                e.NavigationRequested += OnCalculatorGoBack;
                e.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                e.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = e.LoadFromProjectIdAsync(projectId);
                break;
            case MetalViewModel m:
                m.NavigationRequested += OnCalculatorGoBack;
                m.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                m.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = m.LoadFromProjectIdAsync(projectId);
                break;
            case GardenViewModel g:
                g.NavigationRequested += OnCalculatorGoBack;
                g.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                g.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = g.LoadFromProjectIdAsync(projectId);
                break;
            case RoofSolarViewModel r:
                r.NavigationRequested += OnCalculatorGoBack;
                r.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                r.FloatingTextRequested += OnChildFloatingText;
                if (projectId != null) _ = r.LoadFromProjectIdAsync(projectId);
                break;
        }
    }

    private void OnChildFloatingText(string text, string category)
    {
        FloatingTextRequested?.Invoke(text, category);
        if (category == "success")
            CelebrationRequested?.Invoke();
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

    [RelayCommand]
    private async Task RestorePurchases()
    {
        var restored = await _purchaseService.RestorePurchasesAsync();

        if (restored)
        {
            MessageRequested?.Invoke(_localization.GetString("PurchasesRestored"), _localization.GetString("AdsRemovedRestoredMessage"));
            UpdateStatus();
        }
        else
        {
            MessageRequested?.Invoke(_localization.GetString("NoPurchasesFound"), _localization.GetString("NoPurchasesFoundMessage"));
        }
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateStatus);
    }

    private void OnFeedbackRequested(string appName)
    {
        var uri = $"mailto:info@rs-digital.org?subject={Uri.EscapeDataString(appName + " Feedback")}";
        MeineApps.Core.Ava.Services.UriLauncher.OpenUri(uri);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        _premiumAccessService.AccessExpired -= OnAccessExpired;
        SettingsViewModel.LanguageChanged -= OnLanguageChanged;
        SettingsViewModel.FeedbackRequested -= OnFeedbackRequested;
        ProjectsViewModel.NavigationRequested -= OnProjectNavigation;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
