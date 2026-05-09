using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// AAA-Audit P0 (DialogViewModel-Strukturschnitt): Alle Story-Dialog-Properties + -Methoden
/// fuer Meister Hans NPC. Wurde aus DialogViewModel.cs herausgezogen, um die Klasse von
/// 849 auf ~700 Zeilen zu reduzieren und die Single-Responsibility-Lesbarkeit zu erhoehen.
/// </summary>
public sealed partial class DialogViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // STORY-DIALOG PROPERTIES (Meister Hans NPC)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isStoryDialogVisible;

    [ObservableProperty]
    private string _storyTitle = "";

    [ObservableProperty]
    private string _storyText = "";

    [ObservableProperty]
    private string _storyMood = "happy";

    [ObservableProperty]
    private string _storyRewardText = "";

    [ObservableProperty]
    private string _storyChapterId = "";

    [ObservableProperty]
    private bool _hasNewStory;

    [ObservableProperty]
    private int _storyChapterNumber;

    [ObservableProperty]
    private int _storyTotalChapters = 25;

    [ObservableProperty]
    private bool _isStoryTutorial;

    [ObservableProperty]
    private string _storyChapterBadge = "";

    /// <summary>P2.2 AAA-Audit: Skip-Button nur bei Onboarding-Story (Ch.1) sichtbar.</summary>
    [ObservableProperty]
    private bool _canSkipStory;

    /// <summary>P2.2 AAA-Audit: Wird beim Skip gefeuert — MainViewModel kann
    /// analytics_event "onboarding_story_skipped" tracken.</summary>
    public event Action<string>? StorySkipRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // STORY-DIALOG COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissStoryDialog()
    {
        if (!string.IsNullOrEmpty(StoryChapterId))
        {
            _storyService?.MarkChapterViewed(StoryChapterId);
            HasNewStory = false;
        }
        IsStoryDialogVisible = false;
        DeferredDialogCheckRequested?.Invoke();

        // FloatingText fuer Belohnungen
        if (!string.IsNullOrEmpty(StoryRewardText))
        {
            FloatingTextRequested?.Invoke(StoryRewardText, "golden_screws");
        }
    }

    /// <summary>P2.2 AAA-Audit: Skip-Variante. Markiert Kapitel als gesehen, vergibt aber
    /// die Belohnung trotzdem (Spieler wird nicht bestraft fuer Skip-Wahl).</summary>
    [RelayCommand]
    private void SkipStory()
    {
        StorySkipRequested?.Invoke(StoryChapterId);
        DismissStoryDialog();
    }

    /// <summary>
    /// Prueft ob ein neues Story-Kapitel freigeschaltet wurde.
    /// Wird nach Level-Up, Workshop-Upgrade und Auftragsabschluss aufgerufen.
    /// </summary>
    public void CheckForNewStoryChapter(bool isAnyDialogVisible, bool isHoldingUpgrade)
    {
        var chapter = _storyService?.CheckForNewChapter();
        if (chapter != null)
        {
            HasNewStory = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (!isAnyDialogVisible && !IsAnyDialogVisible && !isHoldingUpgrade)
                {
                    ShowStoryDialog(chapter);
                }
            }, DispatcherPriority.Background);
        }
    }

    private void ShowStoryDialog(StoryChapter chapter)
    {
        var title = _localizationService.GetString(chapter.TitleKey);
        var text = _localizationService.GetString(chapter.TextKey);

        StoryTitle = string.IsNullOrEmpty(title) ? chapter.TitleFallback : title;
        StoryText = string.IsNullOrEmpty(text) ? chapter.TextFallback : text;
        StoryMood = chapter.Mood;
        StoryChapterId = chapter.Id;
        StoryChapterNumber = chapter.ChapterNumber;
        var (_, totalChapters) = _storyService?.GetProgress() ?? (0, 35);
        StoryTotalChapters = totalChapters;
        IsStoryTutorial = chapter.IsTutorial;
        StoryChapterBadge = chapter.IsTutorial
            ? _localizationService.GetString("StoryTipFromHans") ?? "Tip from Master Hans"
            : $"Kap. {chapter.ChapterNumber}/{totalChapters}";

        // P2.2 AAA-Audit: Skip nur bei Onboarding-Tutorials (Ch.1 + Welcome-Story).
        // Spaeter im Spiel ist Story Pacing-Element — kein Skip damit Spieler den Plot mitkriegt.
        CanSkipStory = chapter.IsTutorial || chapter.ChapterNumber <= 1;

        // Belohnungs-Text zusammenstellen (skalierte Geldbelohnung anzeigen)
        var rewards = new List<string>();
        if (chapter.MoneyReward > 0)
        {
            var netIncome = _gameStateService.State.NetIncomePerSecond;
            var scaledReward = Math.Max(chapter.MoneyReward, netIncome * 600);
            rewards.Add($"+{MoneyFormatter.FormatCompact(scaledReward)}");
        }
        if (chapter.GoldenScrewReward > 0)
        {
            var screwsLabel = _localizationService.GetString("GoldenScrews") ?? "Golden Screws";
            rewards.Add($"+{chapter.GoldenScrewReward} {screwsLabel}");
        }
        if (chapter.XpReward > 0)
            rewards.Add($"+{chapter.XpReward} XP");
        StoryRewardText = string.Join("  |  ", rewards);

        IsStoryDialogVisible = true;
    }
}
