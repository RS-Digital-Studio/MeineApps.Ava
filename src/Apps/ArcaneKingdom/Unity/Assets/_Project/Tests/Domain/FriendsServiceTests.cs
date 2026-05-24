#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Friends;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Friends;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class FriendsServiceTests
    {
        private sealed class InMemorySaveService : ISaveService<PlayerSave>
        {
            public PlayerSave Save { get; }
            public InMemorySaveService()
            {
                Save = new PlayerSave(new PlayerProfile("u-me", "Tester", "Poseidon", DateTime.UtcNow));
            }
            public UniTask<Result<PlayerSave>> LoadAsync(CancellationToken ct = default) =>
                UniTask.FromResult(Result<PlayerSave>.Success(Save));
            public UniTask<Result> SaveAsync(PlayerSave save, CancellationToken ct = default) =>
                UniTask.FromResult(Result.Success());
            public UniTask<Result<PlayerSave>> MutateAsync(Func<PlayerSave, PlayerSave> mutation, CancellationToken ct = default)
            {
                var mutated = mutation(Save);
                return UniTask.FromResult(Result<PlayerSave>.Success(mutated));
            }
        }

        private sealed class StubAnalytics : IAnalyticsService
        {
            public List<string> Events { get; } = new();
            public void Track(string eventName, IReadOnlyDictionary<string, object>? properties = null) => Events.Add(eventName);
            public void SetUserProperty(string key, string value) { }
            public void SetUserId(string userId) { }
        }

        [Test]
        public async Task SendRequestErzeugtOutgoing()
        {
            var save = new InMemorySaveService();
            var svc = new FriendsService(save, new StubAnalytics());
            var result = await svc.SendRequestAsync("u-other", "Hi", default);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, save.Save.FriendsSlice.OutgoingRequests.Count);
            Assert.AreEqual("u-other", save.Save.FriendsSlice.OutgoingRequests[0].ToPlayerId);
        }

        [Test]
        public async Task DoppelteAnfrageWirdAbgewiesen()
        {
            var save = new InMemorySaveService();
            var svc = new FriendsService(save, new StubAnalytics());
            await svc.SendRequestAsync("u-other", null, default);
            var second = await svc.SendRequestAsync("u-other", null, default);
            Assert.IsFalse(second.IsSuccess);
        }

        [Test]
        public async Task BlockedKannKeineAnfrageBekommen()
        {
            var save = new InMemorySaveService();
            save.Save.FriendsSlice.BlockedPlayerIds.Add("u-bad");
            var svc = new FriendsService(save, new StubAnalytics());
            var result = await svc.SendRequestAsync("u-bad", null, default);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public async Task AcceptVerschiebtInFriends()
        {
            var save = new InMemorySaveService();
            save.Save.FriendsSlice.IncomingRequests.Add(new FriendRequest
            {
                FromPlayerId = "u-other",
                FromDisplayName = "Other",
                ToPlayerId = save.Save.Profile.UserId,
                SentAtUtc = DateTime.UtcNow,
                State = FriendRequestState.Pending
            });
            var svc = new FriendsService(save, new StubAnalytics());
            var result = await svc.AcceptRequestAsync("u-other", default);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, save.Save.FriendsSlice.Friends.Count);
            Assert.AreEqual(FriendRequestState.Accepted, save.Save.FriendsSlice.IncomingRequests[0].State);
        }

        [Test]
        public async Task RemoveFriendLoeschtEintrag()
        {
            var save = new InMemorySaveService();
            save.Save.FriendsSlice.Friends.Add(new FriendEntry { PlayerId = "u-other", DisplayName = "Other" });
            var svc = new FriendsService(save, new StubAnalytics());
            await svc.RemoveFriendAsync("u-other", default);
            Assert.AreEqual(0, save.Save.FriendsSlice.Friends.Count);
        }

        [Test]
        public async Task BlockEntferntFreundschaftUndAnfragen()
        {
            var save = new InMemorySaveService();
            save.Save.FriendsSlice.Friends.Add(new FriendEntry { PlayerId = "u-bad" });
            save.Save.FriendsSlice.IncomingRequests.Add(new FriendRequest { FromPlayerId = "u-bad" });
            var svc = new FriendsService(save, new StubAnalytics());
            await svc.BlockAsync("u-bad", default);
            Assert.AreEqual(0, save.Save.FriendsSlice.Friends.Count);
            Assert.AreEqual(0, save.Save.FriendsSlice.IncomingRequests.Count);
            Assert.IsTrue(save.Save.FriendsSlice.BlockedPlayerIds.Contains("u-bad"));
        }
    }
}
