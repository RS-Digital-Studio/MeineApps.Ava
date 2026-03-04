using System.Globalization;
using MeineApps.Core.Ava.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// In-App-Review Timing-Service.
/// Fordert Review an nach bestimmten Meilensteinen
/// mit 14-Tage-Cooldown zwischen Anfragen.
/// </summary>
public sealed class ReviewService : IReviewService
{
    private const string ReviewPromptedDateKey = "ReviewPromptedDate";
    private const int CooldownDays = 14;

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
        _preferences.Set(ReviewPromptedDateKey, DateTime.UtcNow.ToString("O"));
    }

    public void OnMilestone(string type, int value)
    {
        // Trigger-Meilensteine prüfen
        bool isTrigger = type switch
        {
            "level" => value is 20 or 50 or 100,
            "prestige" => value >= 1,
            "orders" => value >= 50,
            _ => false
        };

        if (!isTrigger) return;

        // Cooldown prüfen
        var lastPrompted = _preferences.Get<string>(ReviewPromptedDateKey, "");
        if (!string.IsNullOrEmpty(lastPrompted))
        {
            try
            {
                var lastDate = DateTime.Parse(lastPrompted, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if ((DateTime.UtcNow - lastDate).TotalDays < CooldownDays)
                    return; // Cooldown noch aktiv
            }
            catch
            {
                // Parse-Fehler -> als nie angefragt behandeln
            }
        }

        // Cooldown abgelaufen -> Review anfordern
        _shouldPrompt = true;
    }
}
