using Avalonia.Controls;

namespace BomberBlast.Views.Components;

/// <summary>
/// What's-New-Modal (Sprint 4.3 AAA-Audit #17). Wird ueber MainViewModel.IsWhatsNewVisible
/// gesteuert; DataContext erbt vom MainViewModel (Modal-Inhalt nutzt
/// <c>{Binding WhatsNewVm}</c>-Pfad fuer WhatsNewViewModel-Bindings).
/// Strict-MVVM: kein Code-Behind ausser InitializeComponent.
/// </summary>
public partial class WhatsNewOverlay : UserControl
{
    public WhatsNewOverlay()
    {
        InitializeComponent();
    }
}
