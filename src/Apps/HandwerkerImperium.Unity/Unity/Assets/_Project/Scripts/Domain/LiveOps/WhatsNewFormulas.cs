#nullable enable

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// What's-New-Dialog (P3 §2/§3): zeigt die Neuerungen genau einmal je Version. Der zuletzt gesehene
    /// Versions-Code wird VOR dem Render gespeichert; angezeigt wird nur, wenn die aktuelle App-Version neuer ist.
    /// Reine, Unity-freie Logik.
    /// </summary>
    public static class WhatsNewFormulas
    {
        /// <summary>True, wenn der What's-New-Dialog für die aktuelle Version noch nicht gesehen wurde.</summary>
        public static bool ShouldShow(int currentVersionCode, int lastSeenVersionCode) =>
            currentVersionCode > lastSeenVersionCode;
    }
}
