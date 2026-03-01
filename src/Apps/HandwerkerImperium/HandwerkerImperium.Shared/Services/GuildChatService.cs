using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Einfacher Gilden-Chat via Firebase Realtime Database.
/// Letzte 50 Nachrichten, Polling bei Tab-Wechsel.
/// Spam-Schutz: 5 Sekunden Cooldown, max 200 Zeichen.
/// </summary>
public class GuildChatService : IGuildChatService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private DateTime _lastMessageSent = DateTime.MinValue;
    private static readonly TimeSpan MessageCooldown = TimeSpan.FromSeconds(5);
    private const int MaxMessageLength = 200;
    private const int MaxMessages = 50;

    public GuildChatService(IFirebaseService firebase, IGameStateService gameStateService)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
    }

    public bool CanSendMessage => DateTime.UtcNow - _lastMessageSent >= MessageCooldown;

    public async Task<bool> SendMessageAsync(string guildId, string text)
    {
        try
        {
            if (!CanSendMessage) return false;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return false;

            // Text bereinigen und kürzen
            text = text.Trim();
            if (text.Length > MaxMessageLength)
                text = text[..MaxMessageLength];

            var playerName = _gameStateService.State.PlayerName ?? "Handwerker";

            var message = new ChatMessage
            {
                Uid = uid,
                Name = playerName,
                Text = text,
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            var key = await _firebase.PushAsync($"guild_chat/{guildId}/messages", message);
            if (string.IsNullOrEmpty(key)) return false;

            _lastMessageSent = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in SendMessageAsync: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ChatMessageDisplay>> GetRecentMessagesAsync(string guildId)
    {
        var result = new List<ChatMessageDisplay>();

        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid)) return result;

            // Letzte 50 Nachrichten laden (orderBy + limitToLast)
            var json = await _firebase.QueryAsync(
                $"guild_chat/{guildId}/messages",
                $"orderBy=\"timestamp\"&limitToLast={MaxMessages}");

            if (string.IsNullOrEmpty(json) || json == "null") return result;

            var messages = JsonSerializer.Deserialize<Dictionary<string, ChatMessage>>(json, JsonOptions);
            if (messages == null) return result;

            foreach (var (_, msg) in messages.OrderBy(m =>
                DateTime.TryParse(m.Value.Timestamp, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.MinValue))
            {
                result.Add(new ChatMessageDisplay
                {
                    Uid = msg.Uid,
                    Name = msg.Name,
                    Text = msg.Text,
                    Timestamp = msg.Timestamp,
                    IsOwnMessage = msg.Uid == uid
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in GetRecentMessagesAsync: {ex.Message}");
        }

        return result;
    }
}
