using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandwerkerImperium.Views.Settings;

/// <summary>AAA-Audit P1: House-Ad-Karte. ViewLocator-Pattern: DataContext kommt von aussen.</summary>
public partial class CrossPromoCard : UserControl
{
    public CrossPromoCard()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
