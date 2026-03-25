using Avalonia.Controls;
using HandwerkerRechner.ViewModels;

namespace HandwerkerRechner.Views;

public partial class QuoteView : UserControl
{
    public QuoteView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Angebote beim ersten Laden asynchron abrufen
        if (DataContext is QuoteViewModel vm)
            _ = vm.LoadQuotesAsync();
    }
}
