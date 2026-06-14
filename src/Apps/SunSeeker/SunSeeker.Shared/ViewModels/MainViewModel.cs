using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace SunSeeker.Shared.ViewModels;

/// <summary>
/// Navigator: hält die Tab-ViewModels und steuert den Tab-Wechsel. Der Ausricht-Tab ist der
/// Standard (das ist die Kern-Aktion). Beim Verlassen des Ausricht-Tabs werden dessen Sensoren
/// gestoppt (Akku sparen).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;
    private readonly BackPressHelper _backPressHelper = new();

    public MainViewModel(
        DashboardViewModel dashboard, AlignViewModel align, LivePowerViewModel power,
        ILocalizationService localization)
    {
        Dashboard = dashboard;
        Align = align;
        Power = power;
        _localization = localization;
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);
    }

    /// <summary>Wird ausgelöst, um einen Exit-Hinweis (Toast) anzuzeigen — Double-Back-to-Exit.</summary>
    public event Action<string>? ExitHintRequested;

    public DashboardViewModel Dashboard { get; }
    public AlignViewModel Align { get; }
    public LivePowerViewModel Power { get; }

    [ObservableProperty] private bool _isAlignActive = true;
    [ObservableProperty] private bool _isDashboardActive;
    [ObservableProperty] private bool _isPowerActive;

    public async Task InitializeAsync()
    {
        await Dashboard.InitializeAsync();
        if (IsAlignActive)
            Align.Activate();
    }

    /// <summary>
    /// Behandelt die Android-Zurück-Taste. Gibt true zurück, wenn konsumiert (App bleibt offen),
    /// false, wenn die App beendet werden darf. Reihenfolge: Nicht auf dem Standard-Tab (Ausrichten)
    /// → zurück zum Ausricht-Tab; sonst Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        if (!IsAlignActive)
        {
            ShowAlign();
            return true;
        }
        return _backPressHelper.HandleDoubleBack(_localization.GetString("BackPressToExit"));
    }

    [RelayCommand]
    private void ShowAlign()
    {
        IsDashboardActive = false;
        IsPowerActive = false;
        IsAlignActive = true;
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        IsAlignActive = false;
        IsPowerActive = false;
        IsDashboardActive = true;
    }

    [RelayCommand]
    private void ShowPower()
    {
        IsAlignActive = false;
        IsDashboardActive = false;
        IsPowerActive = true;
    }

    partial void OnIsAlignActiveChanged(bool value)
    {
        if (value) Align.Activate();
        else Align.Deactivate();
    }

    partial void OnIsPowerActiveChanged(bool value)
    {
        if (value) Power.Activate();
        else Power.Deactivate();
    }

    partial void OnIsDashboardActiveChanged(bool value)
    {
        if (value) Dashboard.Activate();
        else Dashboard.Deactivate();
    }
}
