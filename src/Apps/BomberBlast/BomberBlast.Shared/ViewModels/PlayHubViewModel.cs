using System.Collections.ObjectModel;
using BomberBlast.Icons;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer den "Spielen"-Tab (.1 .
///
/// <para>
/// Konsolidiert alle Spielmodi (Story / Quick Play / Survival / Dungeon / Boss-Rush / Master)
/// als Karten-Liste hinter einem einzigen Bottom-Tab. Ersetzt die verstreuten Modus-Buttons
/// im MainMenuView durch eine fokussierte Auswahl.
/// </para>
///
/// <para>
/// Gesperrte Modi werden mit Schloss + Freischalt-Hinweis angezeigt — Tap zeigt einen
/// FloatingText statt zu navigieren. Unlock-Schwellen sind identisch zum
/// FeatureUnlockChoreographer (Quick/Survival L5, Dungeon L20, Boss-Rush L50, Master L100).
/// </para>
/// </summary>
public sealed partial class PlayHubViewModel : ViewModelBase, INavigable, IGameJuiceEmitter, ILocalizable
{
    private readonly IProgressService _progressService;
    private readonly IMasterModeService _masterModeService;
    private readonly ILocalizationService _localizationService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    // IGameJuiceEmitter-Vertrag — der Play-Hub feuert keine Celebration (reine Navigation).
#pragma warning disable CS0067
    public event Action? CelebrationRequested;
#pragma warning restore CS0067

    [ObservableProperty]
    private string _titleText = "";

    [ObservableProperty]
    private ObservableCollection<PlayModeCard> _modes = [];

    public PlayHubViewModel(
        IProgressService progressService,
        IMasterModeService masterModeService,
        ILocalizationService localizationService)
    {
        _progressService = progressService;
        _masterModeService = masterModeService;
        _localizationService = localizationService;
    }

    /// <summary>Baut die Modus-Liste mit aktuellen Unlock-States neu auf.</summary>
    public void OnAppearing()
    {
        UpdateLocalizedTexts();
    }

    public void UpdateLocalizedTexts()
    {
        TitleText = _localizationService.GetString("PlayHubTitle") ?? "Play";
        BuildModes();
    }

    private void BuildModes()
    {
        int level = _progressService.HighestCompletedLevel;
        string S(string key, string fallback) => _localizationService.GetString(key) ?? fallback;
        string LockHint(int reqLevel) => string.Format(
            S("PlayHubUnlockAtLevel", "Unlocks at level {0}"), reqLevel);

        Modes =
        [
            new PlayModeCard
            {
                Title = S("StoryMode", "Story"),
                Description = S("PlayHubStoryDesc", "100 levels across 10 worlds"),
                Icon = GameIconKind.MapMarker,
                AccentColor = "#FF6B35",
                Request = new GoLevelSelect(),
                IsUnlocked = true,
                ButtonSeed = 320,
            },
            new PlayModeCard
            {
                Title = S("QuickPlay", "Quick Play"),
                Description = S("PlayHubQuickDesc", "Jump straight into a random level"),
                Icon = GameIconKind.Lightning,
                AccentColor = "#22D3EE",
                Request = new GoQuickPlay(),
                IsUnlocked = level >= 5,
                UnlockHint = LockHint(5),
                ButtonSeed = 321,
            },
            new PlayModeCard
            {
                Title = S("SurvivalMode", "Survival"),
                Description = S("PlayHubSurvivalDesc", "Endless waves — how long can you last?"),
                Icon = GameIconKind.Skull,
                AccentColor = "#A855F7",
                Request = new GoGame(Mode: "survival"),
                IsUnlocked = level >= 5,
                UnlockHint = LockHint(5),
                ButtonSeed = 322,
            },
            new PlayModeCard
            {
                Title = S("DungeonButton", "Dungeon"),
                Description = S("PlayHubDungeonDesc", "Roguelike run with buffs and synergies"),
                Icon = GameIconKind.TreasureChest,
                AccentColor = "#FFD700",
                Request = new GoDungeon(),
                IsUnlocked = level >= 20,
                UnlockHint = LockHint(20),
                ButtonSeed = 323,
            },
            new PlayModeCard
            {
                Title = S("BossRushTitle", "Boss Rush"),
                Description = S("PlayHubBossRushDesc", "Fight all 5 bosses back to back"),
                Icon = GameIconKind.Crown,
                AccentColor = "#EF4444",
                Request = new GoBossRush(),
                IsUnlocked = level >= 50,
                UnlockHint = LockHint(50),
                ButtonSeed = 324,
            },
            new PlayModeCard
            {
                Title = S("MasterModeTitle", "Master Mode"),
                Description = S("PlayHubMasterDesc", "The 100-level campaign — harder, faster enemies"),
                Icon = GameIconKind.Star,
                AccentColor = "#F59E0B",
                // Master-Mode wird im LevelSelect getoggelt — dorthin navigieren.
                Request = new GoLevelSelect(),
                IsUnlocked = _masterModeService.IsUnlocked,
                UnlockHint = S("PlayHubMasterLockHint", "Complete level 100 to unlock"),
                ButtonSeed = 325,
            },
        ];
    }

    [RelayCommand]
    private void SelectMode(PlayModeCard? card)
    {
        if (card == null) return;

        if (!card.IsUnlocked)
        {
            FloatingTextRequested?.Invoke(card.UnlockHint, "error");
            return;
        }

        NavigationRequested?.Invoke(card.Request);
    }

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke(new GoBack());
}

/// <summary>Datenmodell fuer eine Spielmodus-Karte im Play-Hub.</summary>
public sealed class PlayModeCard
{
    /// <summary>Lokalisierter Modus-Name.</summary>
    public string Title { get; init; } = "";

    /// <summary>Lokalisierte Kurzbeschreibung.</summary>
    public string Description { get; init; } = "";

    /// <summary>Neon-Arcade-Icon fuer den Modus.</summary>
    public GameIconKind Icon { get; init; }

    /// <summary>Hex-Akzentfarbe der Karte (z.B. "#FF6B35").</summary>
    public string AccentColor { get; init; } = "#FFFFFF";

    /// <summary>Navigations-Ziel beim Antippen (wenn freigeschaltet).</summary>
    public NavigationRequest Request { get; init; } = new GoMainMenu();

    /// <summary>Ob der Modus freigeschaltet ist.</summary>
    public bool IsUnlocked { get; init; }

    /// <summary>Lokalisierter Freischalt-Hinweis (bei IsUnlocked=false als FloatingText gezeigt).</summary>
    public string UnlockHint { get; init; } = "";

    /// <summary>Invertierter Lock-State fuer XAML-Binding (Schloss-Overlay sichtbar wenn locked).</summary>
    public bool IsLocked => !IsUnlocked;

    /// <summary>Einzigartiger Seed fuer GameButtonCanvas (prozedurale Textur).</summary>
    public int ButtonSeed { get; init; }
}
