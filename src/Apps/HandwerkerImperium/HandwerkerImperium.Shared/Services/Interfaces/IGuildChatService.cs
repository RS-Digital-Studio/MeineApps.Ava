namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Einfacher Gilden-Chat via Firebase.
/// Letzte 50 Nachrichten, Polling bei Tab-Wechsel (kein Echtzeit-Push).
/// Max 200 Zeichen pro Nachricht, 5 Sekunden Cooldown.
/// </summary>
public interface IGuildChatService
{
    /// <summary>Nachricht senden (max 200 Zeichen, 5s Cooldown).</summary>
    Task<bool> SendMessageAsync(string guildId, string text);

    /// <summary>Letzte 50 Nachrichten laden.</summary>
    Task<List<ChatMessageDisplay>> GetRecentMessagesAsync(string guildId);

    /// <summary>Ob der Cooldown abgelaufen ist und eine neue Nachricht gesendet werden kann.</summary>
    bool CanSendMessage { get; }
}

/// <summary>
/// Anzeige-Daten für eine Chat-Nachricht.
/// </summary>
public class ChatMessageDisplay
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public bool IsOwnMessage { get; set; }
}
