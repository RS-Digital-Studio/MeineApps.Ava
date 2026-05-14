using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Partial Class: Alert-Dialog (v2.1.0 Aufspaltung ).
/// Generischer „OK"-Dialog mit Titel, Message, Button-Text.
/// </summary>
public sealed partial class DialogViewModel
{
    [ObservableProperty]
    private bool _isAlertDialogVisible;

    [ObservableProperty]
    private string _alertDialogTitle = "";

    [ObservableProperty]
    private string _alertDialogMessage = "";

    [ObservableProperty]
    private string _alertDialogButtonText = "OK";

    [RelayCommand]
    private void DismissAlertDialog()
    {
        IsAlertDialogVisible = false;
    }
}
