using Avalonia.Controls;
using Avalonia.Input;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views.Dialogs;

public partial class PrestigeSummaryDialog : UserControl
{
    public PrestigeSummaryDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Klick auf dunklen Hintergrund schließt die Zusammenfassung.
    /// </summary>
    private void OnOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DialogViewModel vm)
            vm.DismissPrestigeSummaryCommand.Execute(null);
    }

    /// <summary>
    /// Klick auf Dialog-Inhalt: Event nicht zum Overlay durchbubblen lassen.
    /// </summary>
    private void OnDialogContentPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
