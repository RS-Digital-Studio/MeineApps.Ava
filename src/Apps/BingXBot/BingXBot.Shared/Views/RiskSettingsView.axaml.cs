using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class RiskSettingsView : UserControl
{
    public RiskSettingsView()
    {
        InitializeComponent();

        // DataContext aus DI holen
        DataContext = App.Services.GetRequiredService<RiskSettingsViewModel>();
    }
}
