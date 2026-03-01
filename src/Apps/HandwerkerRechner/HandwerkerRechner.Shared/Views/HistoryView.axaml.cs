using Avalonia.Controls;
using HandwerkerRechner.ViewModels;

namespace HandwerkerRechner.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is HistoryViewModel vm)
        {
            vm.LoadHistoryCommand.Execute(null);
        }
    }
}
