using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class TradeHistoryView : UserControl
{
    public TradeHistoryView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TradeHistoryViewModel>();
    }
}
