#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Chat;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Chat
{
    /// <summary>
    /// Chat-Steuerung mit Cooldowns + lokalem Wortfilter. Photon-Chat-Anbindung als STUB.
    /// </summary>
    public sealed class ChatController
    {
        private readonly IAnalyticsService _analytics;
        private DateTime _lastWorldMessageUtc = DateTime.MinValue;
        private readonly HashSet<string> _localProfanityList = new(StringComparer.OrdinalIgnoreCase);

        public event Action<ChatMessage>? MessageReceived;

        public ChatController(IAnalyticsService analytics)
        {
            _analytics = analytics;
            // Pilot-Wortfilter — wird in Production durch Server-Filter ergaenzt.
            _localProfanityList.UnionWith(new[] { "n_word_placeholder", "slur_placeholder" });
        }

        public async UniTask<Result> SendAsync(ChatChannel channel, string body, string? recipientId = null, CancellationToken ct = default)
        {
            if (!ChatValidator.IsLengthValid(body))
                return Result.Failure($"Nachricht ist leer oder >{ChatValidator.MaxMessageLength} Zeichen.");

            if (ContainsProfanity(body))
                return Result.Failure("Nachricht enthaelt unangemessenen Inhalt.");

            if (channel == ChatChannel.World)
            {
                var elapsed = (DateTime.UtcNow - _lastWorldMessageUtc).TotalSeconds;
                if (elapsed < ChatValidator.WorldChannelCooldownSeconds)
                    return Result.Failure($"Welt-Chat-Cooldown: noch {ChatValidator.WorldChannelCooldownSeconds - (int)elapsed}s warten.");
                _lastWorldMessageUtc = DateTime.UtcNow;
            }

            // TODO: Photon-Chat SendChannelMessage.
            await UniTask.Yield(ct);
            _analytics.Track("chat_sent", new Dictionary<string, object>
            {
                ["channel"] = channel.ToString(),
                ["length"] = body.Length
            });
            GameLogger.Verbose("Chat", $"Send[{channel}] '{body}'");
            return Result.Success();
        }

        public void OnMessageReceived(ChatMessage message) => MessageReceived?.Invoke(message);

        private bool ContainsProfanity(string body)
        {
            foreach (var word in _localProfanityList)
                if (body.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
