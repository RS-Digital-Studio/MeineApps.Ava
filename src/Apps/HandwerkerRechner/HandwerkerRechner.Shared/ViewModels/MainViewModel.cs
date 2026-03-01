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
    public event Action<string>? ClipboardRequested;

    // Sub-ViewModels for embedded tabs
    public SettingsViewModel SettingsViewModel { get; }
    public ProjectsViewModel ProjectsViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }

    public MainViewModel(
        IPurchaseService purchaseService,
        IAdService adService,
        ILocalizationService localization,
        IThemeService themeService,
        SettingsViewModel settingsViewModel,
        ProjectsViewModel projectsViewModel,
        HistoryViewModel historyViewModel,
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

        UpdateStatus();
        UpdateNavTexts();
    }

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
        if (value != null)
            CurrentCalculatorVm = CreateCalculatorVm(value);
        else
            CurrentCalculatorVm = null;
    }

    private void CleanupCurrentCalculator()
    {
        switch (CurrentCalculatorVm)
        {
            // Free-Rechner
            case TileCalculatorViewModel t: t.Cleanup(); break;
            case WallpaperCalculatorViewModel w: w.Cleanup(); break;
            case PaintCalculatorViewModel p: p.Cleanup(); break;
            case FlooringCalculatorViewModel f: f.Cleanup(); break;
            case ConcreteCalculatorViewModel c: c.Cleanup(); break;
            // Premium-Rechner
            case DrywallViewModel dw: dw.Cleanup(); break;
            case ElectricalViewModel e: e.Cleanup(); break;
            case MetalViewModel m: m.Cleanup(); break;
            case GardenViewModel g: g.Cleanup(); break;
            case RoofSolarViewModel r: r.Cleanup(); break;
            case StairsViewModel s: s.Cleanup(); break;
            case PlasterViewModel pl: pl.Cleanup(); break;
            case ScreedViewModel sc: sc.Cleanup(); break;
            case InsulationViewModel ins: ins.Cleanup(); break;
            case CableSizingViewModel cab: cab.Cleanup(); break;
            case GroutViewModel gr: gr.Cleanup(); break;
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
            "ConcretePage" => App.Services.GetRequiredService<ConcreteCalculatorViewModel>(),
            "StairsPage" => App.Services.GetRequiredService<StairsViewModel>(),
            "PlasterPage" => App.Services.GetRequiredService<PlasterViewModel>(),
            "ScreedPage" => App.Services.GetRequiredService<ScreedViewModel>(),
            "InsulationPage" => App.Services.GetRequiredService<InsulationViewModel>(),
            "CableSizingPage" => App.Services.GetRequiredService<CableSizingViewModel>(),
            "GroutPage" => App.Services.GetRequiredService<GroutViewModel>(),
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
                t.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = t.LoadFromProjectIdAsync(projectId);
                break;
            case WallpaperCalculatorViewModel w:
                w.NavigationRequested += OnCalculatorGoBack;
                w.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                w.FloatingTextRequested += OnChildFloatingText;
                w.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = w.LoadFromProjectIdAsync(projectId);
                break;
            case PaintCalculatorViewModel p:
                p.NavigationRequested += OnCalculatorGoBack;
                p.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                p.FloatingTextRequested += OnChildFloatingText;
                p.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = p.LoadFromProjectIdAsync(projectId);
                break;
            case FlooringCalculatorViewModel f:
                f.NavigationRequested += OnCalculatorGoBack;
                f.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                f.FloatingTextRequested += OnChildFloatingText;
                f.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = f.LoadFromProjectIdAsync(projectId);
                break;
            case DrywallViewModel d:
                d.NavigationRequested += OnCalculatorGoBack;
                d.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                d.FloatingTextRequested += OnChildFloatingText;
                d.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = d.LoadFromProjectIdAsync(projectId);
                break;
            case ElectricalViewModel e:
                e.NavigationRequested += OnCalculatorGoBack;
                e.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                e.FloatingTextRequested += OnChildFloatingText;
                e.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = e.LoadFromProjectIdAsync(projectId);
                break;
            case MetalViewModel m:
                m.NavigationRequested += OnCalculatorGoBack;
                m.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                m.FloatingTextRequested += OnChildFloatingText;
                m.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = m.LoadFromProjectIdAsync(projectId);
                break;
            case GardenViewModel g:
                g.NavigationRequested += OnCalculatorGoBack;
                g.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                g.FloatingTextRequested += OnChildFloatingText;
                g.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = g.LoadFromProjectIdAsync(projectId);
                break;
            case RoofSolarViewModel r:
                r.NavigationRequested += OnCalculatorGoBack;
                r.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                r.FloatingTextRequested += OnChildFloatingText;
                r.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = r.LoadFromProjectIdAsync(projectId);
                break;
            case ConcreteCalculatorViewModel c:
                c.NavigationRequested += OnCalculatorGoBack;
                c.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                c.FloatingTextRequested += OnChildFloatingText;
                c.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = c.LoadFromProjectIdAsync(projectId);
                break;
            case StairsViewModel s:
                s.NavigationRequested += OnCalculatorGoBack;
                s.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                s.FloatingTextRequested += OnChildFloatingText;
                s.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = s.LoadFromProjectIdAsync(projectId);
                break;
            case PlasterViewModel pl:
                pl.NavigationRequested += OnCalculatorGoBack;
                pl.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                pl.FloatingTextRequested += OnChildFloatingText;
                pl.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = pl.LoadFromProjectIdAsync(projectId);
                break;
            case ScreedViewModel sc:
                sc.NavigationRequested += OnCalculatorGoBack;
                sc.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                sc.FloatingTextRequested += OnChildFloatingText;
                sc.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = sc.LoadFromProjectIdAsync(projectId);
                break;
            case InsulationViewModel ins:
                ins.NavigationRequested += OnCalculatorGoBack;
                ins.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                ins.FloatingTextRequested += OnChildFloatingText;
                ins.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = ins.LoadFromProjectIdAsync(projectId);
                break;
            case CableSizingViewModel cab:
                cab.NavigationRequested += OnCalculatorGoBack;
                cab.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                cab.FloatingTextRequested += OnChildFloatingText;
                cab.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = cab.LoadFromProjectIdAsync(projectId);
                break;
            case GroutViewModel gr:
                gr.NavigationRequested += OnCalculatorGoBack;
                gr.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);
                gr.FloatingTextRequested += OnChildFloatingText;
                gr.ClipboardRequested += OnClipboardRequested;
                if (projectId != null) _ = gr.LoadFromProjectIdAsync(projectId);
                break;
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

        return routeName is "DrywallPage" or "ElectricalPage" or "MetalPage" or "GardenPage" or "RoofSolarPage" or "StairsPage" or "PlasterPage" or "ScreedPage" or "InsulationPage" or "CableSizingPage" or "GroutPage";
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

    private DateTime _lastBackPress = DateTime.MinValue;

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
        if (CurrentCalculatorVm != null)
        {
            var hasSaveDialog = CurrentCalculatorVm switch
            {
                Floor.TileCalculatorViewModel t => t.ShowSaveDialog,
                Floor.WallpaperCalculatorViewModel w => w.ShowSaveDialog,
                Floor.PaintCalculatorViewModel p => p.ShowSaveDialog,
                Floor.FlooringCalculatorViewModel f => f.ShowSaveDialog,
                Floor.ConcreteCalculatorViewModel c => c.ShowSaveDialog,
                DrywallViewModel d => d.ShowSaveDialog,
                ElectricalViewModel e => e.ShowSaveDialog,
                MetalViewModel m => m.ShowSaveDialog,
                GardenViewModel g => g.ShowSaveDialog,
                RoofSolarViewModel r => r.ShowSaveDialog,
                StairsViewModel s => s.ShowSaveDialog,
                PlasterViewModel pl => pl.ShowSaveDialog,
                ScreedViewModel sc => sc.ShowSaveDialog,
                InsulationViewModel ins => ins.ShowSaveDialog,
                CableSizingViewModel cab => cab.ShowSaveDialog,
                GroutViewModel gr => gr.ShowSaveDialog,
                _ => false
            };

            if (hasSaveDialog)
            {
                // Dialog schließen
                switch (CurrentCalculatorVm)
                {
                    case Floor.TileCalculatorViewModel t: t.ShowSaveDialog = false; break;
                    case Floor.WallpaperCalculatorViewModel w: w.ShowSaveDialog = false; break;
                    case Floor.PaintCalculatorViewModel p: p.ShowSaveDialog = false; break;
                    case Floor.FlooringCalculatorViewModel f: f.ShowSaveDialog = false; break;
                    case Floor.ConcreteCalculatorViewModel c: c.ShowSaveDialog = false; break;
                    case DrywallViewModel d: d.ShowSaveDialog = false; break;
                    case ElectricalViewModel e: e.ShowSaveDialog = false; break;
                    case MetalViewModel m: m.ShowSaveDialog = false; break;
                    case GardenViewModel g: g.ShowSaveDialog = false; break;
                    case RoofSolarViewModel r: r.ShowSaveDialog = false; break;
                    case StairsViewModel s: s.ShowSaveDialog = false; break;
                    case PlasterViewModel pl: pl.ShowSaveDialog = false; break;
                    case ScreedViewModel sc: sc.ShowSaveDialog = false; break;
                    case InsulationViewModel ins: ins.ShowSaveDialog = false; break;
                    case CableSizingViewModel cab: cab.ShowSaveDialog = false; break;
                    case GroutViewModel gr: gr.ShowSaveDialog = false; break;
                }
                return true;
            }

            // 3. Calculator schließen → zurück zur Tab-Ansicht
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
        var now = DateTime.UtcNow;
        if ((now - _lastBackPress).TotalMilliseconds < 2000)
            return false; // App beenden

        _lastBackPress = now;
        ExitHintRequested?.Invoke(_localization.GetString("PressBackToExit") ?? "Press back again to exit");
        return true;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

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

        _disposed = true;
    }
}
