#nullable enable
using System;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Friends;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Friends
{
    /// <summary>
    /// Friends-Verwaltung: Anfrage/Accept/Reject/Remove/Block.
    /// Lokal optimistisch, Server (Cloud Function) bestätigt + propagiert beim Empfänger.
    /// </summary>
    public sealed class FriendsService
    {
        private const int MaxFriends = 100;
        private const int MaxBlocked = 100;

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;

        public FriendsService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
        }

        public async UniTask<Result> SendRequestAsync(string toPlayerId, string? message, CancellationToken ct = default)
        {
            string error = string.Empty;
            await _save.MutateAsync(save =>
            {
                if (save.FriendsSlice.BlockedPlayerIds.Contains(toPlayerId)) { error = "Spieler ist blockiert."; return save; }
                if (save.FriendsSlice.Friends.Any(f => f.PlayerId == toPlayerId)) { error = "Bereits befreundet."; return save; }
                if (save.FriendsSlice.OutgoingRequests.Any(r => r.ToPlayerId == toPlayerId && r.State == FriendRequestState.Pending))
                { error = "Anfrage bereits gesendet."; return save; }

                save.FriendsSlice.OutgoingRequests.Add(new FriendRequest
                {
                    FromPlayerId = save.Profile.UserId,
                    FromDisplayName = save.Profile.DisplayName,
                    ToPlayerId = toPlayerId,
                    Message = message,
                    SentAtUtc = DateTime.UtcNow,
                    State = FriendRequestState.Pending
                });
                return save;
            }, ct);

            if (!string.IsNullOrEmpty(error)) return Result.Failure(error);
            _analytics.Track("friend_request_sent");
            return Result.Success();
        }

        public async UniTask<Result> AcceptRequestAsync(string fromPlayerId, CancellationToken ct = default)
        {
            string error = string.Empty;
            await _save.MutateAsync(save =>
            {
                var req = save.FriendsSlice.IncomingRequests.FirstOrDefault(r => r.FromPlayerId == fromPlayerId && r.State == FriendRequestState.Pending);
                if (req == null) { error = "Anfrage nicht gefunden."; return save; }
                if (save.FriendsSlice.Friends.Count >= MaxFriends) { error = $"Max {MaxFriends} Freunde erreicht."; return save; }

                req.State = FriendRequestState.Accepted;
                save.FriendsSlice.Friends.Add(new FriendEntry
                {
                    PlayerId = req.FromPlayerId,
                    DisplayName = req.FromDisplayName,
                    BefriendedAtUtc = DateTime.UtcNow,
                    LastSeenUtc = DateTime.UtcNow,
                    Status = FriendStatus.Online
                });
                return save;
            }, ct);

            if (!string.IsNullOrEmpty(error)) return Result.Failure(error);
            _analytics.Track("friend_request_accepted");
            return Result.Success();
        }

        public async UniTask<Result> RejectRequestAsync(string fromPlayerId, CancellationToken ct = default)
        {
            await _save.MutateAsync(save =>
            {
                var req = save.FriendsSlice.IncomingRequests.FirstOrDefault(r => r.FromPlayerId == fromPlayerId);
                if (req != null) req.State = FriendRequestState.Rejected;
                return save;
            }, ct);
            return Result.Success();
        }

        public async UniTask<Result> RemoveFriendAsync(string playerId, CancellationToken ct = default)
        {
            await _save.MutateAsync(save =>
            {
                save.FriendsSlice.Friends.RemoveAll(f => f.PlayerId == playerId);
                return save;
            }, ct);
            _analytics.Track("friend_removed");
            return Result.Success();
        }

        public async UniTask<Result> BlockAsync(string playerId, CancellationToken ct = default)
        {
            string error = string.Empty;
            await _save.MutateAsync(save =>
            {
                if (save.FriendsSlice.BlockedPlayerIds.Count >= MaxBlocked)
                { error = $"Max {MaxBlocked} blockierte Spieler erreicht."; return save; }
                save.FriendsSlice.BlockedPlayerIds.Add(playerId);
                save.FriendsSlice.Friends.RemoveAll(f => f.PlayerId == playerId);
                save.FriendsSlice.IncomingRequests.RemoveAll(r => r.FromPlayerId == playerId);
                return save;
            }, ct);
            if (!string.IsNullOrEmpty(error)) return Result.Failure(error);
            _analytics.Track("player_blocked");
            return Result.Success();
        }

        public async UniTask<Result> UnblockAsync(string playerId, CancellationToken ct = default)
        {
            await _save.MutateAsync(save => { save.FriendsSlice.BlockedPlayerIds.Remove(playerId); return save; }, ct);
            return Result.Success();
        }
    }
}
