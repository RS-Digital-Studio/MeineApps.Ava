#nullable enable
using System;

namespace ArcaneKingdom.Domain.Player
{
    /// <summary>
    /// Spieler-Stammdaten, die selten geaendert werden (Name, Server, Level).
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public string UserId { get; }
        public string DisplayName { get; set; }
        public string Server { get; set; }
        public int Level { get; set; }
        public long ExpTotal { get; set; }
        public string? GuildId { get; set; }
        public string? AvatarKey { get; set; }
        public DateTime CreatedAtUtc { get; }
        public DateTime LastSeenAtUtc { get; set; }

        public PlayerProfile(string userId, string displayName, string server, DateTime createdAtUtc)
        {
            UserId = userId;
            DisplayName = displayName;
            Server = server;
            Level = 1;
            ExpTotal = 0;
            CreatedAtUtc = createdAtUtc;
            LastSeenAtUtc = createdAtUtc;
        }
    }
}
