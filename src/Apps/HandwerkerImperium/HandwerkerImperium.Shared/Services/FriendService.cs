using System.Text.Json;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet simulierte Freunde (lokal) + echte Firebase-Freunde.
/// Simulierte Freunde: 5 NPCs mit täglichen Goldschrauben-Geschenken.
/// Echte Freunde: Firebase-basiert über Gildenmitgliederliste, max 50.
/// </summary>
public class FriendService : IFriendService
{
    private readonly IGameStateService _gameState;
    private readonly IFirebaseService _firebase;
    private readonly ISaveGameService _saveGame;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int MaxRealFriends = 50;

    public event Action? FriendsUpdated;

    public FriendService(IGameStateService gameState, IFirebaseService firebase, ISaveGameService saveGame)
    {
        _gameState = gameState;
        _firebase = firebase;
        _saveGame = saveGame;
    }

    // --- Simulierte Freunde (offline, lokal) ---

    public void Initialize()
    {
        var state = _gameState.State;
        if (state.Friends.Count == 0)
        {
            state.Friends = Friend.CreateSimulatedFriends();
            _gameState.MarkDirty();
        }
    }

    public void GenerateDailyGifts()
    {
        var state = _gameState.State;
        if (state.Friends.Count == 0) return;

        bool changed = false;
        foreach (var friend in state.Friends)
        {
            if (friend.HasGiftAvailable)
                changed = true;
        }

        if (changed)
        {
            _gameState.MarkDirty();
            FriendsUpdated?.Invoke();
        }
    }

    public void ClaimGift(string friendId)
    {
        var state = _gameState.State;
        var friend = state.Friends.FirstOrDefault(f => f.Id == friendId);
        if (friend == null || !friend.HasGiftAvailable) return;

        _gameState.AddGoldenScrews(friend.GiftAmount);
        friend.LastGiftSent = DateTime.UtcNow;

        _gameState.MarkDirty();
        FriendsUpdated?.Invoke();
    }

    public void SendGift(string friendId)
    {
        var state = _gameState.State;
        var friend = state.Friends.FirstOrDefault(f => f.Id == friendId);
        if (friend == null || friend.HasSentGiftToday) return;

        if (!_gameState.TrySpendGoldenScrews(1)) return;

        friend.LastGiftReceived = DateTime.UtcNow;
        if (friend.FriendshipLevel < 5)
            friend.FriendshipLevel++;

        _gameState.MarkDirty();
        FriendsUpdated?.Invoke();
    }

    // --- Echte Firebase-Freunde ---

    public async Task SendFriendRequestAsync(string targetUid, string targetName)
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid) || uid == targetUid) return;

            // Prüfe ob schon Freund
            var existing = await _firebase.GetAsync<FirebaseFriend>($"friends/{uid}/{targetUid}");
            if (existing != null) return;

            // Spielername für die Anfrage
            var playerName = _gameState.State.PlayerName ?? "Handwerker";

            var request = new FriendRequest
            {
                Name = playerName,
                Level = _gameState.State.PlayerLevel,
                SentAt = DateTime.UtcNow.ToString("O")
            };

            await _firebase.SetAsync($"friend_requests/{targetUid}/{uid}", request);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in SendFriendRequestAsync: {ex.Message}");
        }
    }

    public async Task<List<FriendRequestDisplay>> GetPendingRequestsAsync()
    {
        var result = new List<FriendRequestDisplay>();

        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return result;

            var json = await _firebase.QueryAsync($"friend_requests/{uid}", "");
            if (string.IsNullOrEmpty(json) || json == "null") return result;

            var requests = JsonSerializer.Deserialize<Dictionary<string, FriendRequest>>(json, JsonOptions);
            if (requests == null) return result;

            foreach (var (fromUid, req) in requests)
            {
                result.Add(new FriendRequestDisplay
                {
                    Uid = fromUid,
                    Name = req.Name,
                    Level = req.Level,
                    SentAt = req.SentAt
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in GetPendingRequestsAsync: {ex.Message}");
        }

        return result;
    }

    public async Task AcceptRequestAsync(string fromUid)
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            // Anfrage lesen für Name/Level
            var request = await _firebase.GetAsync<FriendRequest>($"friend_requests/{uid}/{fromUid}");
            if (request == null) return;

            // Max-Freunde prüfen
            var myFriends = await GetRealFriendsAsync();
            if (myFriends.Count >= MaxRealFriends) return;

            var now = DateTime.UtcNow.ToString("O");
            var playerName = _gameState.State.PlayerName ?? "Handwerker";

            // Beidseitige Freundschaft erstellen (3 Writes)
            var myEntry = new FirebaseFriend
            {
                Name = request.Name,
                Level = request.Level,
                AddedAt = now
            };
            var theirEntry = new FirebaseFriend
            {
                Name = playerName,
                Level = _gameState.State.PlayerLevel,
                AddedAt = now
            };

            await _firebase.SetAsync($"friends/{uid}/{fromUid}", myEntry);
            await _firebase.SetAsync($"friends/{fromUid}/{uid}", theirEntry);
            await _firebase.DeleteAsync($"friend_requests/{uid}/{fromUid}");

            FriendsUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in AcceptRequestAsync: {ex.Message}");
        }
    }

    public async Task DeclineRequestAsync(string fromUid)
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            await _firebase.DeleteAsync($"friend_requests/{uid}/{fromUid}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in DeclineRequestAsync: {ex.Message}");
        }
    }

    public async Task<List<FriendDisplay>> GetRealFriendsAsync()
    {
        var result = new List<FriendDisplay>();

        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return result;

            var json = await _firebase.QueryAsync($"friends/{uid}", "");
            if (string.IsNullOrEmpty(json) || json == "null") return result;

            var friends = JsonSerializer.Deserialize<Dictionary<string, FirebaseFriend>>(json, JsonOptions);
            if (friends == null) return result;

            foreach (var (friendUid, friend) in friends)
            {
                result.Add(new FriendDisplay
                {
                    Uid = friendUid,
                    Name = friend.Name,
                    Level = friend.Level,
                    AddedAt = friend.AddedAt
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in GetRealFriendsAsync: {ex.Message}");
        }

        return result;
    }

    public async Task RemoveFriendAsync(string friendUid)
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            // Beidseitig entfernen
            await _firebase.DeleteAsync($"friends/{uid}/{friendUid}");
            await _firebase.DeleteAsync($"friends/{friendUid}/{uid}");

            FriendsUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in RemoveFriendAsync: {ex.Message}");
        }
    }
}
