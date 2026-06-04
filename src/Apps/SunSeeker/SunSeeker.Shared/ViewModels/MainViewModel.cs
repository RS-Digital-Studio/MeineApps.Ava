using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SunSeeker.Shared.ViewModels;

/// <summary>
/// Navigator: haelt die Tab-ViewModels und steuert den Tab-Wechsel. Der Ausricht-Tab ist der
/// Standard (das ist die Kern-Aktion). Beim Verlassen des Ausricht-Tabs werden dessen Sensoren
/// gestoppt (Akku sparen).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public MainViewModel(DashboardViewModel dashboard, AlignViewModel align)
    {
        Dashboard = dashboard;
        Align = align;
    }

    public DashboardViewModel Dashboard { get; }
    public AlignViewModel Align { get; }

    [ObservableProperty] private bool _isAlignActive = true;
    [ObservableProperty] private bool _isDashboardActive;

    public async Task InitializeAsync()
    {
        await Dashboard.InitializeAsync();
        if (IsAlignActive)
            Align.Activate();
    }

    [RelayCommand]
    private void ShowAlign()
    {
        IsDashboardActive = false;
        IsAlignActive = true;
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        IsAlignActive = false;
        IsDashboardActive = true;
    }

    partial void OnIsAlignActiveChanged(bool value)
    {
        if (value) Align.Activate();
        else Align.Deactivate();
    }
}
