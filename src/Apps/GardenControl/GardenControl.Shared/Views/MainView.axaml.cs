using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GardenControl.Shared.Views;

public partial class MainView : UserControl
{
    /// <summary>Verbunden → grüner Punkt, getrennt → roter Punkt</summary>
    public static readonly FuncValueConverter<bool, IBrush> ConnectionBrushConverter =
        new(connected => connected == true
            ? new SolidColorBrush(Color.Parse("#66BB6A"))
            : new SolidColorBrush(Color.Parse("#EF5350")));

    /// <summary>Verbindungsstatus → Hintergrund-Farbe (subtil)</summary>
    public static readonly FuncValueConverter<bool, IBrush> ConnectionBgConverter =
        new(connected => connected == true
            ? new SolidColorBrush(Color.Parse("#15FFFFFF"))
            : new SolidColorBrush(Color.Parse("#30EF5350")));

    public MainView()
    {
        InitializeComponent();
    }
}
