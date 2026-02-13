using Avalonia.Controls;
using Avalonia.Input;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class DayDetailView : UserControl
{
    public DayDetailView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Klick auf den halbtransparenten Hintergrund schließt den Confirm-Dialog
    /// </summary>
    private void OnOverlayBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DayDetailViewModel vm)
        {
            vm.CancelDeleteCommand.Execute(null);
        }
    }
}
