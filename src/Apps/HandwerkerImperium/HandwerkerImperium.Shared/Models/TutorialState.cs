using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Tutorial- und Hint-Tracking-State.
/// Extrahiert aus GameState (V4) fuer bessere Strukturierung.
/// </summary>
public sealed class TutorialState
{
    /// <summary>
    /// Abwaertskompatibilitaet: Altes Tutorial abgeschlossen.
    /// </summary>
    [JsonPropertyName("tutorialCompleted")]
    public bool TutorialCompleted { get; set; }

    [JsonPropertyName("tutorialStep")]
    public int TutorialStep { get; set; }

    /// <summary>
    /// IDs der bereits gesehenen kontextuellen Hints.
    /// </summary>
    [JsonPropertyName("seenHints")]
    public HashSet<string> SeenHints { get; set; } = [];

    /// <summary>
    /// MiniGame-Typen, fuer die das Tutorial bereits angezeigt wurde.
    /// </summary>
    [JsonPropertyName("seenMiniGameTutorials")]
    public List<MiniGameType> SeenMiniGameTutorials { get; set; } = [];

    /// <summary>
    /// Legacy: Altes lineares Tutorial-System. Beibehalten fuer JSON-Kompatibilitaet.
    /// </summary>
    [JsonPropertyName("hasSeenTutorialHint")]
    public bool HasSeenTutorialHint { get; set; }
}
