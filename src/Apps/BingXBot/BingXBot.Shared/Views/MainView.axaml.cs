using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace BingXBot.Views;

public partial class MainView : UserControl
{
    private static readonly SolidColorBrush ConnectedBrush = new(Color.Parse("#10B981"));
    private static readonly SolidColorBrush DisconnectedBrush = new(Color.Parse("#EF4444"));

    private MainViewModel? _vm;

    public MainView()
    {
        InitializeComponent();

        DataContext = App.Services.GetRequiredService<MainViewModel>();

        if (DataContext is MainViewModel vm)
        {
            _vm = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateConnectionDot();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsConnected))
            UpdateConnectionDot();
    }

    private void UpdateConnectionDot()
    {
        if (_vm == null) return;
        var dot = this.FindControl<Ellipse>("ConnectionDot");
        if (dot != null)
            dot.Fill = _vm.IsConnected ? ConnectedBrush : DisconnectedBrush;
    }
}
