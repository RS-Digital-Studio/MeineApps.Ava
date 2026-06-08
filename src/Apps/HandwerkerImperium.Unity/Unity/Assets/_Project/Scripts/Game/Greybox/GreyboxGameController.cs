using System;
using UnityEngine;
using VContainer;
using HandwerkerImperium.Domain.Idle;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Zentraler Greybox-Coordinator (P0): tickt die P0-§3-Services pro Frame (Produktion +
    /// Worker-Automatisierung), rechnet den Offline-Verdienst beim Start an (P0-§3 OfflineProgressService),
    /// speichert periodisch und bei Pause/Quit (CLAUDE.md-Gotcha: Save in OnApplicationPause(true)).
    /// Die View-/Interaktions-MonoBehaviours referenzieren diesen Controller (per Inspector) und nutzen
    /// die hier exponierten, injizierten Services — robuste Verdrahtung ohne Per-View-DI.
    /// </summary>
    public sealed class GreyboxGameController : MonoBehaviour
    {
        [SerializeField] private float autoSaveIntervalSeconds = 15f;

        private GreyboxSession _session;
        private StationService _stations;
        private WorkerAutomationService _workers;
        private UpgradePadService _upgrades;
        private PlotUnlockService _plots;
        private OfflineProgressService _offline;
        private EconomyService _economy;
        private float _saveTimer;

        public EconomyService Economy => _economy;
        public StationService Stations => _stations;
        public WorkerAutomationService Workers => _workers;
        public UpgradePadService Upgrades => _upgrades;
        public PlotUnlockService Plots => _plots;

        /// <summary>Das aktive Balancing (fuer View-Werte wie WalkSpeed). Null bis zur DI-Injektion.</summary>
        public IdleBalancing Balancing => _session != null ? _session.Balancing : null;

        /// <summary>Letzter berechneter Offline-Verdienst beim Start (fuer den „Waehrend du weg warst"-Dialog).</summary>
        public decimal LastOfflineEarned { get; private set; }

        [Inject]
        public void Construct(GreyboxSession session, StationService stations, WorkerAutomationService workers,
            UpgradePadService upgrades, PlotUnlockService plots, OfflineProgressService offline, EconomyService economy)
        {
            _session = session;
            _stations = stations;
            _workers = workers;
            _upgrades = upgrades;
            _plots = plots;
            _offline = offline;
            _economy = economy;
        }

        private void Start()
        {
            if (_offline == null) return;
            LastOfflineEarned = _offline.Claim(DateTime.UtcNow.Ticks);
            if (LastOfflineEarned > 0)
                Debug.Log($"[Greybox] Waehrend du weg warst: +{LastOfflineEarned:0}  (Geld jetzt {_economy.Money:0})");
        }

        private void Update()
        {
            if (_stations == null) return;
            double dt = Time.deltaTime;
            _stations.Tick(dt);
            _workers.Tick(dt);

            _saveTimer += Time.deltaTime;
            if (_saveTimer >= autoSaveIntervalSeconds)
            {
                _saveTimer = 0f;
                PersistNow();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) PersistNow();
        }

        private void OnApplicationQuit() => PersistNow();

        private void PersistNow()
        {
            if (_offline == null || _session == null) return;
            _offline.MarkSeen(DateTime.UtcNow.Ticks);
            GreyboxSave.Save(_session.State);
        }
    }
}
