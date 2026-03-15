using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // DataContext aus DI holen
        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
    }
}
