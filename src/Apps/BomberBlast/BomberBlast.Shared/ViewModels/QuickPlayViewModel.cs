using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für Quick-Play Modus mit Schwierigkeits-Slider und Seed-Anzeige.
/// Ermöglicht dem Spieler ein zufälliges Level mit einstellbarer Schwierigkeit zu starten.
/// </summary>
public partial class QuickPlayViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;

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
    [ObservableProperty] private int _difficulty = 5; // 1-10

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
            _localizationService.GetString("QuickPlayWorldLevel") ?? "Welt {0}", value);
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
        DifficultyHeaderLabel = _localizationService.GetString("QuickPlayDifficulty") ?? "Schwierigkeit";
        SeedHeaderLabel = _localizationService.GetString("QuickPlaySeed") ?? "Seed";
        SeedHint = _localizationService.GetString("QuickPlaySeedHint") ?? "Teile den Seed um das gleiche Level zu spielen";
        PlayButtonText = _localizationService.GetString("QuickPlayPlay") ?? "Spielen!";
        NewSeedButtonText = _localizationService.GetString("QuickPlayNewSeed") ?? "Neuer Seed";
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
        NavigationRequested?.Invoke($"Game?mode=quick&level={_currentSeed}&difficulty={Difficulty}");
    }

    [RelayCommand]
    private void Back() => NavigationRequested?.Invoke("..");

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void GenerateNewSeed()
    {
        _currentSeed = Random.Shared.Next(10000, 99999);
        SeedText = _currentSeed.ToString();
    }
}
