using System.Globalization;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// In-App-Review Timing-Service.
/// Fordert Review an nach Level 3-5 (erstes Erfolgserlebnis),
/// mit 14-Tage-Cooldown zwischen Anfragen.
/// </summary>
public class ReviewService : IReviewService
{
    private const string REVIEW_PROMPTED_DATE_KEY = "ReviewPromptedDate";
    private const string REVIEW_LEVEL_KEY = "ReviewLevelCompleted";
    private const int MIN_LEVEL_FOR_REVIEW = 3;
    private const int MAX_LEVEL_FOR_FIRST_REVIEW = 5;
    private const int COOLDOWN_DAYS = 14;

    private readonly IPreferencesService _preferences;
    private bool _shouldPrompt;

    public ReviewService(IPreferencesService preferences)
    {
        _preferences = preferences;
    }

    public bool ShouldPromptReview()
    {
        return _shouldPrompt;
    }

    public void MarkReviewPrompted()
    {
        _shouldPrompt = false;
        _preferences.Set(REVIEW_PROMPTED_DATE_KEY, DateTime.UtcNow.ToString("O"));
    }

    public void OnLevelCompleted(int level)
    {
        // Level-Completion Counter speichern
        int maxCompleted = _preferences.Get(REVIEW_LEVEL_KEY, 0);
        if (level > maxCompleted)
        {
            _preferences.Set(REVIEW_LEVEL_KEY, level);
        }

        // Prüfen ob Review-Anfrage angezeigt werden soll
        if (level < MIN_LEVEL_FOR_REVIEW)
            return;

        // Cooldown prüfen
        var lastPrompted = _preferences.Get<string>(REVIEW_PROMPTED_DATE_KEY, "");
        if (!string.IsNullOrEmpty(lastPrompted))
        {
            try
            {
                var lastDate = DateTime.Parse(lastPrompted, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if ((DateTime.UtcNow - lastDate).TotalDays < COOLDOWN_DAYS)
                    return; // Cooldown noch aktiv
            }
            catch
            {
                // Parse-Fehler → als nie angefragt behandeln
            }
        }

        // Erstes Erfolgserlebnis (Level 3-5) oder alle 14 Tage nach Level-Complete
        if (level <= MAX_LEVEL_FOR_FIRST_REVIEW || string.IsNullOrEmpty(lastPrompted))
        {
            _shouldPrompt = true;
        }
        else
        {
            // Nach Cooldown-Ablauf bei jedem Level-Complete erneut fragen
            _shouldPrompt = true;
        }
    }
}
