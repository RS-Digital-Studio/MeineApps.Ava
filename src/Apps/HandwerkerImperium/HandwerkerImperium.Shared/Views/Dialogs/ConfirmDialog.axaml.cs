using Avalonia.Controls;
using Avalonia.Input;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views.Dialogs;

public partial class ConfirmDialog : UserControl
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Klick auf dunklen Hintergrund bricht den Confirm-Dialog ab.
    /// </summary>
    private void OnConfirmOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ConfirmDialogCancelCommand.Execute(null);
    }

    /// <summary>
    /// Klick auf Dialog-Inhalt: Event nicht zum Overlay durchbubblen lassen.
    /// </summary>
    private void OnDialogContentPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
