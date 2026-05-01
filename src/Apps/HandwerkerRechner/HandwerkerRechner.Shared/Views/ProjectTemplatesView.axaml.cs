using Avalonia.Controls;

namespace HandwerkerRechner.Views;

public partial class ProjectTemplatesView : UserControl
{
    public ProjectTemplatesView()
    {
        InitializeComponent();
    }

    // Hinweis: LoadTemplatesAsync erfolgt zentral im MainViewModel.OnCurrentPageChanged().
    // Hier KEIN doppelter Fire-and-forget-Aufruf - sonst Doppel-Load + ungehandelte Exceptions.
}
