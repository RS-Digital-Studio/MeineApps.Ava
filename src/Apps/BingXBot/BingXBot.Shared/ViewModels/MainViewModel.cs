using BingXBot.Core.Enums;
using BingXBot.Trading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot.ViewModels;

/// <summary>
/// Haupt-ViewModel der BingXBot-App. Hält alle Sub-ViewModels (Dashboard, Scanner, etc.)
/// und steuert die Navigation über <see cref="CurrentPageViewModel"/>. Kein direktes
/// View-Referencing mehr — der ViewLocator resolved automatisch VM → View.
///
/// Empfängt Bot-Status-Updates über den BotEventBus und stellt sie UI-seitig bereit.
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly BotEventBus _eventBus;
    private readonly BackPressHelper _backPressHelper = new();

    // Lazy-VMs: Dashboard ist Startup-Page → eager. Alle anderen werden erst on-demand
    // bei Navigation resolved. Eliminiert 6-8 transitiv aufgeloeste VM-Ctors beim App-Start.
    private readonly Lazy<ScannerViewModel> _scanner;
    private readonly Lazy<StrategyViewModel> _strategy;
    private readonly Lazy<BacktestViewModel> _backtest;
    private readonly Lazy<TradeHistoryViewModel> _tradeHistory;
    private readonly Lazy<RiskSettingsViewModel> _riskSettings;
    private readonly Lazy<LogViewModel> _log;
    private readonly Lazy<SettingsViewModel> _settings;
    private readonly Lazy<SettingsHistoryViewModel> _settingsHistory;

    /// <summary>Sub-ViewModels. Werden per DI injiziert und sind als Binding-Quellen öffentlich sichtbar.</summary>
    public DashboardViewModel Dashboard { get; }
    public ScannerViewModel Scanner => _scanner.Value;
    public StrategyViewModel Strategy => _strategy.Value;
    public BacktestViewModel Backtest => _backtest.Value;
    public TradeHistoryViewModel TradeHistory => _tradeHistory.Value;
    public RiskSettingsViewModel RiskSettings => _riskSettings.Value;
    public LogViewModel Log => _log.Value;
    public SettingsViewModel Settings => _settings.Value;
    public SettingsHistoryViewModel SettingsHistory => _settingsHistory.Value;

    /// <summary>Aktuell angezeigtes Sub-ViewModel. ContentControl.Content rendert es via ViewLocator.</summary>
    [ObservableProperty] private ViewModelBase _currentPageViewModel;

    /// <summary>Logische Seitenkennung (Text in Top-Bar) — unabhängig vom VM-Typ gehalten.</summary>
    [ObservableProperty] private string _currentPage = "Dashboard";

    /// <summary>Bot-Status-Text in der Top-Bar.</summary>
    [ObservableProperty] private string _botStatus = "Gestoppt";
    [ObservableProperty] private string _tradingMode = "Paper";
    [ObservableProperty] private string _connectionStatus = "Marktdaten verfügbar";
    [ObservableProperty] private bool _isConnected = true;

    /// <summary>Bottom-Sheet "Mehr" auf Mobile — zeigt seltener genutzte Views (Strategie/Backtest/Risk/Settings).</summary>
    [ObservableProperty] private bool _isMoreDrawerOpen;

    /// <summary>
    /// Gefeuert wenn der User "Zurueck" drueckt und noch ein Hinweis-Toast angezeigt werden soll.
    /// Android-MainActivity subscribt und zeigt einen Toast — Double-Back-to-Exit.
    /// </summary>
    public event Action<string>? ExitHintRequested;

    // === Computed Properties für Tab-Highlighting (werden bei CurrentPageViewModel-Wechsel raised) ===
    // Wichtig: IsValueCreated-Check vermeidet unnoetige Lazy-Initialisierung wenn Tab nie besucht wurde.
    public bool IsDashboardActive => ReferenceEquals(CurrentPageViewModel, Dashboard);
    public bool IsScannerActive => _scanner.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _scanner.Value);
    public bool IsStrategyActive => _strategy.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _strategy.Value);
    public bool IsBacktestActive => _backtest.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _backtest.Value);
    public bool IsTradeHistoryActive => _tradeHistory.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _tradeHistory.Value);
    public bool IsRiskSettingsActive => _riskSettings.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _riskSettings.Value);
    public bool IsLogActive => _log.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _log.Value);
    public bool IsSettingsActive => _settings.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _settings.Value);
    public bool IsSettingsHistoryActive => _settingsHistory.IsValueCreated && ReferenceEquals(CurrentPageViewModel, _settingsHistory.Value);

    /// <summary>Mobile: True wenn aktuell eine Drawer-Seite (Strategie/Backtest/Risk/Settings/Diagnose) sichtbar ODER das Sheet offen ist.</summary>
    public bool IsMoreSectionActive =>
        IsMoreDrawerOpen || IsStrategyActive || IsBacktestActive || IsRiskSettingsActive || IsSettingsActive
        || IsSettingsHistoryActive;

    /// <summary>Farbe des Verbindungs-Indikators (grün=verbunden, rot=getrennt).</summary>
    public string ConnectionDotColor => IsConnected ? "#10B981" : "#EF4444";

    /// <summary>Farbe des Bot-Status-Texts (grün=läuft, gelb=pausiert, rot=fehler, grau=gestoppt).</summary>
    public string BotStatusColor => BotStatus switch
    {
        "Läuft" => "#10B981",
        "Startet..." => "#3B82F6",
        "Pausiert" => "#F59E0B",
        "Notfall-Stop" or "Fehler" => "#EF4444",
        _ => "#94A3B8"
    };

    public MainViewModel(
        BotEventBus eventBus,
        DashboardViewModel dashboard,
        Lazy<ScannerViewModel> scanner,
        Lazy<StrategyViewModel> strategy,
        Lazy<BacktestViewModel> backtest,
        Lazy<TradeHistoryViewModel> tradeHistory,
        Lazy<RiskSettingsViewModel> riskSettings,
        Lazy<LogViewModel> log,
        Lazy<SettingsViewModel> settings,
        Lazy<SettingsHistoryViewModel> settingsHistory)
    {
        _eventBus = eventBus;
        _eventBus.BotStateChanged += OnBotStateChanged;
        _eventBus.TradingModeChanged += OnTradingModeChanged;
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        Dashboard = dashboard;
        _scanner = scanner;
        _strategy = strategy;
        _backtest = backtest;
        _tradeHistory = tradeHistory;
        _riskSettings = riskSettings;
        _log = log;
        _settings = settings;
        _settingsHistory = settingsHistory;

        // Startseite ist Dashboard. Initiale Page-Bezeichnung ist bereits "Dashboard" (Default).
        _currentPageViewModel = Dashboard;
    }

    /// <summary>Raised alle IsXxxActive-Properties wenn das aktuelle VM wechselt (für Tab-Highlighting).</summary>
    partial void OnCurrentPageViewModelChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsScannerActive));
        OnPropertyChanged(nameof(IsStrategyActive));
        OnPropertyChanged(nameof(IsBacktestActive));
        OnPropertyChanged(nameof(IsTradeHistoryActive));
        OnPropertyChanged(nameof(IsRiskSettingsActive));
        OnPropertyChanged(nameof(IsLogActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsSettingsHistoryActive));
        OnPropertyChanged(nameof(IsMoreSectionActive));
    }

    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectionDotColor));
    partial void OnBotStatusChanged(string value) => OnPropertyChanged(nameof(BotStatusColor));
    partial void OnIsMoreDrawerOpenChanged(bool value) => OnPropertyChanged(nameof(IsMoreSectionActive));

    /// <summary>Reagiert auf Paper/Live-Wechsel und aktualisiert die Statusleiste unten rechts.</summary>
    private void OnTradingModeChanged(object? sender, bool isPaper)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            TradingMode = isPaper ? "Paper" : "Live";
        });
    }

    /// <summary>
    /// Android-Back-Handler. Gibt true zurueck wenn die App NICHT beendet werden soll
    /// (Navigation zurueck zu Dashboard ODER erster Back-Press fuer Exit-Hint).
    /// </summary>
    public bool HandleBackPressed()
    {
        // Mobile: Offenes Mehr-Sheet zuerst schliessen.
        if (IsMoreDrawerOpen)
        {
            IsMoreDrawerOpen = false;
            return true;
        }
        // Nicht-Dashboard-Seite? Erst zurueck navigieren.
        if (!IsDashboardActive)
        {
            NavigateTo("Dashboard");
            return true;
        }
        // Auf Dashboard: Double-Back-to-Exit
        return _backPressHelper.HandleDoubleBack("Nochmal druecken zum Beenden");
    }

    private void OnBotStateChanged(object? sender, BotState state)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            BotStatus = state switch
            {
                BotState.Running => "Läuft",
                BotState.Paused => "Pausiert",
                BotState.Stopped => "Gestoppt",
                BotState.Starting => "Startet...",
                BotState.EmergencyStop => "Notfall-Stop",
                BotState.Error => "Fehler",
                _ => state.ToString()
            };

            IsConnected = state is BotState.Running or BotState.Paused or BotState.Starting;
        });
    }

    /// <summary>
    /// Navigation zu einer Seite. Parameter ist immer string (XAML CommandParameter).
    /// Setzt <see cref="CurrentPageViewModel"/> — der ViewLocator rendert die richtige View.
    /// </summary>
    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page;
        CurrentPageViewModel = page switch
        {
            "Dashboard" => Dashboard,
            "Scanner" => Scanner,
            "Strategie" => Strategy,
            "Backtest" => Backtest,
            "TradeHistory" => TradeHistory,
            "RiskSettings" => RiskSettings,
            "Log" => Log,
            "Settings" => Settings,
            "SettingsHistory" => SettingsHistory,
            _ => Dashboard
        };

        // Mobile: Drawer nach Navigation zuklappen (harmlos auf Desktop).
        IsMoreDrawerOpen = false;
    }

    /// <summary>Mobile: Öffnet oder schließt das "Mehr"-Bottom-Sheet.</summary>
    [RelayCommand]
    private void ToggleMoreDrawer() => IsMoreDrawerOpen = !IsMoreDrawerOpen;

    /// <summary>Mobile: Schließt das "Mehr"-Bottom-Sheet (Tap auf Scrim).</summary>
    [RelayCommand]
    private void CloseMoreDrawer() => IsMoreDrawerOpen = false;

    public void Dispose()
    {
        _eventBus.BotStateChanged -= OnBotStateChanged;
        _eventBus.TradingModeChanged -= OnTradingModeChanged;
    }
}
