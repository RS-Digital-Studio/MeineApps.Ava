using Avalonia.Controls;
using Avalonia.Input;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views.Dialogs;

public partial class NotificationCenterPopup : UserControl
{
    public NotificationCenterPopup()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Klick auf den Hintergrund schliesst das Popup (kein Loeschen, nur Verstecken).
    /// </summary>
    private void OnOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is NotificationCenterViewModel vm)
            vm.ClosePopupCommand.Execute(null);
    }

    /// <summary>
    /// Klick auf die Karte selbst soll nicht zum Overlay durchbubbeln.
    /// </summary>
    private void OnContentPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
