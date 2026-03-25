using Avalonia.Controls;
using Avalonia.Interactivity;
using GardenControl.Shared.ViewModels;

namespace GardenControl.Shared.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm)
            _ = vm.LoadDataCommand.ExecuteAsync(null);
    }
}
