using Avalonia.Controls;

namespace BingXBot.Views;

/// <summary>
/// Mobile-Dashboard: Balance + Bot-Steuerung + Modus + Strategie + Positionen + Activity.
/// DataContext wird vom ViewLocator gesetzt (via ContentControl).
/// </summary>
public partial class DashboardViewMobile : UserControl
{
    public DashboardViewMobile()
    {
        InitializeComponent();
    }
}
