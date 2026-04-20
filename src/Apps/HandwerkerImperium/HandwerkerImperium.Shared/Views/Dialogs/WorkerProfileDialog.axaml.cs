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
    /// Nutzt den dedizierten <see cref="MainViewModel.CloseWorkerProfileCommand"/> statt
    /// des komplexen <c>HandleBackPressed()</c>-Pfads (MVVM-sauber, keine Seiten-Effekte auf andere Overlays).
    /// </summary>
    private void OnWorkerProfileBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.CloseWorkerProfileCommand.CanExecute(null))
            vm.CloseWorkerProfileCommand.Execute(null);
    }

    /// <summary>
    /// Klick auf Dialog-Inhalt: Event nicht zum Overlay durchbubblen lassen.
    /// </summary>
    private void OnDialogContentPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
