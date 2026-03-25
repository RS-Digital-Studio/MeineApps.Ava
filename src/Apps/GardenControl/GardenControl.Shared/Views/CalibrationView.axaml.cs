using Avalonia.Controls;
using Avalonia.Interactivity;
using GardenControl.Shared.ViewModels;

namespace GardenControl.Shared.Views;

public partial class CalibrationView : UserControl
{
    public CalibrationView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CalibrationViewModel vm)
            _ = vm.LoadZonesCommand.ExecuteAsync(null);
    }
}
