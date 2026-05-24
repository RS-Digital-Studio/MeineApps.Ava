#nullable enable
using System;

namespace ArcaneKingdom.Domain.Friends
{
    public enum FriendStatus
    {
        Offline = 0,
        Online = 1,
        InBattle = 2,
        InArena = 3
    }

    [Serializable]
    public sealed class FriendEntry
    {
        public string PlayerId { get; init; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? GuildTag { get; set; }
        public int Level { get; set; }
        public DateTime BefriendedAtUtc { get; init; }
        public DateTime LastSeenUtc { get; set; }
        public FriendStatus Status { get; set; }
    }

    public enum FriendRequestState
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Cancelled = 3
    }

    [Serializable]
    public sealed class FriendRequest
    {
        public string FromPlayerId { get; init; } = string.Empty;
        public string FromDisplayName { get; init; } = string.Empty;
        public string ToPlayerId { get; init; } = string.Empty;
        public string? Message { get; init; }
        public DateTime SentAtUtc { get; init; }
        public FriendRequestState State { get; set; }
    }
}
