#nullable enable
namespace ArcaneKingdom.UI.Modals
{
    /// <summary>
    /// Zwischen-Cache fuer die Parameter des MemoryFragmentModal (Designplan v4 Story Kap. 9).
    /// Da Screens in VContainer als Transient registriert sind, koennen Caller nicht direkt
    /// Properties auf der Modal-Instance setzen — stattdessen schreiben sie in diesen Singleton-Context,
    /// und das Modal liest die Werte beim OnEnter aus.
    /// </summary>
    public sealed class MemoryFragmentContext
    {
        public string? FragmentId { get; set; }
        public string? TitleKey { get; set; }
        public string? ContentKey { get; set; }
        public string? TwistRevealKey { get; set; }
        public bool IsMajorTwist { get; set; }

        /// <summary>Optional: NPC-ID fuer das Portrait (z.B. "lumis", "lilith"). Wird vom Modal als Sprite geladen.</summary>
        public string? NpcId { get; set; }

        public void Reset()
        {
            FragmentId = null;
            TitleKey = null;
            ContentKey = null;
            TwistRevealKey = null;
            IsMajorTwist = false;
            NpcId = null;
        }
    }

    /// <summary>
    /// Analog fuer PrestigeUpgradeModal — Welt-ID muss vor dem Push gesetzt werden.
    /// </summary>
    public sealed class PrestigeUpgradeContext
    {
        public string? TargetWorldId { get; set; }
        public void Reset() => TargetWorldId = null;
    }
}
