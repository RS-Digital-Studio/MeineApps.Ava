using Avalonia.Controls;
using Avalonia.Interactivity;
using GardenControl.Shared.ViewModels;

namespace GardenControl.Shared.Views;

public partial class ScheduleView : UserControl
{
    public ScheduleView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ScheduleViewModel vm)
            _ = vm.LoadConfigCommand.ExecuteAsync(null);
    }
}
