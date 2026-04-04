using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;

namespace BingXBot.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();

        var vm = App.Services.GetRequiredService<LogViewModel>();
        DataContext = vm;

        // Auto-Scroll: Bei neuen Log-Einträgen nach unten scrollen
        vm.LogEntries.CollectionChanged += OnLogEntriesChanged;
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
