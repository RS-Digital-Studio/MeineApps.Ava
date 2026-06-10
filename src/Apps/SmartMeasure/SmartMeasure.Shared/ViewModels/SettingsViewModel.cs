using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Einstellungen: Einheiten, App-/Datenbank-Info. Werte werden persistiert.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private const string KeyUseMetric = "sm.use_metric";

    private readonly IAppPaths _appPaths;
    private readonly IPreferencesService _preferences;
    private bool _isLoaded;

    [ObservableProperty] private bool _useMetric = true;
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
        UseMetric = _preferences.Get(KeyUseMetric, true);
        _isLoaded = true;
    }

    partial void OnUseMetricChanged(bool value)
    {
        if (_isLoaded) _preferences.Set(KeyUseMetric, value);
    }
}
