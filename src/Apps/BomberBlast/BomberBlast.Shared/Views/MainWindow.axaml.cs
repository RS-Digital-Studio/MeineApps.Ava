using Avalonia.Controls;
using Avalonia.Input;
using BomberBlast.ViewModels;

namespace BomberBlast.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Handle keyboard input at window level so it always works
        // regardless of which child control has focus.
        KeyDown += OnWindowKeyDown;
        KeyUp += OnWindowKeyUp;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // GameVm ist seit v2.0.36 nullable (Lazy-Resolution). Bei IsGameActive=true ist es garantiert da,
        // null-conditional als zusaetzliche Sicherung gegen Race wenn IsGameActive vor GameVm-Init feuert.
        if (DataContext is MainViewModel mainVm && mainVm.IsGameActive && mainVm.GameVm is { } gameVm)
        {
            gameVm.OnKeyDown(e.Key);
            e.Handled = true;
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel mainVm && mainVm.IsGameActive && mainVm.GameVm is { } gameVm)
        {
            gameVm.OnKeyUp(e.Key);
            e.Handled = true;
        }
    }
}
