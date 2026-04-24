using Avalonia.Controls;
using BingXBot.ViewModels;
using System.Collections.Specialized;

namespace BingXBot.Views;

/// <summary>
/// Log-View mit Auto-Scroll. DataContext wird vom ViewLocator gesetzt —
/// Event-Subscription für CollectionChanged läuft über DataContextChanged.
/// </summary>
public partial class LogView : UserControl
{
    private LogViewModel? _vm;

    public LogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        Unsubscribe();
        if (DataContext is LogViewModel vm)
        {
            _vm = vm;
            vm.LogEntries.CollectionChanged += OnLogEntriesChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_vm != null)
        {
            _vm.LogEntries.CollectionChanged -= OnLogEntriesChanged;
            _vm = null;
        }
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // LogScrollViewer ist jetzt eine ListBox (Virtualisierung)
            var listBox = this.FindControl<ListBox>("LogScrollViewer");
            if (listBox?.ItemCount > 0)
                listBox.ScrollIntoView(listBox.ItemCount - 1);
        }
    }
}
