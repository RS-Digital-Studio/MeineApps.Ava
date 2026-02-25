using Avalonia.Controls;
using Avalonia.Input;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views.Dialogs;

public partial class WorkerProfileDialog : UserControl
{
    public WorkerProfileDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Backdrop-Klick schließt das Worker-Profile Bottom-Sheet.
    /// </summary>
    private void OnWorkerProfileBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.HandleBackPressed();
    }

    /// <summary>
    /// Klick auf Dialog-Inhalt: Event nicht zum Overlay durchbubblen lassen.
    /// </summary>
    private void OnDialogContentPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
