using Avalonia.Controls;
using Avalonia.Input;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views.Dialogs;

/// <summary>
/// Kontextueller Hint-Dialog: Tooltip-Bubble oder zentrierter Dialog.
/// Ersetzt den alten TutorialDialog mit kontextuellen Hinweisen.
/// </summary>
public partial class ContextualHintDialog : UserControl
{
    public ContextualHintDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Klick auf Backdrop dismissed den Hint (nur Tooltip-Modus).
    /// </summary>
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DialogViewModel vm)
            vm.DismissHintCommand.Execute(null);
    }

    /// <summary>
    /// Klick auf Bubble: Event nicht zum Backdrop durchbubblen lassen.
    /// </summary>
    private void OnBubblePressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
