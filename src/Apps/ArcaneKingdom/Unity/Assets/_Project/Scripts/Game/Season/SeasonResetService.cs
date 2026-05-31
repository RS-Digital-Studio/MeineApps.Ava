#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Season;
using ArcaneKingdom.Game.Quest;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Season
{
    /// <summary>
    /// Triggert Daily-/Weekly-/Saison-Resets bei Hub-Tick. Anker-Datum kommt aus PlayerSave.
    /// </summary>
    public sealed class SeasonResetService
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly QuestService _questService;
        private readonly IAnalyticsService _analytics;

        public event Action? DailyReset;
        public event Action? WeeklyReset;
        public event Action? SeasonReset;

        public SeasonResetService(ISaveService<PlayerSave> save, QuestService questService, IAnalyticsService analytics)
        {
            _save = save;
            _questService = questService;
            _analytics = analytics;
        }

        public async UniTask CheckResetsAsync(CancellationToken ct = default)
        {
            var loadResult = await _save.LoadAsync(ct);
            if (!loadResult.IsSuccess) return;
            var save = loadResult.Value!;
            var nowUtc = DateTime.UtcNow;

            // Anker sind die DAFUER vorgesehenen Quest-Slice-Felder — NICHT LastEnergyRegenAtUtc
            // (das wird bei jeder Energie-Regen aktualisiert, wodurch der Reset praktisch nie feuerte).
            save.Quests ??= new ArcaneKingdom.Domain.Save.QuestSaveSlice();
            var lastDaily = save.Quests.LastDailyResetUtc;
            var lastWeekly = save.Quests.LastWeeklyResetUtc;

            // Beim Erststart (Anker == default) nur den Anker setzen, keinen Reset ausloesen.
            var daily  = lastDaily  != default && ResetWindow.HasCrossedDailyReset(lastDaily, nowUtc);
            var weekly = lastWeekly != default && ResetWindow.HasCrossedWeeklyReset(lastWeekly, nowUtc);

            if (daily)
            {
                _questService.ResetDaily();
                _analytics.Track("daily_reset_applied");
                DailyReset?.Invoke();
            }
            if (weekly)
            {
                _questService.ResetWeekly();
                _analytics.Track("weekly_reset_applied");
                WeeklyReset?.Invoke();
            }

            // Reset-Anker fortschreiben (und beim Erststart initialisieren). Quest-Counts sichern.
            if (daily || weekly || lastDaily == default || lastWeekly == default)
            {
                if (daily || weekly) await _questService.FlushAsync(ct);
                await _save.MutateAsync(s =>
                {
                    s.Quests ??= new ArcaneKingdom.Domain.Save.QuestSaveSlice();
                    if (daily || lastDaily == default)  s.Quests.LastDailyResetUtc = nowUtc;
                    if (weekly || lastWeekly == default) s.Quests.LastWeeklyResetUtc = nowUtc;
                    return s;
                }, ct);
            }

            // Saison: pragmatisch über Account-Erstellung als Anker — Production wird Server-getrieben sein
            var seasonStart = save.Profile.CreatedAtUtc;
            var seasonEnd = ResetWindow.NextSeasonResetUtc(seasonStart);
            if (nowUtc >= seasonEnd)
            {
                _analytics.Track("season_reset_applied");
                SeasonReset?.Invoke();
                GameLogger.Info("Season", $"Saison resettet (Start {seasonStart:O}, Ende {seasonEnd:O}).");
                // TODO MVP: Arena-Rangpunkte um 25% reduzieren, Belohnungen verteilen,
                //          neuen seasonStart im Save persistieren.
            }
        }
    }
}
