using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandwerkerImperium.Views.Settings;

public partial class ReferralCard : UserControl
{
    public ReferralCard()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
