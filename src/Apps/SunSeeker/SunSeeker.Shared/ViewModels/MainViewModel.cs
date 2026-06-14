using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace SunSeeker.Shared.ViewModels;

/// <summary>
/// Navigator: hält die Tab-ViewModels und steuert den Tab-Wechsel. Der Ausricht-Tab ist der
/// Standard (das ist die Kern-Aktion). Beim Verlassen des Ausricht-Tabs werden dessen Sensoren
/// gestoppt (Akku sparen).
///
/// Zusätzlich am App-Vordergrund-Lifecycle (<see cref="IAppLifecycleService"/>) gekoppelt: geht die
/// App in den Hintergrund (Bildschirm-Sperre/Task-Wechsel), wird der aktuell sichtbare Tab
/// deaktiviert — sonst liefen dessen Hardware-Ressourcen (Heading-Sensor, Anker-MQTT, Sekunden-Timer)
/// im Hintergrund weiter, weil sich der aktive Tab nicht ändert. Beim Resume wird derselbe Tab über
/// den erprobten <c>Activate()</c>-Pfad reaktiviert.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly IAppLifecycleService _lifecycle;
    private readonly BackPressHelper _backPressHelper = new();
    private bool _disposed;

    public MainViewModel(
        DashboardViewModel dashboard, AlignViewModel align, LivePowerViewModel power,
        ILocalizationService localization, IAppLifecycleService lifecycle)
    {
        Dashboard = dashboard;
        Align = align;
        Power = power;
        _localization = localization;
        _lifecycle = lifecycle;
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);
        _lifecycle.Paused += OnAppPaused;
        _lifecycle.Resumed += OnAppResumed;
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

    /// <summary>
    /// App geht in den Hintergrund: nur den aktuell sichtbaren Tab deaktivieren (genau das, was die
    /// Tab-Wechsel-Logik beim Verlassen täte). Die <c>IsXxxActive</c>-Flags bleiben unverändert, damit
    /// beim Resume derselbe Tab sichtbar ist und wieder aktiviert wird. Die Tab-VMs sind intern
    /// flankengeschützt — ein erneutes Deactivate wäre ein No-Op; der Broker feuert ohnehin nur bei
    /// echtem Wechsel.
    /// </summary>
    private void OnAppPaused()
    {
        if (IsAlignActive) Align.Deactivate();
        if (IsPowerActive) Power.Deactivate();
        if (IsDashboardActive) Dashboard.Deactivate();
    }

    /// <summary>App kommt zurück in den Vordergrund: den weiterhin sichtbaren Tab wieder aktivieren.</summary>
    private void OnAppResumed()
    {
        if (IsAlignActive) Align.Activate();
        if (IsPowerActive) Power.Activate();
        if (IsDashboardActive) Dashboard.Activate();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lifecycle.Paused -= OnAppPaused;
        _lifecycle.Resumed -= OnAppResumed;
        Align.Dispose();
        Power.Dispose();
        Dashboard.Dispose();

        GC.SuppressFinalize(this);
    }
}
