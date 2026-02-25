using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer den Victory-Screen (alle 50 Level geschafft).
/// Zeigt Glueckwunsch, Sterne-Zaehler und Dankes-Text.
/// </summary>
public partial class VictoryViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly ILocalizationService _localizationService;
    private readonly IProgressService _progressService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private readonly IGemService _gemService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    [ObservableProperty]
    private string _titleText = "";

    [ObservableProperty]
    private string _subtitleText = "";

    [ObservableProperty]
    private string _starsText = "";

    [ObservableProperty]
    private string _thanksText = "";

    [ObservableProperty]
    private int _totalStars;

    [ObservableProperty]
    private int _scoreTotal;

    [ObservableProperty]
    private string _scoreTotalText = "";

    /// <summary>Ob der +5 Gems per Ad Button verfügbar ist</summary>
    [ObservableProperty]
    private bool _canWatchAdForGems;

    public VictoryViewModel(
        ILocalizationService localizationService,
        IProgressService progressService,
        IRewardedAdService rewardedAdService,
        IPurchaseService purchaseService,
        IGemService gemService)
    {
        _localizationService = localizationService;
        _progressService = progressService;
        _rewardedAdService = rewardedAdService;
        _purchaseService = purchaseService;
        _gemService = gemService;
    }

    public void OnAppearing()
    {
        TitleText = _localizationService.GetString("VictoryTitle") ?? "Victory!";
        SubtitleText = _localizationService.GetString("VictorySubtitle") ?? "You completed all 50 levels!";
        ThanksText = _localizationService.GetString("VictoryThanks") ?? "Thank you for playing!";

        TotalStars = _progressService.GetTotalStars();
        StarsText = string.Format(
            _localizationService.GetString("VictoryStars") ?? "Total Stars: {0}/150",
            TotalStars);

        // Sieg-Celebration auslösen (Confetti)
        CelebrationRequested?.Invoke();
    }

    /// <summary>
    /// Setzt den Gesamtscore fuer die Anzeige (aus GameOver-Daten).
    /// </summary>
    public void SetScore(int score)
    {
        ScoreTotal = score;
        ScoreTotalText = score.ToString("N0");
        // Premium: Button immer sichtbar (Reward gratis). Free: nur wenn Ad verfügbar + kein Cooldown
        CanWatchAdForGems = _purchaseService.IsPremium ||
            (_rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd);
    }

    /// <summary>
    /// +5 Gems per Rewarded Ad auf dem Victory-Screen.
    /// Premium: Gratis ohne Ad.
    /// </summary>
    [RelayCommand]
    private async Task WatchAdForGemBonus()
    {
        if (!CanWatchAdForGems) return;

        CanWatchAdForGems = false;

        // Premium: Reward sofort gratis (kein Ad nötig)
        var success = _purchaseService.IsPremium || await _rewardedAdService.ShowAdAsync("gem_bonus");
        if (success)
        {
            if (!_purchaseService.IsPremium) RewardedAdCooldownTracker.RecordAdShown();
            _gemService.AddGems(5);
            FloatingTextRequested?.Invoke("+5 Gems!", "cyan");
        }
        else
        {
            // Ad fehlgeschlagen → wieder aktivieren
            CanWatchAdForGems = _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
        }
    }

    [RelayCommand]
    private void GoToMainMenu() => NavigationRequested?.Invoke(new GoResetThen(new GoMainMenu()));

    [RelayCommand]
    private void GoToShop() => NavigationRequested?.Invoke(new GoShop());
}
