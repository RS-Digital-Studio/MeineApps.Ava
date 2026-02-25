using Avalonia.Controls;
using Avalonia.Input;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views.Dialogs;

public partial class AlertDialog : UserControl
{
    public AlertDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Klick auf dunklen Hintergrund schließt den Alert-Dialog.
    /// </summary>
    private void OnAlertOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.DismissAlertDialogCommand.Execute(null);
    }

    /// <summary>
    /// Klick auf Dialog-Inhalt: Event nicht zum Overlay durchbubblen lassen.
    /// </summary>
    private void OnDialogContentPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
