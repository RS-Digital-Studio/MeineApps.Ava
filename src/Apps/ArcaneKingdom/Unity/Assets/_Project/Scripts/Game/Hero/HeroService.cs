#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Hero;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ArcaneKingdom.Game.Hero
{
    /// <summary>
    /// Verwaltet die Helden-Auswahl des Spielers und stellt die HeroDefinition fuer den
    /// aktiven Held bereit. Wechsel-Cooldown 30 Tage (DESIGN.md Kap. 9.6).
    /// </summary>
    public sealed class HeroService
    {
        private const int SwitchCooldownDays = 30;

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly List<HeroDefinition> _availableHeroes = new();

        public HeroService(ISaveService<PlayerSave> save, IAnalyticsService analytics)
        {
            _save = save;
            _analytics = analytics;
            LoadHeroes();
        }

        public IReadOnlyList<HeroDefinition> AvailableHeroes => _availableHeroes;
        public HeroDefinition? CurrentHero { get; private set; }

        public async UniTask<Result> SelectInitialHeroAsync(string heroId, CancellationToken ct = default)
        {
            var hero = _availableHeroes.FirstOrDefault(h => h.Id == heroId);
            if (hero == null) return Result.Failure($"Held '{heroId}' unbekannt.");
            await _save.MutateAsync(save =>
            {
                save.Profile.AvatarKey = $"hero.{heroId}";
                return save;
            }, ct);
            CurrentHero = hero;
            _analytics.Track("hero_selected", new Dictionary<string, object> { ["hero_id"] = heroId });
            return Result.Success();
        }

        private void LoadHeroes()
        {
            _availableHeroes.AddRange(Resources.LoadAll<HeroDefinition>(""));
            if (_availableHeroes.Count == 0)
                GameLogger.Warning("Hero", "Keine HeroDefinitions in Resources — laeuft im Editor erst nach DataImporter -> Import Heroes.");
        }
    }
}
