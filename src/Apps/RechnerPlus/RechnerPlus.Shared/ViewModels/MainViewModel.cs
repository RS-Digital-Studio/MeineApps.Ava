using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.CalcLib;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace RechnerPlus.ViewModels;

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private const string OnboardingShownKey = "onboarding_shown_v2";

    private bool _disposed;
    private readonly ILocalizationService _localization;
    private readonly IPreferencesService _preferences;
    private readonly ExpressionParser _expressionParser;
    private readonly IAppLifecycleService _lifecycle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCalculatorActive))]
    [NotifyPropertyChangedFor(nameof(IsConverterActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    private int _selectedTabIndex;

    [ObservableProperty]
    private CalculatorViewModel _calculatorViewModel;

    [ObservableProperty]
    private ConverterViewModel _converterViewModel;

    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;

    // Lokalisierte Tab-Labels
    public string NavCalculatorText => _localization.GetString("NavCalculator");
    public string NavConverterText => _localization.GetString("NavConverter");
    public string NavSettingsText => _localization.GetString("NavSettings");

    /// <summary>Lokalisierter "Kopieren"-Text für ContextFlyout im History-Verlauf.</summary>
    public string CopyText => _localization.GetString("Copy") ?? "Copy";

    // Active tab indicators
    public bool IsCalculatorActive => SelectedTabIndex == 0;
    public bool IsConverterActive => SelectedTabIndex == 1;
    public bool IsSettingsActive => SelectedTabIndex == 2;

    /// <summary>Event fuer Floating-Text-Anzeige (Text, Kategorie).</summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Wird ausgelöst um einen Exit-Hinweis anzuzeigen (z.B. Toast "Nochmal drücken zum Beenden").</summary>
    public event Action<string>? ExitHintRequested;

    /// <summary>App-Pause/Resume (Android-Lifecycle). Die MainView stoppt darüber ihren
    /// animierten Hintergrund-Render-Timer im Hintergrund (Akku-Sparen).</summary>
    public event Action<bool>? PauseStateChanged;

    public MainViewModel(
        ILocalizationService localization,
        IPreferencesService preferences,
        ExpressionParser expressionParser,
        CalculatorViewModel calculatorViewModel,
        ConverterViewModel converterViewModel,
        SettingsViewModel settingsViewModel,
        IAppLifecycleService lifecycle)
    {
        _localization = localization;
        _preferences = preferences;
        _expressionParser = expressionParser;
        _lifecycle = lifecycle;
        _calculatorViewModel = calculatorViewModel;
        _converterViewModel = converterViewModel;
        _settingsViewModel = settingsViewModel;

        _localization.LanguageChanged += OnLanguageChanged;
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        _lifecycle.Paused += OnAppPaused;
        _lifecycle.Resumed += OnAppResumed;

        // Floating-Text-Events vom Calculator weiterleiten
        CalculatorViewModel.FloatingTextRequested += OnCalculatorFloatingText;

        // Calculator-Engine waermt sich synchron im Ctor auf (einmaliger Parse <1ms).
        // Garantiert: WarmUp findet VOR jeglicher VM-Benutzung statt und kollidiert nicht
        // mit spaeteren UI-Thread-Calls auf ExpressionParser (kein parallel-Zugriff).
        try { _expressionParser.Evaluate("1+1"); }
        catch { /* nicht kritisch - Pre-Warm */ }
    }

    #region Splash / Onboarding (View-Texte + Persistenz via VM)

    /// <summary>Lokalisierter Text: "Grafik-Engine wird vorbereitet..." (Splash-Progress).</summary>
    public string LoadingShadersText => _localization.GetString("LoadingShaders") ?? "Grafik-Engine wird vorbereitet...";

    /// <summary>Lokalisierter Text: "Rechner wird initialisiert..." (Splash-Progress).</summary>
    public string LoadingCalculatorText => _localization.GetString("LoadingCalculator") ?? "Rechner wird initialisiert...";

    /// <summary>Prueft ob das Onboarding bereits angezeigt wurde.</summary>
    public bool IsOnboardingCompleted => _preferences.Get(OnboardingShownKey, false);

    /// <summary>Markiert das Onboarding als abgeschlossen (Persistenz via IPreferencesService).</summary>
    public void MarkOnboardingCompleted() => _preferences.Set(OnboardingShownKey, true);

    /// <summary>Lokalisierte Onboarding-Texte in View-Reihenfolge.</summary>
    public string[] GetOnboardingTexts() =>
    [
        _localization.GetString("OnboardingSwipeDelete") ?? "Wische nach links zum Löschen",
        _localization.GetString("OnboardingSwipeHistory") ?? "Wische hoch für den Verlauf",
        _localization.GetString("OnboardingScientific") ?? "Drehe dein Gerät für den Wissenschaftsmodus"
    ];

    #endregion

    private void OnCalculatorFloatingText(string text, string category)
    {
        FloatingTextRequested?.Invoke(text, category);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(NavCalculatorText));
        OnPropertyChanged(nameof(NavConverterText));
        OnPropertyChanged(nameof(NavSettingsText));
        OnPropertyChanged(nameof(CopyText));
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        // Beim Wechsel zum Rechner: Zahlenformat aktualisieren (falls in Settings geändert)
        if (value == 0)
            CalculatorViewModel.RefreshNumberFormat();
    }

    #region Back-Navigation (Double-Back-to-Exit)

    private readonly BackPressHelper _backPressHelper = new();

    /// <summary>
    /// Behandelt die Zurück-Taste. Gibt true zurück wenn konsumiert (App bleibt offen),
    /// false wenn die App geschlossen werden darf (Double-Back).
    /// Reihenfolge: History schließen → Bestätigungsdialog schließen → Tab zum Rechner zurück → Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. History-Panel offen → schließen
        if (CalculatorViewModel.IsHistoryVisible)
        {
            CalculatorViewModel.HideHistoryCommand.Execute(null);
            return true;
        }

        // 2. Bestätigungsdialog offen → schließen
        if (CalculatorViewModel.ShowClearHistoryConfirm)
        {
            CalculatorViewModel.CancelClearHistoryCommand.Execute(null);
            return true;
        }

        // 3. Nicht auf dem Rechner-Tab → zurück zum Rechner
        if (SelectedTabIndex != 0)
        {
            SelectedTabIndex = 0;
            return true;
        }

        // 4. Auf Startseite: Double-Back-to-Exit
        var msg = _localization.GetString("BackPressToExit") ?? "Erneut drücken zum Beenden";
        return _backPressHelper.HandleDoubleBack(msg);
    }

    #endregion

    [RelayCommand]
    private void NavigateToCalculator() => SelectedTabIndex = 0;

    [RelayCommand]
    private void NavigateToConverter() => SelectedTabIndex = 1;

    [RelayCommand]
    private void NavigateToSettings() => SelectedTabIndex = 2;

    private void OnAppPaused() => PauseStateChanged?.Invoke(true);
    private void OnAppResumed() => PauseStateChanged?.Invoke(false);

    public void Dispose()
    {
        if (_disposed) return;
        _lifecycle.Paused -= OnAppPaused;
        _lifecycle.Resumed -= OnAppResumed;
        _localization.LanguageChanged -= OnLanguageChanged;
        CalculatorViewModel.FloatingTextRequested -= OnCalculatorFloatingText;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
