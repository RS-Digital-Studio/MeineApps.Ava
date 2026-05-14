using Avalonia.Controls;

namespace BomberBlast.Views.Components;

/// <summary>
/// What's-New-Modal (.3 . Wird ueber MainViewModel.IsWhatsNewVisible
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
