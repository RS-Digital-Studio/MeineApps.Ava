using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für Quick-Play Modus mit Schwierigkeits-Slider und Seed-Anzeige.
/// Ermöglicht dem Spieler ein zufälliges Level mit einstellbarer Schwierigkeit zu starten.
/// Unterstützt "Challenge a Friend" via Seed-Sharing.
/// </summary>
public sealed partial class QuickPlayViewModel : ViewModelBase, INavigable, IGameJuiceEmitter
{
    private readonly ILocalizationService _localizationService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty] private string _title = "Quick Play";
    [ObservableProperty] private string _difficultyHeaderLabel = "Schwierigkeit";
    [ObservableProperty] private string _difficultyLabel = "Welt 5";
    [ObservableProperty] private string _seedHeaderLabel = "Seed";
    [ObservableProperty] private string _seedText = "12345";
    [ObservableProperty] private string _seedHint = "Teile den Seed um das gleiche Level zu spielen";
    [ObservableProperty] private string _playButtonText = "Spielen!";
    [ObservableProperty] private string _newSeedButtonText = "Neuer Seed";
    [ObservableProperty] private string _challengeButtonText = "Challenge a Friend";
    [ObservableProperty] private int _difficulty = 5; // 1-10

    /// <summary>Letzter Score nach Quick-Play (für Challenge-Sharing)</summary>
    [ObservableProperty] private int _lastScore;
    [ObservableProperty] private bool _hasLastScore;

    private int _currentSeed;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public QuickPlayViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        GenerateNewSeed();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTY CHANGED CALLBACKS
    // ═══════════════════════════════════════════════════════════════════════

    partial void OnDifficultyChanged(int value)
    {
        // Label mit lokalisiertem "Welt X" Text aktualisieren
        DifficultyLabel = string.Format(
            _localizationService.GetString("QuickPlayWorldLevel") ?? "World {0}", value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
    }

    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("QuickPlayTitle") ?? "Quick Play";
        DifficultyHeaderLabel = _localizationService.GetString("QuickPlayDifficulty") ?? "Difficulty";
        SeedHeaderLabel = _localizationService.GetString("QuickPlaySeed") ?? "Seed";
        SeedHint = _localizationService.GetString("QuickPlaySeedHint") ?? "Share the seed to play the same level";
        PlayButtonText = _localizationService.GetString("QuickPlayPlay") ?? "Play!";
        NewSeedButtonText = _localizationService.GetString("QuickPlayNewSeed") ?? "New Seed";
        ChallengeButtonText = _localizationService.GetString("ChallengeAFriend") ?? "Challenge a Friend";
        // DifficultyLabel mit aktuellem Wert auffrischen
        OnDifficultyChanged(Difficulty);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void NewSeed()
    {
        GenerateNewSeed();
    }

    [RelayCommand]
    private void Play()
    {
        NavigationRequested?.Invoke(new GoGame(Mode: "quick", Level: _currentSeed, Difficulty: Difficulty));
    }

    /// <summary>
    /// Teilt den aktuellen Seed + Schwierigkeit als Challenge via Share-Sheet.
    /// Wenn ein Score vorhanden ist, wird dieser mit geteilt.
    /// </summary>
    [RelayCommand]
    private void ShareChallenge()
    {
        var format = _localizationService.GetString("ChallengeShareText")
            ?? "Can you beat my score? Play BomberBlast Seed: {0} Difficulty: {1} - My Score: {2}";

        string shareText;
        if (HasLastScore)
        {
            shareText = string.Format(format, SeedText, Difficulty, LastScore.ToString("N0"));
        }
        else
        {
            // Ohne Score: Nur Seed + Schwierigkeit teilen
            var challengeFormat = _localizationService.GetString("ChallengeAFriend") ?? "Challenge a Friend";
            shareText = $"{challengeFormat}! BomberBlast Seed: {SeedText} Difficulty: {Difficulty}";
        }

        UriLauncher.ShareText(shareText, "BomberBlast Challenge");

        var sharedText = _localizationService.GetString("ChallengeShared") ?? "Challenge shared!";
        FloatingTextRequested?.Invoke(sharedText, "gold");
    }

    /// <summary>Setzt den letzten Score nach Abschluss eines Quick-Play Levels</summary>
    public void SetLastScore(int score)
    {
        LastScore = score;
        HasLastScore = score > 0;
    }

    [RelayCommand]
    private void Back() => NavigationRequested?.Invoke(new GoBack());

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void GenerateNewSeed()
    {
        _currentSeed = Random.Shared.Next(0x100000, 0xFFFFFF);
        SeedText = _currentSeed.ToString("X6");
    }
}
