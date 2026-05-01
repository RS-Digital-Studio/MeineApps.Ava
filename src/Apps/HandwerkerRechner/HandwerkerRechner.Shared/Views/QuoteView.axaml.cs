using Avalonia.Controls;

namespace HandwerkerRechner.Views;

public partial class QuoteView : UserControl
{
    public QuoteView()
    {
        InitializeComponent();
    }

    // Hinweis: LoadQuotesAsync erfolgt zentral im MainViewModel.OnCurrentPageChanged().
    // Hier KEIN doppelter Fire-and-forget-Aufruf - sonst Doppel-Load + ungehandelte Exceptions.
}
