using Avalonia.Controls;

namespace BomberBlast.Views.Components;

/// <summary>
/// Audit M20: Daily-Reward-Overlay als eigenstaendiges UserControl extrahiert aus MainMenuView.axaml.
/// Strict-MVVM: keine Logic im Code-Behind. DataContext erbt automatisch vom Parent (MainMenuViewModel).
/// </summary>
public partial class DailyRewardOverlay : UserControl
{
    public DailyRewardOverlay()
    {
        InitializeComponent();
    }
}
