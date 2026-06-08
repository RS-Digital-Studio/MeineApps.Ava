using UnityEngine;
using VContainer;
using VContainer.Unity;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// VContainer-Scope des Greybox-Prototyps (P0-Spec §3: „DI via VContainer"). Registriert Balancing +
    /// Sim-Zustand (aus Save oder neu) und die sechs benannten P0-§3-Logik-Services. Der
    /// <see cref="GreyboxGameController"/> in der Szene wird per RegisterComponentInHierarchy injiziert.
    /// </summary>
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private BalancingConfig balancingConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            IdleBalancing balancing = balancingConfig != null ? balancingConfig.ToDomain() : new IdleBalancing();

            GreyboxSimState state = GreyboxSave.Load();
            if (state == null || state.Stations == null || state.Stations.Count != balancing.Stations.Count)
                state = GreyboxSimState.CreateNew(balancing);

            builder.RegisterInstance(new GreyboxSession(balancing, state));

            builder.Register<StationService>(Lifetime.Singleton);
            builder.Register<EconomyService>(Lifetime.Singleton);
            builder.Register<WorkerAutomationService>(Lifetime.Singleton);
            builder.Register<UpgradePadService>(Lifetime.Singleton);
            builder.Register<PlotUnlockService>(Lifetime.Singleton);
            builder.Register<OfflineProgressService>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<GreyboxGameController>();
        }
    }
}
