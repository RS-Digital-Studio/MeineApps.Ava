using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Einstellungen: Einheiten, Stabhoehe, NTRIP-Profile</summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private float _stabHeight = 1.5f;
    [ObservableProperty] private bool _useMetric = true;
    [ObservableProperty] private int _minFixQuality = 5; // Minimum: Float
    [ObservableProperty] private string _appVersion = "1.0.0";
}
