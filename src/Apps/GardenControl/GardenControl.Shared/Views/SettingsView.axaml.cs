using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace GardenControl.Shared.Views;

public partial class SettingsView : UserControl
{
    public static readonly FuncValueConverter<bool, string> ConnectedTextConverter =
        new(connected => connected == true ? "Verbunden mit Server" : "Nicht verbunden");

    public SettingsView()
    {
        InitializeComponent();
    }
}
