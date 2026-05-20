using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IFeatureUnlockChoreographer"/> (.4 .
///
/// <para>
/// Verwaltet eine interne Queue von Feature-Unlocks — feuert <see cref="FeatureUnlocked"/>
/// einmal pro Sekunde wenn die View "DismissCurrent" aufgerufen hat. So koennen mehrere
/// gleichzeitige Unlocks (z.B. Level + Achievement gleichzeitig) sequentiell angezeigt werden.
/// </para>
///
/// <para>
/// Idempotent: Jedes Feature wird nur einmal pro Lebenszeit gezeigt (Pref-Flag
/// <c>FeatureUnlock_{FeatureId}_Shown</c>).
/// </para>
/// </summary>
public sealed class FeatureUnlockChoreographer : IFeatureUnlockChoreographer
{
    private const string PrefKeyPrefix = "FeatureUnlock_";

    private readonly IPreferencesService _prefs;
    private readonly IAnalyticsService _analytics;
    private readonly Queue<FeatureUnlockEvent> _queue = new();
    private bool _isShowing;
    private readonly object _lock = new();

    public event Action<FeatureUnlockEvent>? FeatureUnlocked;

    public FeatureUnlockChoreographer(IPreferencesService prefs, IAnalyticsService analytics)
    {
        _prefs = prefs;
        _analytics = analytics;
    }

    public void OnLevelComplete(int completedLevel)
    {
        // Schwellen-basierte Feature-Unlocks. Map: Level → Feature(s).
        switch (completedLevel)
        {
            case 10:
                Enqueue("daily_challenge", "FeatureUnlockDailyChallengeTitle",
                    "FeatureUnlockDailyChallengeDesc", ctaNav: "DailyChallenge");
                break;
            case 20:
                Enqueue("dungeon_mode", "FeatureUnlockDungeonTitle",
                    "FeatureUnlockDungeonDesc", ctaNav: "Dungeon");
                break;
            case 30:
                Enqueue("line_bomb", "FeatureUnlockLineBombTitle",
                    "FeatureUnlockLineBombDesc");
                break;
            case 40:
                Enqueue("power_bomb", "FeatureUnlockPowerBombTitle",
                    "FeatureUnlockPowerBombDesc");
                break;
            case 50:
                Enqueue("boss_rush", "FeatureUnlockBossRushTitle",
                    "FeatureUnlockBossRushDesc", ctaNav: "BossRush");
                break;
            case 100:
                Enqueue("master_mode", "FeatureUnlockMasterModeTitle",
                    "FeatureUnlockMasterModeDesc");
                break;
        }
        TryShowNext();
    }

    public void OnAchievementUnlocked(string achievementId)
    {
        // Spezielle Achievements triggern Cosmetic-Unlocks.
        switch (achievementId)
        {
            case "ach_master_100":
                Enqueue("champion_skin", "FeatureUnlockChampionSkinTitle",
                    "FeatureUnlockChampionSkinDesc");
                TryShowNext();
                break;
        }
    }

    public void DismissCurrent()
    {
        lock (_lock)
        {
            _isShowing = false;
            _showTimeoutTimer?.Dispose();
            _showTimeoutTimer = null;
        }
        TryShowNext();
    }

    // v2.0.60 (B-E10): Timer für Auto-Dismiss falls View DismissCurrent nicht aufruft
    // (z.B. Crash/Navigation weg). Verhindert dass _isShowing für die ganze Session hängt
    // und Queue-Items nie angezeigt werden.
    private const int SHOW_TIMEOUT_MS = 10_000;
    private System.Threading.Timer? _showTimeoutTimer;

    private void Enqueue(string featureId, string titleKey, string descKey,
        string? ctaNav = null, string? heroAsset = null)
    {
        var prefKey = PrefKeyPrefix + featureId;
        if (_prefs.Get(prefKey, false))
        {
            return;  // Schon gezeigt
        }
        // Sofort als gezeigt markieren — Re-Trigger durch Re-Spielen wird verhindert,
        // selbst wenn die Anzeige aus irgendeinem Grund versagt (App-Crash, View nicht montiert).
        _prefs.Set(prefKey, true);

        //.2 : Funnel-Event feature_unlocked
        _analytics?.LogEvent(AnalyticsEvents.FeatureUnlocked, new Dictionary<string, object>
        {
            [AnalyticsParams.FeatureId] = featureId,
        });

        lock (_lock)
        {
            _queue.Enqueue(new FeatureUnlockEvent
            {
                FeatureId = featureId,
                TitleKey = titleKey,
                DescriptionKey = descKey,
                HeroAssetPath = heroAsset,
                CtaNavTarget = ctaNav,
                CtaTextKey = ctaNav != null ? "FeatureUnlockExploreCta" : null,
            });
        }
    }

    private void TryShowNext()
    {
        FeatureUnlockEvent? next = null;
        lock (_lock)
        {
            if (_isShowing) return;
            if (_queue.Count == 0) return;
            next = _queue.Dequeue();
            _isShowing = true;
            // v2.0.60 (B-E10): Timeout-Fallback. Auto-Dismiss nach 10s falls View nicht reagiert.
            _showTimeoutTimer?.Dispose();
            _showTimeoutTimer = new System.Threading.Timer(_ => DismissCurrent(),
                null, SHOW_TIMEOUT_MS, System.Threading.Timeout.Infinite);
        }
        if (next != null)
        {
            // Auf UI-Thread feuern damit Subscriber sicher Avalonia-Properties setzen koennen.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => FeatureUnlocked?.Invoke(next));
        }
    }
}
