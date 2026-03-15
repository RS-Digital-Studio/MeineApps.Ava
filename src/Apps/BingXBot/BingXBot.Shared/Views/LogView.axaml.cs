using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<LogViewModel>();
    }
}
