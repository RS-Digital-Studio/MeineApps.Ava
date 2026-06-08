using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Domain.Onboarding
{
    /// <summary>
    /// Tutorial- und Hint-Tracking-State (persistierter Fortschritt). 1:1-Port aus dem Avalonia-Original
    /// (Models/TutorialState.cs). MiniGameType ist aus Schicht 4, FtueState aus Schicht 15. Die
    /// ContextualHint-Definitionen selbst liegen in der Präsentationsschicht — hier nur die gesehenen IDs.
    /// Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class TutorialState
    {
        /// <summary>Abwärtskompatibilität: Altes Tutorial abgeschlossen.</summary>
        [JsonProperty("tutorialCompleted")]
        public bool TutorialCompleted { get; set; }

        [JsonProperty("tutorialStep")]
        public int TutorialStep { get; set; }

        /// <summary>IDs der bereits gesehenen kontextuellen Hints.</summary>
        [JsonProperty("seenHints")]
        public HashSet<string> SeenHints { get; set; } = new HashSet<string>();

        /// <summary>MiniGame-Typen, für die das Tutorial bereits angezeigt wurde.</summary>
        [JsonProperty("seenMiniGameTutorials")]
        public List<MiniGameType> SeenMiniGameTutorials { get; set; } = new List<MiniGameType>();

        /// <summary>Legacy: Altes lineares Tutorial-System. Beibehalten für JSON-Kompatibilität.</summary>
        [JsonProperty("hasSeenTutorialHint")]
        public bool HasSeenTutorialHint { get; set; }

        /// <summary>Scripted FTUE-State (10-Schritt-Tutorial).</summary>
        [JsonProperty("ftue")]
        public FtueState Ftue { get; set; } = new FtueState();
    }
}
