using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Partial Class: Prestige-Summary-Dialog (v2.1.0 Aufspaltung Phase 1).
/// Zeigt nach erfolgreichem Prestige Tier + PP + Multiplikator + Count.
/// </summary>
public sealed partial class DialogViewModel
{
    [ObservableProperty]
    private bool _isPrestigeSummaryVisible;

    [ObservableProperty]
    private string _prestigeSummaryTier = "";

    [ObservableProperty]
    private string _prestigeSummaryTierIcon = "";

    [ObservableProperty]
    private string _prestigeSummaryTierColor = "#FFD700";

    [ObservableProperty]
    private string _prestigeSummaryPoints = "";

    [ObservableProperty]
    private string _prestigeSummaryMultiplier = "";

    [ObservableProperty]
    private string _prestigeSummaryCount = "";

    [RelayCommand]
    private void DismissPrestigeSummary()
    {
        IsPrestigeSummaryVisible = false;
    }

    [RelayCommand]
    private void PrestigeSummaryGoToShop()
    {
        IsPrestigeSummaryVisible = false;
        PrestigeSummaryGoToShopRequested?.Invoke();
    }
}
