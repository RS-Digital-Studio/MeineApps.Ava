using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using HandwerkerRechner.ViewModels;

namespace HandwerkerRechner.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.ClipboardRequested -= OnClipboardRequested;
        }

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
            _vm.ClipboardRequested += OnClipboardRequested;
        }
    }

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            _ => Color.Parse("#3B82F6")
        };

        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;

        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), h * 0.4, color, 16);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }

    private async void OnClipboardRequested(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }
        catch (Exception)
        {
            // Clipboard-Fehler still ignorieren
        }
    }
}
