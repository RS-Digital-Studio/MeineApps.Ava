using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;

namespace SmartMeasure.Shared.ViewModels;

/// <summary>Einstellungen: Einheiten, Stabhoehe, NTRIP-Profile</summary>
public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private float _stabHeight = 1.5f;
    [ObservableProperty] private bool _useMetric = true;
    [ObservableProperty] private int _minFixQuality = 5; // Minimum: Float
    [ObservableProperty] private string _appVersion = "1.0.0";
}
