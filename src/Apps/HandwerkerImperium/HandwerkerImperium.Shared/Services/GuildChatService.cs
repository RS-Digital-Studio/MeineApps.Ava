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
public sealed class GuildChatService : IGuildChatService, IDisposable
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _sendLock = new(1, 1);
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
        if (!await _sendLock.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false))
            return false; // Timeout: Lock nicht erhalten
        try
        {
            if (!CanSendMessage) return false;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var uid = _firebase.PlayerId;
            if (string.IsNullOrEmpty(uid)) return false;

            // Text bereinigen: Control-Characters entfernen (außer Newline), trimmen, kürzen
            text = new string(text.Where(c => !char.IsControl(c) || c == '\n').ToArray());
            text = text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return false;
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

            var key = await _firebase.PushAsync($"guild_chat/{guildId}/messages", message).ConfigureAwait(false);
            if (string.IsNullOrEmpty(key)) return false;

            _lastMessageSent = DateTime.UtcNow;
            return true;
        }
        catch
        {
            // Netzwerkfehler still behandelt
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<List<ChatMessageDisplay>> GetRecentMessagesAsync(string guildId)
    {
        var result = new List<ChatMessageDisplay>();

        try
        {
            var uid = _firebase.PlayerId;
            if (string.IsNullOrEmpty(uid)) return result;

            // Letzte 50 Nachrichten laden (orderBy + limitToLast)
            var json = await _firebase.QueryAsync(
                $"guild_chat/{guildId}/messages",
                $"orderBy=\"timestamp\"&limitToLast={MaxMessages}").ConfigureAwait(false);

            if (string.IsNullOrEmpty(json) || json == "null") return result;

            var messages = JsonSerializer.Deserialize<Dictionary<string, ChatMessage>>(json, JsonOptions);
            if (messages == null) return result;

            // Nachrichten in Liste sammeln (ohne LINQ OrderBy)
            foreach (var (_, msg) in messages)
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

            // In-place Sortierung statt LINQ OrderBy (vermeidet Extra-Allokation)
            result.Sort((a, b) =>
            {
                DateTime.TryParse(a.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtA);
                DateTime.TryParse(b.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtB);
                return dtA.CompareTo(dtB);
            });
        }
        catch
        {
            // Netzwerkfehler still behandelt
        }

        return result;
    }

    public void Dispose()
    {
        _sendLock.Dispose();
    }
}
