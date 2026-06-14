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
    private readonly IAppLifecycleService _lifecycle;

    // Zähler für Berechnungen: nach jeder 3. Berechnung wird ein Opt-in-Ad-Angebot gezeigt
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

    /// <summary>App-Pause/Resume (Android-Lifecycle). Die MainView stoppt darüber ihren
    /// animierten Blueprint-Hintergrund-Render-Timer im Hintergrund (Akku-Sparen);
    /// bei Resume entscheidet die View-eigene Sichtbarkeits-Logik über den Neustart.</summary>
    public event Action<bool>? PauseStateChanged;

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
        QuoteViewModel quoteViewModel,
        IAppLifecycleService lifecycle)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _localization = localization;
        _rewardedAdService = rewardedAdService;
        _calculatorFactory = calculatorFactory;
        _favoritesService = favoritesService;
        _lifecycle = lifecycle;
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

        // App-Lifecycle: Hintergrund-Render-Loop der MainView im Hintergrund anhalten (Akku)
        _lifecycle.Paused += OnAppPaused;
        _lifecycle.Resumed += OnAppResumed;

        UpdateStatus();
        UpdateNavTexts();
    }

    private void OnAppPaused() => PauseStateChanged?.Invoke(true);
    private void OnAppResumed() => PauseStateChanged?.Invoke(false);

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
            calc.MessageRequested += OnChildMessage;
            calc.FloatingTextRequested += OnChildFloatingText;
            calc.ClipboardRequested += OnClipboardRequested;
            calc.CalculationPerformed += OnCalculationPerformed;
            // LoadSafeAsync statt nacktem Fire-and-forget: einheitlicher Exception-Schutz für alle 19 VMs
            if (projectId != null) _ = LoadSafeAsync(calc.LoadFromProjectIdAsync(projectId));
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

    #region Message-Dialog (Inline-Overlay)

    [ObservableProperty]
    private bool _showMessageDialog;

    [ObservableProperty]
    private string _messageDialogTitle = "";

    [ObservableProperty]
    private string _messageDialogText = "";

    [RelayCommand]
    private void DismissMessage() => ShowMessageDialog = false;

    /// <summary>
    /// Zeigt eine Meldung im Inline-Message-Overlay der MainView an.
    /// Feuert zusätzlich das MessageRequested-Event (für externe Abonnenten).
    /// </summary>
    private void ShowMessage(string title, string text)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            MessageDialogTitle = title;
            MessageDialogText = text;
            ShowMessageDialog = true;
        });
        MessageRequested?.Invoke(title, text);
    }

    #endregion

    #region Ad-Opt-in (nach jeder 3. Berechnung Angebot statt erzwungenem Video)

    [ObservableProperty]
    private bool _showAdOfferDialog;

    // Verbleibende bannerfreie Berechnungen nach geschautem Video (session-only, keine Persistenz)
    private int _adFreeCalculationsRemaining;

    /// <summary>
    /// Wird nach jeder erfolgreichen Berechnung aufgerufen.
    /// Nach jeder 3. Berechnung wird ein Opt-in-Angebot gezeigt (kein erzwungenes
    /// Rewarded Video — AdMob-Policy: Rewarded nur mit Einwilligung + Belohnung).
    /// </summary>
    private void OnCalculationPerformed()
    {
        if (_purchaseService.IsPremium) return;

        // Aktive Belohnung: Banner bleibt ausgeblendet, kein neues Angebot
        if (_adFreeCalculationsRemaining > 0)
        {
            _adFreeCalculationsRemaining--;
            if (_adFreeCalculationsRemaining == 0 && _adService.AdsEnabled)
                _adService.ShowBanner();   // Belohnung aufgebraucht → Banner wieder einblenden
            return;
        }

        _calculationCount++;
        if (_calculationCount >= 3)
        {
            _calculationCount = 0;
            ShowAdOfferDialog = true;
        }
    }

    /// <summary>
    /// Opt-in: Video ansehen → Banner für die nächsten 10 Berechnungen ausblenden.
    /// </summary>
    [RelayCommand]
    private async Task WatchAd()
    {
        ShowAdOfferDialog = false;
        try
        {
            var rewarded = await _rewardedAdService.ShowAdAsync("calculation_ad");
            if (rewarded)
            {
                _adFreeCalculationsRemaining = 10;
                _adService.HideBanner();
            }
            // false ohne Exception: Service feuert selbst AdUnavailable → OnAdUnavailable zeigt die Meldung
        }
        catch (Exception)
        {
            OnAdUnavailable();
        }
    }

    [RelayCommand]
    private void DeclineAdOffer()
    {
        ShowAdOfferDialog = false;
        _calculationCount = 0;   // nächstes Angebot erst nach 3 weiteren Berechnungen
    }

    #endregion

    [RelayCommand]
    private async Task PurchaseRemoveAds()
    {
        if (_purchaseService.IsPremium)
        {
            ShowMessage(_localization.GetString("AlreadyAdFree"), _localization.GetString("AlreadyAdFreeMessage"));
            return;
        }

        var success = await _purchaseService.PurchaseRemoveAdsAsync();
        if (success)
        {
            ShowMessage(_localization.GetString("PurchaseSuccessful"), _localization.GetString("RemoveAdsPurchaseSuccessMessage"));
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
        => ShowMessage(title, msg);

    private void OnAdUnavailable()
        => ShowMessage(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);

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
    /// Reihenfolge: Overlays → Projekt-Dialoge → SaveDialog → Calculator/Seiten-Dialoge → Tab → Home (Double-Tap-Exit)
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. App-weite Overlays schließen (Message-Dialog, Ad-Angebot)
        if (ShowMessageDialog)
        {
            ShowMessageDialog = false;
            return true;
        }
        if (ShowAdOfferDialog)
        {
            DeclineAdOffer();
            return true;
        }

        // 2. Projekt-Dialoge schließen (Lösch-Bestätigung, Notizen-Editor)
        if (ProjectsViewModel.ShowDeleteConfirmation)
        {
            ProjectsViewModel.ShowDeleteConfirmation = false;
            return true;
        }
        if (ProjectsViewModel.ShowNotesEditor)
        {
            ProjectsViewModel.ShowNotesEditor = false;
            return true;
        }

        // 3. SaveDialog im aktuellen Calculator schließen
        if (CurrentCalculatorVm is ICalculatorViewModel calc)
        {
            if (calc.ShowSaveDialog)
            {
                calc.ShowSaveDialog = false;
                return true;
            }

            // 4. Calculator schließen → zurück zur Tab-Ansicht
            CurrentPage = null;
            return true;
        }

        // 5. Vorlagen-Seite: Anwenden-Dialog schließen statt Seite
        if (CurrentPage == "ProjectTemplatesPage" && ProjectTemplatesViewModel.ShowApplyDialog)
        {
            ProjectTemplatesViewModel.ShowApplyDialog = false;
            return true;
        }

        // 6. Angebots-Seite: GoBack-Logik des VM nutzen (behandelt IsEditing → Liste statt Seite schließen)
        if (CurrentPage == "QuotePage")
        {
            QuoteViewModel.GoBackCommand.Execute(null);
            return true;
        }

        // Nicht-Calculator-Seiten (Templates) → zurück zur Tab-Ansicht
        if (CurrentCalculatorVm != null)
        {
            CurrentPage = null;
            return true;
        }

        // 7. Vom Projekt-/Settings-Tab zurück zum Home-Tab
        if (SelectedTab != 0)
        {
            SelectHomeTab();
            return true;
        }

        // 8. Auf Home-Tab: Double-Tap-Exit
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

        ProjectTemplatesViewModel.NavigationRequested -= OnTemplateNavigation;
        ProjectTemplatesViewModel.MessageRequested -= OnChildMessage;
        ProjectTemplatesViewModel.FloatingTextRequested -= OnChildFloatingText;
        QuoteViewModel.NavigationRequested -= OnQuoteNavigation;
        QuoteViewModel.MessageRequested -= OnChildMessage;
        QuoteViewModel.FloatingTextRequested -= OnChildFloatingText;
        _favoritesService.FavoritesChanged -= OnFavoritesChanged;

        _lifecycle.Paused -= OnAppPaused;
        _lifecycle.Resumed -= OnAppResumed;

        _disposed = true;
    }
}

/// <summary>Favorit-Eintrag für die Schnellzugriff-Leiste im HomeTab</summary>
public record FavoriteItem(string Route, string Label, string IconKind, bool IsPremium);
