using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace BomberBlast.ViewModels;

/// <summary>
/// Weekly Boss-Rush Pre-Run-Screen + Wochen-Leaderboard (v2.0.42, Plan Task 3.3).
/// Spieler sieht Wochen-Best-Score + Lifetime-Stats + "Start"-Button.
/// Beim Tap auf Start wird via GoGame mit Mode="bossrush" das erste Boss-Level gestartet.
/// </summary>
public sealed partial class BossRushViewModel : ViewModelBase, INavigable, IGameJuiceEmitter
{
    private readonly IBossRushService _bossRushService;
    private readonly ILocalizationService _localization;

    public event Action<NavigationRequest>? NavigationRequested;

    // IGameJuiceEmitter-Pflichtfelder. BossRush emittiert aktuell keine Floating-Texte (kommt mit GameEngine-Hook in Folge-Iteration).
#pragma warning disable CS0067
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;
#pragma warning restore CS0067

    [ObservableProperty] private string _titleText = "";
    [ObservableProperty] private string _descText = "";
    [ObservableProperty] private string _weeklyBestText = "";
    [ObservableProperty] private string _totalCompletionsText = "";
    [ObservableProperty] private string _startButtonText = "";

    public BossRushViewModel(IBossRushService bossRushService, ILocalizationService localization)
    {
        _bossRushService = bossRushService;
        _localization = localization;
    }

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
    }

    public void UpdateLocalizedTexts()
    {
        TitleText = _localization.GetString("BossRushTitle") ?? "Weekly Boss Rush";
        DescText = _localization.GetString("BossRushDesc") ?? "Defeat all 5 bosses in a row.";
        var weeklyFmt = _localization.GetString("BossRushWeeklyBest") ?? "Weekly Best: {0}";
        WeeklyBestText = string.Format(weeklyFmt, _bossRushService.WeeklyBestScore.ToString("N0"));
        TotalCompletionsText = $"{_bossRushService.TotalCompletions}";
        StartButtonText = _localization.GetString("BossRushStartButton") ?? "Start Boss Rush";
    }

    [RelayCommand]
    private void Start()
    {
        // Mode "bossrush" + Boss-Index 0 als Floor-Parameter (GameEngine.StartBossRushModeAsync nutzt das)
        NavigationRequested?.Invoke(new GoGame(Mode: "bossrush", Level: 1, Floor: 0));
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(new GoBack());
    }
}
