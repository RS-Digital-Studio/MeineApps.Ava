using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Einstellungen: Einheiten, Stabhoehe, NTRIP-Profile</summary>
public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private float _stabHeight = 1.5f;
    [ObservableProperty] private bool _useMetric = true;
    [ObservableProperty] private int _minFixQuality = 5; // Minimum: Float
    [ObservableProperty] private string _appVersion = "1.0.2";
    [ObservableProperty] private string _databaseInfo = "smartmeasure.db";

    public SettingsViewModel()
    {
        // Datenbankpfad anzeigen
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "smartmeasure.db");
        if (File.Exists(dbPath))
        {
            var info = new FileInfo(dbPath);
            DatabaseInfo = $"smartmeasure.db ({info.Length / 1024.0:F0} KB)";
        }

        // Version aus Assembly lesen
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        if (ver != null)
            AppVersion = $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }
}
