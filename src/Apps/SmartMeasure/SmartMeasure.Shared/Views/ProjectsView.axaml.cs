using Avalonia.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class ProjectsView : UserControl
{
    public ProjectsView()
    {
        InitializeComponent();

        // Projekte laden wenn View sichtbar wird
        Loaded += async (_, _) =>
        {
            if (DataContext is ProjectsViewModel vm)
                await vm.LoadProjectsCommand.ExecuteAsync(null);
        };
    }
}
