using Avalonia.Controls;

namespace BomberBlast.Views.Components;

/// <summary>
/// Audit M20: Onboarding-Overlay als eigenstaendiges UserControl extrahiert aus MainMenuView.axaml.
/// Strict-MVVM: keine Logic im Code-Behind. DataContext erbt automatisch vom Parent (MainMenuViewModel).
/// </summary>
public partial class OnboardingOverlay : UserControl
{
    public OnboardingOverlay()
    {
        InitializeComponent();
    }
}
