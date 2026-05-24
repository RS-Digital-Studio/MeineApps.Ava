#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Friends;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Friends-State pro Spieler (Schema v2).
    /// </summary>
    [Serializable]
    public sealed class FriendsSaveSlice
    {
        public List<FriendEntry> Friends { get; } = new();
        public List<FriendRequest> IncomingRequests { get; } = new();
        public List<FriendRequest> OutgoingRequests { get; } = new();
        public HashSet<string> BlockedPlayerIds { get; } = new();
    }
}
