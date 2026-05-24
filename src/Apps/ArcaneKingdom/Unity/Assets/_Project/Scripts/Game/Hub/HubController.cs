#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Hub
{
    /// <summary>
    /// Steuerung der Hub-Welt: Energie-Regen-Tick, Navigation zu Welten/Arena/Shop/Gilde.
    ///
    /// SKELETT: Energie-Regen implementiert, alle Navigations-Targets sind Stubs.
    /// </summary>
    public sealed class HubController
    {
        private readonly ISaveService<PlayerSave> _save;
        private readonly ISceneLoaderService _sceneLoader;
        private readonly IAnalyticsService _analytics;
        private readonly Domain.Config.BalancingConfig _config;

        public HubController(ISaveService<PlayerSave> save, ISceneLoaderService sceneLoader,
                             IAnalyticsService analytics, Domain.Config.BalancingConfig config)
        {
            _save = save;
            _sceneLoader = sceneLoader;
            _analytics = analytics;
            _config = config;
        }

        /// <summary>
        /// Wird vom Hub-Tick (z.B. alle 30s) aufgerufen. Regeneriert Energie aus
        /// vergangener Zeit seit dem letzten Tick.
        /// </summary>
        public async UniTask RegenerateEnergyAsync(CancellationToken ct = default)
        {
            await _save.MutateAsync(save =>
            {
                var elapsed = (System.DateTime.UtcNow - save.LastEnergyRegenAtUtc).TotalSeconds;
                var energyToAdd = (int)(elapsed / _config.EnergyRegenSeconds);
                if (energyToAdd > 0)
                {
                    save.Currencies.AddEnergy(energyToAdd);
                    save.LastEnergyRegenAtUtc = save.LastEnergyRegenAtUtc
                        .AddSeconds(energyToAdd * _config.EnergyRegenSeconds);
                    GameLogger.Verbose("Hub", $"+{energyToAdd} Energie regeneriert.");
                }
                return save;
            }, ct);
        }

        public async UniTask OpenWorldMapAsync(CancellationToken ct = default)
        {
            _analytics.Track("hub_open_world_map");
            await UniTask.CompletedTask;
            GameLogger.Info("Hub", "TODO: WorldMap-Scene laden.");
        }

        public async UniTask OpenArenaAsync(CancellationToken ct = default)
        {
            _analytics.Track("hub_open_arena");
            await _sceneLoader.LoadAdditiveAsync(SceneNames.Arena, ct);
        }

        public async UniTask OpenShopAsync(CancellationToken ct = default)
        {
            _analytics.Track("hub_open_shop");
            await UniTask.CompletedTask;
            GameLogger.Info("Hub", "TODO: Shop-Overlay aktivieren.");
        }

        public async UniTask OpenGuildAsync(CancellationToken ct = default)
        {
            _analytics.Track("hub_open_guild");
            await _sceneLoader.LoadAdditiveAsync(SceneNames.Guild, ct);
        }
    }
}
