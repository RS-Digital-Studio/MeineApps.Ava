using Avalonia.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class ProjectsView : UserControl
{
    public ProjectsView()
    {
        InitializeComponent();

        // Init-Logik im ViewModel (MVVM), View triggert nur bei erstem Laden
        Loaded += async (_, _) =>
        {
            if (DataContext is ProjectsViewModel vm)
                await vm.EnsureInitializedAsync();
        };
    }
}
