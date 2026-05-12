using Avalonia.Controls;
using HandwerkerImperium.ViewModels.Guild;

namespace HandwerkerImperium.Views.Guild;

/// <summary>
/// V7 (Phase 4 Ressourcen-Plan, Plan Section 3.9): Mega-Projekt-Bauplatz-View.
/// ViewLocator findet diese ueber Namespace-Konvention (.Views.Guild.GuildBuildSiteView fuer
/// .ViewModels.Guild.GuildMegaProjectViewModel — siehe ViewLocator.GetViewTypeName).
///
/// Beim Attach wird der 15s-Refresh-Timer im VM gestartet, beim Detach gestoppt.
/// </summary>
public partial class GuildBuildSiteView : UserControl
{
    public GuildBuildSiteView()
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
