using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Partial Class: Level-Up-Dialog (v2.1.0 Aufspaltung Phase 1).
/// Eigene Datei fuer Wartbarkeit — Bindings bleiben auf DialogViewModel,
/// keine UI-Aenderung noetig.
/// </summary>
public sealed partial class DialogViewModel
{
    [ObservableProperty]
    private bool _isLevelUpDialogVisible;

    [ObservableProperty]
    private bool _isLevelUpPulsing;

    [ObservableProperty]
    private int _levelUpNewLevel;

    [ObservableProperty]
    private string _levelUpUnlockedText = "";

    [RelayCommand]
    private void DismissLevelUpDialog()
    {
        IsLevelUpDialogVisible = false;
    }
}
