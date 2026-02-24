using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.League;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// Profil-Seite: Spielername editieren, aktiver Skin/Frame, Stats-Übersicht.
/// </summary>
public partial class ProfileViewModel : ObservableObject
{
    private readonly ILeagueService _leagueService;
    private readonly ICustomizationService _customizationService;
    private readonly IProgressService _progressService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly IAchievementService _achievementService;
    private readonly ILocalizationService _localization;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;

    // === OBSERVABLE PROPERTIES ===

    [ObservableProperty] private string _titleText = "";
    [ObservableProperty] private string _playerName = "";
    [ObservableProperty] private string _nameHintText = "";
    [ObservableProperty] private string _saveButtonText = "";
    [ObservableProperty] private string _skinLabel = "";
    [ObservableProperty] private string _frameLabel = "";

    // Skin-Info
    [ObservableProperty] private string _activeSkinName = "";
    [ObservableProperty] private string _activeSkinColor = "#FFFFFF";
    [ObservableProperty] private string _activeFrameName = "";

    // Stats
    [ObservableProperty] private string _starsText = "";
    [ObservableProperty] private string _starsLabel = "";
    [ObservableProperty] private string _coinsText = "";
    [ObservableProperty] private string _coinsLabel = "";
    [ObservableProperty] private string _gemsText = "";
    [ObservableProperty] private string _gemsLabel = "";
    [ObservableProperty] private string _leagueText = "";
    [ObservableProperty] private string _leagueLabel = "";
    [ObservableProperty] private string _leagueColor = "#FFFFFF";
    [ObservableProperty] private string _achievementsText = "";
    [ObservableProperty] private string _achievementsLabel = "";
    [ObservableProperty] private int _achievementPercent;

    public ProfileViewModel(
        ILeagueService leagueService,
        ICustomizationService customizationService,
        IProgressService progressService,
        ICoinService coinService,
        IGemService gemService,
        IAchievementService achievementService,
        ILocalizationService localization)
    {
        _leagueService = leagueService;
        _customizationService = customizationService;
        _progressService = progressService;
        _coinService = coinService;
        _gemService = gemService;
        _achievementService = achievementService;
        _localization = localization;

        _coinService.BalanceChanged += (_, _) => UpdateStats();
        _gemService.BalanceChanged += (_, _) => UpdateStats();
    }

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        UpdateStats();

        // Spielernamen laden
        PlayerName = _leagueService.PlayerName;

        // Skin-Info (PrimaryColor ist SKColor)
        var skin = _customizationService.PlayerSkin;
        ActiveSkinName = _localization.GetString(skin.NameKey) ?? skin.Id;
        ActiveSkinColor = $"#{skin.PrimaryColor.Red:X2}{skin.PrimaryColor.Green:X2}{skin.PrimaryColor.Blue:X2}";

        var frame = _customizationService.ActiveFrame;
        ActiveFrameName = frame != null
            ? (_localization.GetString(frame.NameKey) ?? frame.Id)
            : (_localization.GetString("ProfileNoFrame") ?? "-");
    }

    public void UpdateLocalizedTexts()
    {
        TitleText = _localization.GetString("ProfileTitle") ?? "Profil";
        NameHintText = _localization.GetString("ProfileNameHint") ?? "Max. 16 Zeichen";
        SaveButtonText = _localization.GetString("ProfileSave") ?? "Speichern";
        SkinLabel = _localization.GetString("ProfileSkin") ?? "Skin";
        FrameLabel = _localization.GetString("ProfileFrame") ?? "Rahmen";
        StarsLabel = _localization.GetString("ProfileStars") ?? "Sterne";
        CoinsLabel = _localization.GetString("ProfileCoins") ?? "Coins";
        GemsLabel = _localization.GetString("ProfileGems") ?? "Gems";
        LeagueLabel = _localization.GetString("ProfileLeague") ?? "Liga";
        AchievementsLabel = _localization.GetString("ProfileAchievements") ?? "Erfolge";
    }

    private void UpdateStats()
    {
        StarsText = $"{_progressService.GetTotalStars()}";
        CoinsText = $"{_coinService.Balance:N0}";
        GemsText = $"{_gemService.Balance:N0}";

        // Liga (GetColor() gibt string zurück, z.B. "#CD7F32")
        var tier = _leagueService.CurrentTier;
        var tierName = _localization.GetString(tier.GetNameKey()) ?? tier.ToString();
        LeagueText = tierName;
        LeagueColor = tier.GetColor();

        // Achievements
        int unlocked = _achievementService.UnlockedCount;
        int total = _achievementService.TotalCount;
        AchievementsText = $"{unlocked}/{total}";
        AchievementPercent = total > 0 ? unlocked * 100 / total : 0;
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    [RelayCommand]
    private void SaveName()
    {
        var name = PlayerName?.Trim() ?? "";
        if (name.Length > 16) name = name[..16];
        if (string.IsNullOrWhiteSpace(name)) return;

        PlayerName = name;
        _leagueService.SetPlayerName(name);

        var msg = _localization.GetString("ProfileSaved") ?? "Gespeichert!";
        FloatingTextRequested?.Invoke(msg, "success");
    }
}
