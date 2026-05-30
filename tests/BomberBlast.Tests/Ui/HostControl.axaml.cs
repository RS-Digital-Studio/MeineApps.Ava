using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BomberBlast.Tests.Ui;

public partial class HostControl : UserControl
{
    public HostControl() => AvaloniaXamlLoader.Load(this);
}
