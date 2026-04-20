using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer den Welcome-Flow: CombinedWelcome (Offline-Earnings + DailyReward kombiniert),
/// Starter-Offer (Premium-Sonderangebot fuer neue Spieler), Offline-Earnings-Dialog, Daily-Reward-Dialog.
/// Extrahiert aus MainViewModel.Init.cs (17.04.2026, Phase 3 Schritt 10).
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
}
