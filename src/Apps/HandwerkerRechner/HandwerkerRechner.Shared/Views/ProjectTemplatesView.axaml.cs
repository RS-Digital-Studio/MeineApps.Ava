using Avalonia.Controls;
using HandwerkerRechner.ViewModels;

namespace HandwerkerRechner.Views;

public partial class ProjectTemplatesView : UserControl
{
    public ProjectTemplatesView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Vorlagen beim ersten Anzeigen asynchron laden
        if (DataContext is ProjectTemplatesViewModel vm)
            _ = vm.LoadTemplatesAsync();
    }
}
