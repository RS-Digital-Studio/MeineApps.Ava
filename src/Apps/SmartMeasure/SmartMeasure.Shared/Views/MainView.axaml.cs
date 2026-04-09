using Avalonia.Controls;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class MainView : UserControl
{
    private bool _mapViewCreated;

    public MainView()
    {
        InitializeComponent();

        // MapView lazy erstellen wenn Karten-Tab erstmals aktiviert wird
        // Mapsui MapControl crasht auf Android wenn GL-Kontext beim Start nicht bereit
        var mapContainer = this.FindControl<Border>("MapContainer");
        if (mapContainer != null)
        {
            mapContainer.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name != "Classes") return;
                if (_mapViewCreated) return;

                // Prüfen ob MapContainer die "Active" Klasse hat
                if (!mapContainer.Classes.Contains("Active")) return;

                _mapViewCreated = true;
                try
                {
                    if (DataContext is MainViewModel vm)
                    {
                        vm.MapVm.EnsureInitialized();
                        mapContainer.Child = new MapView { DataContext = vm.MapVm };
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MapView-Erstellung fehlgeschlagen: {ex.Message}");
                }
            };
        }
    }
}
