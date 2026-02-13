using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
        Focusable = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
        }

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
        }
    }

    // === Keyboard Shortcuts (Desktop) ===

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm == null) return;

        switch (e.Key)
        {
            // F5 = Aktualisieren
            case Key.F5:
                _vm.LoadDataCommand.Execute(null);
                e.Handled = true;
                break;

            // Escape = Sub-Page schließen
            case Key.Escape:
                if (_vm.IsSubPageActive)
                {
                    _vm.GoBackCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            // Ziffern 1-5 = Tab-Navigation
            case Key.D1: _vm.SelectTodayTabCommand.Execute(null); e.Handled = true; break;
            case Key.D2: _vm.SelectWeekTabCommand.Execute(null); e.Handled = true; break;
            case Key.D3: _vm.SelectCalendarTabCommand.Execute(null); e.Handled = true; break;
            case Key.D4: _vm.SelectStatisticsTabCommand.Execute(null); e.Handled = true; break;
            case Key.D5: _vm.SelectSettingsTabCommand.Execute(null); e.Handled = true; break;

            // Ctrl+Z = Undo
            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                if (_vm.IsUndoVisible)
                {
                    _vm.UndoLastActionCommand.Execute(null);
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            "overtime" => Color.Parse("#F59E0B"),
            _ => Color.Parse("#3B82F6")
        };
        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;
        // Position 40-50% der Höhe → gut sichtbar auf allen Bildschirmgrößen
        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), Math.Max(100, h * 0.45), color, 18);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }
}
