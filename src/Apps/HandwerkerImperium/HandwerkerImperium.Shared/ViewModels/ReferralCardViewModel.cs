using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer die Referral-Card in den Settings (F-02).
/// Zeigt den eigenen Code, erlaubt Teilen via System-Share-Sheet, nimmt fremden Code an,
/// zeigt Tier-Fortschritt + claimable Tier-Belohnung.
/// </summary>
public sealed partial class ReferralCardViewModel : ViewModelBase
{
    private readonly IReferralService _referralService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _ownCode = "";

    [ObservableProperty]
    private int _successfulReferrals;

    [ObservableProperty]
    private string _progressDisplay = "";

    [ObservableProperty]
    private bool _hasUsedReferralCode;

    [ObservableProperty]
    private string _usedReferralCodeDisplay = "";

    [ObservableProperty]
    private string _enteredCode = "";

    [ObservableProperty]
    private bool _hasClaimableTier;

    public ReferralCardViewModel(IReferralService referralService,
                                 IGameStateService gameStateService,
                                 ILocalizationService localization,
                                 IDialogService dialogService)
    {
        _referralService = referralService;
        _gameStateService = gameStateService;
        _localization = localization;
        _dialogService = dialogService;

        _referralService.TierUnlocked += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>
    /// Aktualisiert alle Anzeige-Properties vom State.
    /// </summary>
    public void Refresh()
    {
        var r = _gameStateService.State.Referral;
        OwnCode = _referralService.GetOwnCode();
        SuccessfulReferrals = r.SuccessfulReferrals;
        HasUsedReferralCode = !string.IsNullOrEmpty(r.UsedReferralCode);
        UsedReferralCodeDisplay = r.UsedReferralCode ?? "";

        // Naechstes Tier-Ziel (1/5/10) — danach voll.
        int next = SuccessfulReferrals < 1 ? 1
                 : SuccessfulReferrals < 5 ? 5
                 : SuccessfulReferrals < 10 ? 10
                 : 10;
        var fmt = _localization.GetString("ReferralProgressFormat") ?? "{0} / {1} erfolgreiche Empfehlungen";
        ProgressDisplay = string.Format(fmt, SuccessfulReferrals, next);

        // Claimable wenn aktuelle Anzahl >= naechstem un-claimed Tier.
        HasClaimableTier = (SuccessfulReferrals >= 1 && !r.ClaimedTiers.Contains(1))
                         || (SuccessfulReferrals >= 5 && !r.ClaimedTiers.Contains(5))
                         || (SuccessfulReferrals >= 10 && !r.ClaimedTiers.Contains(10));
    }

    [RelayCommand]
    private void ShareOwnCode()
    {
        var text = string.Format(
            _localization.GetString("ReferralShareText") ?? "Spiel HandwerkerImperium mit mir! Code: {0}",
            OwnCode);
        UriLauncher.ShareText(text,
            _localization.GetString("ReferralShareTitle") ?? "HandwerkerImperium Empfehlung");
    }

    [RelayCommand]
    private void SubmitEnteredCode()
    {
        if (string.IsNullOrWhiteSpace(EnteredCode)) return;
        var ok = _referralService.SubmitReferralCode(EnteredCode.Trim());
        var title = _localization.GetString(ok ? "ReferralCodeAcceptedTitle" : "ReferralCodeRejectedTitle")
                    ?? (ok ? "Code angenommen" : "Code abgelehnt");
        var body = _localization.GetString(ok ? "ReferralCodeAcceptedBody" : "ReferralCodeRejectedBody")
                   ?? (ok ? "Danke! Sobald dein Werber 24h aktiv ist, bekommt er die Belohnung."
                          : "Code ungültig oder bereits genutzt.");
        _dialogService.ShowAlertDialog(title, body, _localization.GetString("Confirm") ?? "OK");
        if (ok) EnteredCode = "";
        Refresh();
    }

    [RelayCommand]
    private void ClaimNextTier()
    {
        var tier = _referralService.TryClaimNextTier();
        if (tier.HasValue)
        {
            var title = _localization.GetString("ReferralRewardClaimedTitle") ?? "Belohnung erhalten!";
            var body = string.Format(_localization.GetString("ReferralRewardClaimedBody") ?? "Stufe {0} freigeschaltet.",
                tier.Value);
            _dialogService.ShowAlertDialog(title, body, _localization.GetString("Confirm") ?? "OK");
        }
        Refresh();
    }
}
