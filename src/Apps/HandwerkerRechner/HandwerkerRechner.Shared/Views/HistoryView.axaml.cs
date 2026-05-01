using Avalonia.Controls;

namespace HandwerkerRechner.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    // Hinweis: Daten-Load erfolgt zentral im MainViewModel.SelectHistoryTab().
    // Hier KEIN doppelter LoadHistoryCommand-Aufruf - sonst race-condition + flackernde Liste.
}
