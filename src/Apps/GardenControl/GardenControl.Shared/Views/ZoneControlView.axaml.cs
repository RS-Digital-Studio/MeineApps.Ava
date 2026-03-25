using Avalonia.Controls;
using Avalonia.Data.Converters;
using Material.Icons;

namespace GardenControl.Shared.Views;

public partial class ZoneControlView : UserControl
{
    public static readonly FuncValueConverter<bool, MaterialIconKind> WateringIconConverter =
        new(watering => watering == true ? MaterialIconKind.Stop : MaterialIconKind.Play);

    public static readonly FuncValueConverter<bool, string> WateringTextConverter =
        new(watering => watering == true ? "Stopp" : "Start");

    public ZoneControlView()
    {
        InitializeComponent();
    }
}
