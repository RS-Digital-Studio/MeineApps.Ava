#nullable enable
using System;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Belohnungen, die der Spieler verdient hat aber noch nicht abgeholt hat
    /// (z.B. Level-Up-Packs/AvatarFrames/Saison-End-Belohnungen). Werden in
    /// PlayerSave-Schema v2 persistiert und im Hub als "Briefkasten" angezeigt.
    /// </summary>
    public enum PendingClaimKind
    {
        Pack = 0,
        AvatarFrame = 1,
        Title = 2,
        FeatureUnlock = 3,
        RuneSlotUnlock = 4,
        Card = 5,
        Currency = 6,
        Scrap = 7,
        /// <summary>Eine konkrete Rune (SubType = RuneDefinitionId) fuers RuneInventory.</summary>
        Rune = 8
    }

    [Serializable]
    public sealed class PendingClaim
    {
        public string Id { get; init; } = string.Empty;
        public PendingClaimKind Kind { get; init; }
        public string SubType { get; init; } = string.Empty;   // z.B. "common_pack", "Gold", "epic_scrap"
        public long Amount { get; init; }
        public string? SourceKey { get; init; }                 // z.B. "level_up_20", "season_5_meister"
        public DateTime CreatedAtUtc { get; init; }
    }
}
