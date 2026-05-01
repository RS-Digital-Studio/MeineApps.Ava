using Avalonia.Controls;

namespace HandwerkerRechner.Views;

public partial class ProjectsView : UserControl
{
    public ProjectsView()
    {
        InitializeComponent();
    }

    // Hinweis: Daten-Load erfolgt zentral im MainViewModel.SelectProjectsTab().
    // Hier KEIN doppelter LoadProjectsCommand-Aufruf - sonst race-condition + flackernde Liste.
}
