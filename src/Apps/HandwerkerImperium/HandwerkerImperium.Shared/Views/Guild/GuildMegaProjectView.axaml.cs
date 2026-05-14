using Avalonia.Controls;
using HandwerkerImperium.ViewModels.Guild;

namespace HandwerkerImperium.Views.Guild;

/// <summary>
/// V7 (, Plan Section 3.9): Mega-Projekt-Bauplatz-View.
/// ViewLocator findet diese ueber Namespace-Konvention:
/// .ViewModels.Guild.GuildMegaProjectViewModel → .Views.Guild.GuildMegaProjectView.
///
/// Beim Attach wird der 15s-Refresh-Timer im VM gestartet, beim Detach gestoppt.
/// </summary>
public partial class GuildMegaProjectView : UserControl
{
    public GuildMegaProjectView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => (DataContext as GuildMegaProjectViewModel)?.Start();
        DetachedFromVisualTree += (_, _) => (DataContext as GuildMegaProjectViewModel)?.Stop();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is GuildMegaProjectViewModel vm)
            vm.Start();
    }
}
