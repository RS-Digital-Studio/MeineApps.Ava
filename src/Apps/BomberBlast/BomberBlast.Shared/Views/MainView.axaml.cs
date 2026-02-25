using Avalonia.Controls;
using Avalonia.Media;
using BomberBlast.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BomberBlast.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    // Benannte Border-Referenzen für CSS-Klassen-Toggle
    private Border? _mainMenuBorder;
    private Border? _gameBorder;
    private Border? _levelSelectBorder;
    private Border? _settingsBorder;
    private Border? _highScoresBorder;
    private Border? _gameOverBorder;
    private Border? _shopBorder;
    private Border? _victoryBorder;
    private Border? _statisticsBorder;
    private Border? _quickPlayBorder;
    private Border? _dungeonBorder;
    private Border? _battlePassBorder;
    private Border? _leagueBorder;
    private Border? _profileBorder;
    private Border? _gemShopBorder;
    private Border? _cardsBorder;
    private Border? _challengesBorder;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Benannte Border-Referenzen suchen
        _mainMenuBorder = this.FindControl<Border>("MainMenuBorder");
        _gameBorder = this.FindControl<Border>("GameBorder");
        _levelSelectBorder = this.FindControl<Border>("LevelSelectBorder");
        _settingsBorder = this.FindControl<Border>("SettingsBorder");
        _highScoresBorder = this.FindControl<Border>("HighScoresBorder");
        _gameOverBorder = this.FindControl<Border>("GameOverBorder");
        _shopBorder = this.FindControl<Border>("ShopBorder");
        _victoryBorder = this.FindControl<Border>("VictoryBorder");
        _statisticsBorder = this.FindControl<Border>("StatisticsBorder");
        _quickPlayBorder = this.FindControl<Border>("QuickPlayBorder");
        _dungeonBorder = this.FindControl<Border>("DungeonBorder");
        _battlePassBorder = this.FindControl<Border>("BattlePassBorder");
        _leagueBorder = this.FindControl<Border>("LeagueBorder");
        _profileBorder = this.FindControl<Border>("ProfileBorder");
        _gemShopBorder = this.FindControl<Border>("GemShopBorder");
        _cardsBorder = this.FindControl<Border>("CardsBorder");
        _challengesBorder = this.FindControl<Border>("ChallengesBorder");

        // Initial: MainMenu aktiv setzen
        UpdateActiveClasses();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateActiveClasses();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Bei Änderung einer IsXxxActive Property → CSS-Klassen aktualisieren
        if (e.PropertyName != null && e.PropertyName.StartsWith("Is") && e.PropertyName.EndsWith("Active"))
        {
            UpdateActiveClasses();
        }
    }

    /// <summary>
    /// CSS-Klassen "Active" auf den PageView-Borders setzen/entfernen basierend auf ViewModel-State
    /// </summary>
    private void UpdateActiveClasses()
    {
        if (_vm == null) return;

        SetActiveClass(_mainMenuBorder, _vm.IsMainMenuActive);
        SetActiveClass(_gameBorder, _vm.IsGameActive);
        SetActiveClass(_levelSelectBorder, _vm.IsLevelSelectActive);
        SetActiveClass(_settingsBorder, _vm.IsSettingsActive);
        SetActiveClass(_highScoresBorder, _vm.IsHighScoresActive);
        SetActiveClass(_gameOverBorder, _vm.IsGameOverActive);
        SetActiveClass(_shopBorder, _vm.IsShopActive);
        SetActiveClass(_victoryBorder, _vm.IsVictoryActive);
        SetActiveClass(_statisticsBorder, _vm.IsStatisticsActive);
        SetActiveClass(_quickPlayBorder, _vm.IsQuickPlayActive);
        SetActiveClass(_dungeonBorder, _vm.IsDungeonActive);
        SetActiveClass(_battlePassBorder, _vm.IsBattlePassActive);
        SetActiveClass(_leagueBorder, _vm.IsLeagueActive);
        SetActiveClass(_profileBorder, _vm.IsProfileActive);
        SetActiveClass(_gemShopBorder, _vm.IsGemShopActive);
        SetActiveClass(_cardsBorder, _vm.IsCardsActive);
        SetActiveClass(_challengesBorder, _vm.IsChallengesActive);
    }

    private static void SetActiveClass(Border? border, bool isActive)
    {
        if (border == null) return;

        if (isActive)
        {
            if (!border.Classes.Contains("Active"))
                border.Classes.Add("Active");
        }
        else
        {
            border.Classes.Remove("Active");
        }
    }

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            "gold" => Color.Parse("#FFD700"),
            "error" => Color.Parse("#EF4444"),
            _ => Color.Parse("#3B82F6")
        };

        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;

        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), h * 0.35, color, 20);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Events abmelden bei Detach (verhindert Memory Leaks)
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm = null;
        }

        DataContextChanged -= OnDataContextChanged;
    }
}
