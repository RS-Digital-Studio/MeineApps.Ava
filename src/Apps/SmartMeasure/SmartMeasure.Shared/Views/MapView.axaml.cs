using Avalonia.Controls;
using Mapsui.UI.Avalonia;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

/// <summary>
/// Mapsui MapControl wird nicht im XAML erstellt — GL-Context-Init beim Avalonia-Start
/// crasht auf Android. Stattdessen Lazy-Init beim ersten DataContextChanged.
/// </summary>
public partial class MapView : UserControl
{
    private MapControl? _mapControl;
    private Border? _host;

    public MapView()
    {
        InitializeComponent();
        _host = this.FindControl<Border>("MapHost");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is not MapViewModel vm || _host == null) return;

        // Erst beim ersten tatsächlichen VM-Binding das MapControl erzeugen
        if (_mapControl == null)
        {
            _mapControl = new MapControl();
            _host.Child = _mapControl;
        }

        // Map-Instance vom VM holen (Mapsui hat kein AvaloniaProperty → kein Binding möglich)
        _mapControl.Map = vm.MapInstance;
    }
}
