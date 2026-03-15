using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        // DataContext aus DI holen
        DataContext = App.Services.GetRequiredService<MainViewModel>();
    }
}
