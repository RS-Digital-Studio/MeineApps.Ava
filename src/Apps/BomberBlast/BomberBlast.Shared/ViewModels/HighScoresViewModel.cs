using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel for the high scores page.
/// Zeigt die Top 10 Bestenlisten-Scores.
/// </summary>
public partial class HighScoresViewModel : ObservableObject
{
    private readonly IHighScoreService _highScoreService;
    private readonly IPurchaseService _purchaseService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event to request navigation. Parameter is the route string.
    /// </summary>
    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<ScoreDisplayItem> _scores = [];

    [ObservableProperty]
    private bool _hasScores;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public HighScoresViewModel(IHighScoreService highScoreService, IPurchaseService purchaseService)
    {
        _highScoreService = highScoreService;
        _purchaseService = purchaseService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when the view appears. Loads the score list.
    /// </summary>
    public void OnAppearing()
    {
        LoadScores();
    }

    private void LoadScores()
    {
        Scores.Clear();

        var entries = _highScoreService.GetTopScores(10);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            Scores.Add(new ScoreDisplayItem
            {
                Rank = $"#{i + 1}",
                RankIndex = i,
                Name = entry.PlayerName,
                ScoreText = entry.Score.ToString("N0"),
                WaveText = $"Wave {entry.Level} - {entry.Date:MMM dd}",
                Score = entry.Score
            });
        }

        HasScores = Scores.Count > 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }
}

/// <summary>
/// Display model for a single score entry in the high scores list.
/// </summary>
public class ScoreDisplayItem
{
    public string Rank { get; set; } = "";
    public int RankIndex { get; set; }
    public string Name { get; set; } = "";
    public string ScoreText { get; set; } = "";
    public string WaveText { get; set; } = "";
    public int Score { get; set; }

    /// <summary>Brush color for the rank display based on position.</summary>
    public IBrush RankBrush => RankIndex switch
    {
        0 => Brushes.Gold,
        1 => Brushes.Silver,
        2 => new SolidColorBrush(Color.Parse("#CD7F32")), // Bronze
        _ => Brushes.White
    };
}
