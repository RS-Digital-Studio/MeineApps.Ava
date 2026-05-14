using Avalonia.Controls;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Closing += OnWindowClosing;
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ResumeGameLoop();
    }

    private async void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // H-H05: PauseGameLoopAsync wartet den Save ab. async void ist hier korrekt
        // (Event-Handler) — RunHandlerSafely faengt etwaige Save-Exceptions ab.
        if (DataContext is MainViewModel vm)
            await Helpers.AsyncExtensions.RunHandlerSafely(vm.PauseGameLoopAsync);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Dispose();
    }
}
