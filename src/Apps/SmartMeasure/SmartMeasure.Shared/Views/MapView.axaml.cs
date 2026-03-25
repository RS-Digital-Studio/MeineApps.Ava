using Avalonia.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class MapView : UserControl
{
    public MapView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MapViewModel vm)
            {
                MapControl.Map = vm.MapInstance;
            }
        };
    }
}
