using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BomberBlast.Tests.Ui;

public partial class BrokenContextControl : UserControl
{
    public BrokenContextControl() => AvaloniaXamlLoader.Load(this);
}
