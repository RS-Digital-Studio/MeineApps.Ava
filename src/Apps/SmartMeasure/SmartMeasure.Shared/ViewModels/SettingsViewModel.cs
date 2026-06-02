using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Einstellungen: Einheiten, Stabhöhe, Min-Fix-Quality. Werte werden persistiert.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private const string KeyStabHeight = "sm.stab_height";
    private const string KeyUseMetric = "sm.use_metric";
    private const string KeyMinFixQuality = "sm.min_fix_quality";

    private readonly IAppPaths _appPaths;
    private readonly IPreferencesService _preferences;
    private readonly IHardwareModeService _hardwareMode;
    private bool _isLoaded;

    [ObservableProperty] private float _stabHeight = 1.5f;
    [ObservableProperty] private bool _useMetric = true;
    [ObservableProperty] private int _minFixQuality = 5; // Minimum: Float
    [ObservableProperty] private string _appVersion = "1.0.2";
    [ObservableProperty] private string _databaseInfo = "smartmeasure.db";

    /// <summary>True = RTK-Stab-Optionen + "AR-Modus zuruecksetzen" anzeigen. False = reiner
    /// AR-Modus → stattdessen dezenter "RTK-Stab verbinden"-Hinweis. Aus <see cref="IHardwareModeService"/>.</summary>
    [ObservableProperty] private bool _showRtkUi;

    /// <summary>Navigation anfordern (Event-basiert, Convention). MainViewModel verdrahtet das
    /// auf <c>Navigate(route)</c> — z.B. um zum ausgeblendeten Connect-Screen zu springen.</summary>
    public event Action<string>? NavigationRequested;

    public SettingsViewModel(IAppPaths appPaths, IPreferencesService preferences,
        IHardwareModeService hardwareMode)
    {
        _appPaths = appPaths;
        _preferences = preferences;
        _hardwareMode = hardwareMode;

        ShowRtkUi = _hardwareMode.ShowRtkUi;
        _hardwareMode.Changed += () =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowRtkUi = _hardwareMode.ShowRtkUi);

        // Datenbank-Info aus IAppPaths (sandbox-sicher auf Android, ApplicationData auf Desktop)
        try
        {
            if (File.Exists(_appPaths.DatabasePath))
            {
                var info = new FileInfo(_appPaths.DatabasePath);
                DatabaseInfo = $"smartmeasure.db ({info.Length / 1024.0:F0} KB)";
            }
        }
        catch
        {
            // File-Zugriff kann auf Android mit bestimmten Setups scheitern — harmlos
        }

        // Version aus Assembly
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        if (ver != null)
            AppVersion = $"{ver.Major}.{ver.Minor}.{ver.Build}";

        LoadFromPreferences();
    }

    private void LoadFromPreferences()
    {
        // Generisches Get<T> von IPreferencesService
        StabHeight = _preferences.Get(KeyStabHeight, 1.5f);
        UseMetric = _preferences.Get(KeyUseMetric, true);
        MinFixQuality = _preferences.Get(KeyMinFixQuality, 5);
        _isLoaded = true;
    }

    partial void OnStabHeightChanged(float value)
    {
        if (_isLoaded) _preferences.Set(KeyStabHeight, value);
    }

    partial void OnUseMetricChanged(bool value)
    {
        if (_isLoaded) _preferences.Set(KeyUseMetric, value);
    }

    partial void OnMinFixQualityChanged(int value)
    {
        if (_isLoaded) _preferences.Set(KeyMinFixQuality, value);
    }

    /// <summary>Springt zum (im AR-Modus ausgeblendeten) Connect-Screen, damit der Nutzer
    /// optional einen RTK-Stab koppeln kann. Nach der Verbindung schaltet der
    /// <see cref="IHardwareModeService"/> die volle Hardware-UI automatisch frei.</summary>
    [RelayCommand]
    private void ConnectRtkStick() => NavigationRequested?.Invoke("Connect");

    /// <summary>Setzt die App auf reinen AR-Modus zurueck (blendet die Hardware-UI wieder aus,
    /// sofern kein Stab aktuell verbunden ist).</summary>
    [RelayCommand]
    private void ResetToArMode() => _hardwareMode.ResetToArMode();
}
