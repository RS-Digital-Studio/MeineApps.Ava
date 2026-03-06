using Avalonia.Controls;
using Avalonia.Media.Transformation;
using RechnerPlus.ViewModels;

namespace RechnerPlus.Views;

public partial class ConverterView : UserControl
{
    private ConverterViewModel? _currentVm;
    private double _swapRotation;

    public ConverterView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Swap-Rotation bei Button-Klick (TransformOperations fuer animierte Transition)
        var swapButton = this.FindControl<Button>("SwapButton");
        if (swapButton != null)
        {
            swapButton.Click += (_, _) =>
            {
                _swapRotation += 180;
                swapButton.RenderTransform = TransformOperations.Parse($"rotate({_swapRotation}deg)");
            };
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden
        if (_currentVm != null)
            _currentVm.ClipboardCopyRequested -= OnClipboardCopy;

        _currentVm = DataContext as ConverterViewModel;

        if (_currentVm != null)
            _currentVm.ClipboardCopyRequested += OnClipboardCopy;
    }

    private async Task OnClipboardCopy(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(text);
    }
}
