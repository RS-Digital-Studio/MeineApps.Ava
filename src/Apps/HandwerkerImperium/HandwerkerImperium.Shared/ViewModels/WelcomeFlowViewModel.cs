using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer den Welcome-Flow: CombinedWelcome (Offline-Earnings + DailyReward kombiniert),
/// Starter-Offer (Premium-Sonderangebot fuer neue Spieler), Offline-Earnings-Dialog, Daily-Reward-Dialog.
/// Extrahiert aus MainViewModel.Init.cs (17.04.2026, Schritt 10).
/// Wird ueber <see cref="MainViewModel.WelcomeFlowVM"/> in den Views referenziert.
/// MainViewModel bietet Delegate-Properties fuer Rueckwaertskompatibilitaet mit bestehenden Bindings.
/// </summary>
public sealed partial class WelcomeFlowViewModel : ViewModelBase
{
    // CombinedWelcome (Offline + Welcome-Offer in einem Dialog)
    [ObservableProperty] private bool _isCombinedWelcomeDialogVisible;
    [ObservableProperty] private string _combinedOfflineEarnings = "";
    [ObservableProperty] private string _combinedOfferMoney = "";
    [ObservableProperty] private string _combinedOfferScrews = "";
    [ObservableProperty] private string _combinedOfflineDuration = "";
    [ObservableProperty] private string _combinedOfferTimer = "";

    // Starter-Offer (einmaliges Premium-Angebot ab Lv.10)
    [ObservableProperty] private bool _isStarterOfferVisible;
    [ObservableProperty] private string _starterOfferCountdown = string.Empty;

    // Offline-Earnings-Dialog (separat zu Combined)
    [ObservableProperty] private bool _isOfflineEarningsDialogVisible;
    [ObservableProperty] private string _offlineEarningsAmountText = "";
    [ObservableProperty] private string _offlineEarningsDurationText = "";
    [ObservableProperty] private bool _isOfflineNewRecord;

    /// <summary>Prozent-Hinweis im Offline-Dialog.</summary>
    [ObservableProperty] private string _offlineEfficiencyHint = "";

    /// <summary>Naechstes Ziel als Wiedereinsteiger-Tipp (nur bei >48h Offline-Pause).</summary>
    [ObservableProperty] private string _offlineGoalText = "";

    // Daily-Reward-Dialog
    [ObservableProperty] private bool _isDailyRewardDialogVisible;
    [ObservableProperty] private string _dailyRewardDayText = "";
    [ObservableProperty] private string _dailyRewardStreakText = "";
    [ObservableProperty] private string _dailyRewardAmountText = "";

    // Streak-Dots als Datengebundene Sammlung statt 7 hardcodierter
    // Border-Elemente. StreakDays[i].IsCurrent = true beim aktuellen Tag, IsClaimed = true
    // fuer schon erhaltene Tage. Tag-1-Spieler sieht jetzt einen echten Status, kein
    // statisches alternierendes Muster.
    public System.Collections.ObjectModel.ObservableCollection<StreakDayItem> StreakDays { get; } = new();

    public void UpdateStreakDays(int currentDay, int streakCount)
    {
        StreakDays.Clear();
        for (int i = 1; i <= 7; i++)
        {
            StreakDays.Add(new StreakDayItem
            {
                Day = i,
                IsCurrent = i == currentDay,
                IsClaimed = i < currentDay || (i == currentDay && i <= streakCount),
                IsMilestone = i == 7
            });
        }
    }
}

/// <summary>
/// Datenklasse fuer einen Streak-Dot im DailyRewardDialog.
/// </summary>
public sealed class StreakDayItem
{
    public int Day { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsClaimed { get; init; }
    public bool IsMilestone { get; init; }
}
