using Avalonia.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class ProjectsView : UserControl
{
    private bool _isInitialized;

    public ProjectsView()
    {
        InitializeComponent();

        // Projekte nur beim ersten Mal laden (Loaded feuert bei jedem Tab-Wechsel)
        Loaded += async (_, _) =>
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (DataContext is ProjectsViewModel vm)
                await vm.LoadProjectsCommand.ExecuteAsync(null);
        };
    }
}
