using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace RechnerPlus.ViewModels;

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localization;

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

    public MainViewModel(
        ILocalizationService localization,
        CalculatorViewModel calculatorViewModel,
        ConverterViewModel converterViewModel,
        SettingsViewModel settingsViewModel)
    {
        _localization = localization;
        _calculatorViewModel = calculatorViewModel;
        _converterViewModel = converterViewModel;
        _settingsViewModel = settingsViewModel;

        _localization.LanguageChanged += OnLanguageChanged;
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // Floating-Text-Events vom Calculator weiterleiten
        CalculatorViewModel.FloatingTextRequested += OnCalculatorFloatingText;
    }

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

    public void Dispose()
    {
        if (_disposed) return;
        _localization.LanguageChanged -= OnLanguageChanged;
        CalculatorViewModel.FloatingTextRequested -= OnCalculatorFloatingText;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
