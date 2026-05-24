#nullable enable
using System;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Chat;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Save;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Chat
{
    /// <summary>
    /// Verwaltet Mute-Listen, Reports und Auto-Mute-Logik (DESIGN.md Kap. 14.3).
    /// Aktionen werden lokal im PlayerSave persistiert; Server-seitige Aggregation
    /// (3 Reports/24h → AutoMute) erfolgt in einer Cloud Function.
    /// </summary>
    public sealed class ChatModerationService
    {
        private const int AutoMuteReportThreshold = 3;
        private static readonly TimeSpan AutoMuteWindow = TimeSpan.FromHours(24);
        private static readonly TimeSpan AutoMuteDuration = TimeSpan.FromHours(24);

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;

        public ChatModerationService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
        }

        public bool IsMessageVisible(ChatMessage message, ChatSaveSlice mySlice) =>
            !mySlice.MutedPlayerIds.Contains(message.SenderId);

        public bool CanSendNow(ChatSaveSlice mySlice) =>
            !mySlice.MutedUntilUtc.HasValue || mySlice.MutedUntilUtc.Value <= DateTime.UtcNow;

        public async UniTask<Result> MutePlayerAsync(string targetPlayerId, CancellationToken ct = default)
        {
            await _save.MutateAsync(save => { save.ChatSlice.MutedPlayerIds.Add(targetPlayerId); return save; }, ct);
            _analytics.Track("chat_mute_added");
            GameLogger.Info("ChatMod", $"User '{targetPlayerId}' gemutet.");
            return Result.Success();
        }

        public async UniTask<Result> UnmutePlayerAsync(string targetPlayerId, CancellationToken ct = default)
        {
            await _save.MutateAsync(save => { save.ChatSlice.MutedPlayerIds.Remove(targetPlayerId); return save; }, ct);
            _analytics.Track("chat_mute_removed");
            return Result.Success();
        }

        public async UniTask<Result> ReportMessageAsync(ChatMessage message, ReportReason reason, CancellationToken ct = default)
        {
            await _save.MutateAsync(save =>
            {
                save.ChatSlice.ReportsSent.Add(new ChatReportEntry
                {
                    ReportedPlayerId = message.SenderId,
                    MessageId = message.Id,
                    Reason = reason.ToString(),
                    ReportedAtUtc = DateTime.UtcNow
                });
                return save;
            }, ct);
            _analytics.Track("chat_report_sent", new System.Collections.Generic.Dictionary<string, object>
            {
                ["reason"] = reason.ToString(), ["reported_player_id"] = message.SenderId
            });
            // TODO Server: Cloud Function aggregiert Reports und triggert AutoMute zentral.
            return Result.Success();
        }

        /// <summary>
        /// Client-seitiger AutoMute-Check (Fallback wenn Server nicht erreichbar).
        /// In Production maßgeblich ist die Server-Aggregation.
        /// </summary>
        public async UniTask EvaluateAutoMuteAsync(string targetPlayerId, CancellationToken ct = default)
        {
            await _save.MutateAsync(save =>
            {
                var since = DateTime.UtcNow - AutoMuteWindow;
                var count = save.ChatSlice.ReportsSent.Count(r =>
                    r.ReportedPlayerId == targetPlayerId && r.ReportedAtUtc >= since);
                if (count >= AutoMuteReportThreshold)
                {
                    save.ChatSlice.MutedUntilUtc = DateTime.UtcNow + AutoMuteDuration;
                    GameLogger.Warning("ChatMod", $"AutoMute: User wurde {count}x in 24h gemeldet.");
                }
                return save;
            }, ct);
        }
    }
}
