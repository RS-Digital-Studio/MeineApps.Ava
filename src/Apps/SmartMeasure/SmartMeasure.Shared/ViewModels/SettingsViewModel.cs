using CommunityToolkit.Mvvm.ComponentModel;
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
    private bool _isLoaded;

    [ObservableProperty] private float _stabHeight = 1.5f;
    [ObservableProperty] private bool _useMetric = true;
    [ObservableProperty] private int _minFixQuality = 5; // Minimum: Float
    [ObservableProperty] private string _appVersion = "1.0.2";
    [ObservableProperty] private string _databaseInfo = "smartmeasure.db";

    public SettingsViewModel(IAppPaths appPaths, IPreferencesService preferences)
    {
        _appPaths = appPaths;
        _preferences = preferences;

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
}
