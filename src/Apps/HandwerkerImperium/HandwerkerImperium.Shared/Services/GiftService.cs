using System.Text.Json;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Firebase-basiertes Geschenke-System.
/// Täglich 1 Geschenk an einen echten Freund: 3 Goldschrauben.
/// Geschenke verfallen nach 7 Tagen.
/// </summary>
public class GiftService : IGiftService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly ISaveGameService _saveGameService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int GiftAmount = 3;
    private const int GiftExpiryDays = 7;

    public GiftService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        ISaveGameService saveGameService)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _saveGameService = saveGameService;
    }

    public bool HasSentGiftToday =>
        _gameStateService.State.LastGiftSentDate.Date >= DateTime.UtcNow.Date;

    public async Task<bool> SendGiftAsync(string friendUid, string friendName)
    {
        try
        {
            if (HasSentGiftToday) return false;

            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return false;

            var playerName = _gameStateService.State.PlayerName ?? "Handwerker";

            var gift = new FirebaseGift
            {
                FromUid = uid,
                FromName = playerName,
                Type = "golden_screws",
                Amount = GiftAmount,
                SentAt = DateTime.UtcNow.ToString("O"),
                Claimed = false
            };

            var key = await _firebase.PushAsync($"gifts/{friendUid}", gift);
            if (string.IsNullOrEmpty(key)) return false;

            // Letztes Sendedatum speichern
            _gameStateService.State.LastGiftSentDate = DateTime.UtcNow;
            await _saveGameService.SaveAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<GiftDisplay>> GetPendingGiftsAsync()
    {
        var result = new List<GiftDisplay>();

        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return result;

            var json = await _firebase.QueryAsync($"gifts/{uid}", "");
            if (string.IsNullOrEmpty(json) || json == "null") return result;

            var gifts = JsonSerializer.Deserialize<Dictionary<string, FirebaseGift>>(json, JsonOptions);
            if (gifts == null) return result;

            var now = DateTime.UtcNow;

            foreach (var (giftId, gift) in gifts)
            {
                // Abgelaufene + eingelöste Geschenke aufräumen
                if (gift.Claimed)
                {
                    if (DateTime.TryParse(gift.SentAt, out var sentDate) &&
                        (now - sentDate).TotalDays > GiftExpiryDays)
                    {
                        _ = _firebase.DeleteAsync($"gifts/{uid}/{giftId}");
                    }
                    continue;
                }

                // Abgelaufene uneingelöste Geschenke entfernen
                if (DateTime.TryParse(gift.SentAt, out var sent) &&
                    (now - sent).TotalDays > GiftExpiryDays)
                {
                    _ = _firebase.DeleteAsync($"gifts/{uid}/{giftId}");
                    continue;
                }

                result.Add(new GiftDisplay
                {
                    GiftId = giftId,
                    FromName = gift.FromName,
                    Type = gift.Type,
                    Amount = gift.Amount,
                    SentAt = gift.SentAt,
                    Claimed = gift.Claimed
                });
            }
        }
        catch
        {
            // Fehler ignorieren
        }

        return result;
    }

    public async Task<bool> ClaimGiftAsync(string giftId)
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return false;

            // Geschenk laden
            var gift = await _firebase.GetAsync<FirebaseGift>($"gifts/{uid}/{giftId}");
            if (gift == null || gift.Claimed) return false;

            // Goldschrauben gutschreiben
            _gameStateService.AddGoldenScrews(gift.Amount);

            // Als eingelöst markieren
            await _firebase.UpdateAsync($"gifts/{uid}/{giftId}",
                new Dictionary<string, object> { ["claimed"] = true });

            await _saveGameService.SaveAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> ClaimAllGiftsAsync()
    {
        var gifts = await GetPendingGiftsAsync();
        int claimed = 0;

        foreach (var gift in gifts.Where(g => !g.Claimed))
        {
            if (await ClaimGiftAsync(gift.GiftId))
                claimed++;
        }

        return claimed;
    }
}
