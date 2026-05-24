#nullable enable
using System;

namespace ArcaneKingdom.Domain.Chat
{
    public enum ChatChannel
    {
        World = 0,
        Guild = 1,
        Private = 2,
        System = 3
    }

    /// <summary>
    /// Chat-Nachricht (DESIGN.md Kap. 14).
    /// </summary>
    [Serializable]
    public sealed class ChatMessage
    {
        public string Id { get; }
        public ChatChannel Channel { get; }
        public string SenderId { get; }
        public string SenderDisplayName { get; }
        public string? SenderGuildTag { get; }
        public string? RecipientId { get; }                // Nur fuer Private
        public string Body { get; }
        public DateTime SentAtUtc { get; }

        public ChatMessage(string id, ChatChannel channel, string senderId, string senderDisplayName,
                           string? senderGuildTag, string? recipientId, string body, DateTime sentAtUtc)
        {
            Id = id;
            Channel = channel;
            SenderId = senderId;
            SenderDisplayName = senderDisplayName;
            SenderGuildTag = senderGuildTag;
            RecipientId = recipientId;
            Body = body;
            SentAtUtc = sentAtUtc;
        }
    }

    /// <summary>
    /// Validierung von Chat-Eingaben (Laenge, Wortfilter, Cooldown).
    /// </summary>
    public static class ChatValidator
    {
        public const int MaxMessageLength = 200;
        public const int WorldChannelCooldownSeconds = 30;

        public static bool IsLengthValid(string body) => !string.IsNullOrWhiteSpace(body) && body.Length <= MaxMessageLength;
    }
}
