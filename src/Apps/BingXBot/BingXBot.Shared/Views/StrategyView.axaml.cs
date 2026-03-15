using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class StrategyView : UserControl
{
    public StrategyView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<StrategyViewModel>();
    }
}
