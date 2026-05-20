using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer den Live-Event-Chip im Dashboard-BannerStrip (F-16).
/// Zeigt Titel, Score-Progress und Countdown des aktuellen Events; Tap oeffnet
/// einen Reward-Tier-Dialog ueber <see cref="IDialogService"/>.
/// Refresh wird vom GameTick-Coordinator ausgeloest (Per-Tick-Orchestrierung).
/// </summary>
public sealed partial class LiveEventBannerViewModel : ViewModelBase
{
    private readonly ILiveEventService _liveEventService;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _hasActiveLiveEvent;

    [ObservableProperty]
    private string _liveEventTitle = "";

    [ObservableProperty]
    private string _liveEventScoreDisplay = "";

    [ObservableProperty]
    private string _liveEventTimeRemaining = "";

    /// <summary>Fortschritt 0..1 fuer optional als ProgressBar im UI.</summary>
    [ObservableProperty]
    private double _liveEventProgress;

    public LiveEventBannerViewModel(ILiveEventService liveEventService,
                                    ILocalizationService localization,
                                    IDialogService dialogService)
    {
        _liveEventService = liveEventService;
        _localization = localization;
        _dialogService = dialogService;

        _liveEventService.EventStarted += (_, _) => Refresh();
        _liveEventService.EventEnded += (_, _) => Refresh();
    }

    /// <summary>
    /// Aktualisiert Chip-Properties anhand des aktuellen LiveEvent-States.
    /// Wird vom GameTick-Coordinator (~1 Hz) aufgerufen, um Countdown + Score-Display
    /// frisch zu halten — ohne separates VM-Tick.
    /// </summary>
    public void Refresh()
    {
        var ev = _liveEventService.CurrentEvent;
        if (ev == null || !_liveEventService.IsActive)
        {
            HasActiveLiveEvent = false;
            return;
        }

        HasActiveLiveEvent = true;
        LiveEventTitle = _localization.GetString($"LiveEvent_{ev.TemplateId}_Title")
                         ?? ev.TemplateId;

        long maxThreshold = 0;
        for (int i = 0; i < _liveEventService.RewardTierThresholds.Count; i++)
        {
            if (_liveEventService.RewardTierThresholds[i] > maxThreshold)
                maxThreshold = _liveEventService.RewardTierThresholds[i];
        }
        LiveEventScoreDisplay = maxThreshold > 0
            ? $"{ev.PlayerScore}/{maxThreshold}"
            : ev.PlayerScore.ToString(CultureInfo.InvariantCulture);
        LiveEventProgress = maxThreshold > 0
            ? System.Math.Min(1.0, (double)ev.PlayerScore / maxThreshold)
            : 0;

        LiveEventTimeRemaining = ComputeTimeRemaining(ev);
    }

    private static string ComputeTimeRemaining(LiveEvent ev)
    {
        if (!DateTime.TryParse(ev.EndsAtIso, CultureInfo.InvariantCulture,
                               DateTimeStyles.RoundtripKind, out var endsAt))
            return "";
        var remaining = endsAt - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return "";
        if (remaining.TotalHours >= 24)
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{remaining.Minutes}m";
    }

    /// <summary>
    /// Tap auf den Chip — zeigt Reward-Tier-Uebersicht und reicht den naechsten claimbaren
    /// Tier-Reward heraus, falls einer faellig ist (idempotent durch ClaimedRewardTiers).
    /// </summary>
    [RelayCommand]
    private async Task TapChipAsync()
    {
        if (!HasActiveLiveEvent) return;

        var claimed = _liveEventService.TryClaimNextReward();
        if (claimed.HasValue)
        {
            // Bestaetigungs-Toast/Dialog
            var title = _localization.GetString("LiveEventRewardClaimedTitle") ?? "Belohnung erhalten!";
            var body = string.Format(
                _localization.GetString("LiveEventRewardClaimedBody") ?? "Stufe {0} freigeschaltet.",
                claimed.Value + 1);
            _dialogService.ShowAlertDialog(title, body, _localization.GetString("Confirm") ?? "OK");
            Refresh();
            return;
        }

        // Sonst Info-Dialog mit Tier-Liste
        var infoTitle = _localization.GetString("LiveEventDialogTitle") ?? "Live-Event";
        var tiers = _liveEventService.RewardTierThresholds;
        var lines = new List<string> { LiveEventTitle, "", LiveEventScoreDisplay };
        var rewards = new[] { 25, 75, 200 };
        for (int i = 0; i < tiers.Count && i < rewards.Length; i++)
        {
            var done = _liveEventService.CurrentEvent?.ClaimedRewardTiers.Contains(i) == true;
            var marker = done ? "[OK]" : "[ ]";
            lines.Add($"{marker} {tiers[i]} Score → {rewards[i]} GS");
        }
        await _dialogService.ShowConfirmDialog(infoTitle, string.Join("\n", lines),
            _localization.GetString("Confirm") ?? "OK",
            "");
    }
}
