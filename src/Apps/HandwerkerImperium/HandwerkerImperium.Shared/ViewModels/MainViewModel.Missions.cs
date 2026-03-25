using CommunityToolkit.Mvvm.Input;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Missions-Logik extrahiert nach MissionsFeatureViewModel (19.03.2026).
// Nur noch LuckySpin-Overlay-Steuerung (IsLuckySpinVisible ist ActivePage-Overlay im MainViewModel).
public sealed partial class MainViewModel
{
    /// <summary>
    /// Zeigt das Gluecksrad-Overlay. Delegiert Refresh/Timer an MissionsVM,
    /// setzt aber IsLuckySpinVisible hier (Overlay-State im MainViewModel).
    /// </summary>
    [RelayCommand]
    private void ShowLuckySpin()
    {
        MissionsVM.ShowLuckySpinCommand.Execute(null);
        IsLuckySpinVisible = true;
    }

    /// <summary>
    /// Versteckt das Gluecksrad-Overlay.
    /// </summary>
    private void HideLuckySpinOverlay()
    {
        MissionsVM.HideLuckySpinCommand.Execute(null);
        IsLuckySpinVisible = false;
    }
}
