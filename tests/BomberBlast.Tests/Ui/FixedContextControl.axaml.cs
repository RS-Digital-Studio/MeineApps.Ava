using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BomberBlast.Tests.Ui;

public partial class FixedContextControl : UserControl
{
    public FixedContextControl() => AvaloniaXamlLoader.Load(this);
}
