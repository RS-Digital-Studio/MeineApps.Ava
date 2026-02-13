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
    private Border? _helpBorder;
    private Border? _shopBorder;
    private Border? _achievementsBorder;

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
        _helpBorder = this.FindControl<Border>("HelpBorder");
        _shopBorder = this.FindControl<Border>("ShopBorder");
        _achievementsBorder = this.FindControl<Border>("AchievementsBorder");

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
        SetActiveClass(_helpBorder, _vm.IsHelpActive);
        SetActiveClass(_shopBorder, _vm.IsShopActive);
        SetActiveClass(_achievementsBorder, _vm.IsAchievementsActive);
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
}
