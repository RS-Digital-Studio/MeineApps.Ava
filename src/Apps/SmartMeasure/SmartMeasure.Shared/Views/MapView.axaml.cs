using Avalonia.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class MapView : UserControl
{
    public MapView()
    {
        InitializeComponent();

        // Mapsui MapControl hat kein AvaloniaProperty fuer Map → Binding nicht moeglich,
        // deshalb manuelle Zuweisung im Code-Behind (Mapsui-Limitation)
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MapViewModel vm)
            {
                MapControl.Map = vm.MapInstance;
            }
        };
    }
}
