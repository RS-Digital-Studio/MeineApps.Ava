using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class BacktestView : UserControl
{
    public BacktestView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<BacktestViewModel>();
    }
}
