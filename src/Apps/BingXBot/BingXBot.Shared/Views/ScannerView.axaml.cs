using Avalonia.Controls;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BingXBot.Views;

public partial class ScannerView : UserControl
{
    public ScannerView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ScannerViewModel>();
    }
}
