#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Guild;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Guild
{
    /// <summary>
    /// Gilden-Steuerung: Suchen, Beitreten, Verlassen, Mitglieder-Verwaltung, Tech-Tree.
    /// Backend-Calls (Firestore) sind STUBS.
    /// </summary>
    public sealed class GuildController
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly Domain.Config.BalancingConfig _config;

        public GuildController(ISaveService<PlayerSave> save, IAnalyticsService analytics, Domain.Config.BalancingConfig config)
        {
            _save = save;
            _analytics = analytics;
            _config = config;
        }

        public async UniTask<Result<GuildSnapshot>> CreateGuildAsync(string name, string tag, string slogan, CancellationToken ct = default)
        {
            var loadResult = await _save.LoadAsync(ct);
            if (!loadResult.IsSuccess) return Result<GuildSnapshot>.Failure(loadResult.ErrorMessage ?? "Save load failed");

            var save = loadResult.Value!;
            if (save.Profile.Level < _config.GuildMinPlayerLevel)
                return Result<GuildSnapshot>.Failure($"Mindestlevel {_config.GuildMinPlayerLevel} fuer Gilden-Gruendung.");
            if (!save.Currencies.SpendGold(_config.GuildFoundationCost))
                return Result<GuildSnapshot>.Failure($"Nicht genug Gold ({_config.GuildFoundationCost:N0} benoetigt).");

            await _save.SaveAsync(save, ct);

            // TODO: Firestore Transaction — Tag-Eindeutigkeit prüfen, Gilde anlegen.
            var guild = new GuildSnapshot(
                id: Guid.NewGuid().ToString("N"),
                name: name,
                tag: tag,
                leaderId: save.Profile.UserId,
                createdAtUtc: DateTime.UtcNow);
            guild.Slogan = slogan;
            guild.Members.Add(new GuildMember(save.Profile.UserId, save.Profile.DisplayName, save.Profile.Level, GuildRole.Leader, DateTime.UtcNow));

            save.Profile.GuildId = guild.Id;
            await _save.SaveAsync(save, ct);
            _analytics.Track("guild_created", new Dictionary<string, object> { ["guild_id"] = guild.Id });
            return Result<GuildSnapshot>.Success(guild);
        }

        public async UniTask<Result> RequestJoinAsync(string guildId, CancellationToken ct = default)
        {
            // TODO: Firestore Append zum Application-Subcollection
            await UniTask.Yield(ct);
            _analytics.Track("guild_join_requested", new Dictionary<string, object> { ["guild_id"] = guildId });
            GameLogger.Info("Guild", $"RequestJoin {guildId} — STUB.");
            return Result.Success();
        }

        public async UniTask<Result> LeaveGuildAsync(CancellationToken ct = default)
        {
            var saveResult = await _save.LoadAsync(ct);
            if (!saveResult.IsSuccess) return Result.Failure(saveResult.ErrorMessage ?? "Save load failed");

            var save = saveResult.Value!;
            if (string.IsNullOrEmpty(save.Profile.GuildId)) return Result.Failure("Keine Gilde.");

            // TODO: Firestore — Mitglied entfernen, ggf. Gilde auflösen wenn letzter Leader.
            save.Profile.GuildId = null;
            await _save.SaveAsync(save, ct);
            _analytics.Track("guild_left");
            return Result.Success();
        }

        public async UniTask<Result> DonateGuildPointsAsync(long amount, CancellationToken ct = default)
        {
            if (amount <= 0) return Result.Failure("Spende muss > 0 sein.");
            await _save.MutateAsync(save =>
            {
                save.Currencies.AddGuildPoints(amount);
                return save;
            }, ct);
            _analytics.Track("guild_donated", new Dictionary<string, object> { ["amount"] = amount });
            return Result.Success();
        }
    }
}
