using System;
using UnityEngine;
using VContainer;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Zentraler Greybox-Coordinator (P0): tickt die P0-§3-Services pro Frame (Produktion +
    /// Worker-Automatisierung), rechnet den Offline-Verdienst beim Start an (P0-§3 OfflineProgressService),
    /// speichert periodisch und bei Pause/Quit (CLAUDE.md-Gotcha: Save in OnApplicationPause(true)).
    /// Die View-/Interaktions-MonoBehaviours referenzieren diesen Controller (per Inspector) und nutzen
    /// die hier exponierten, injizierten Services — robuste Verdrahtung ohne Per-View-DI.
    /// <para>
    /// <b>Gekoppelter Modus:</b> Ist <see cref="runtime"/> gesetzt, arbeiten die Services direkt auf dem
    /// <c>GameModel.Idle</c> des <see cref="RuntimeGameController"/> (eine Wahrheit). Tick, Offline und
    /// Persistenz übernimmt dann ausschließlich der Runtime (GameSimulation.Tick + HMAC-Save) — dieser
    /// Controller liefert nur die Service-Fläche für die physischen Views (Avatar/Stationen/Pads/Tresen).
    /// </para>
    /// </summary>
    public sealed class GreyboxGameController : MonoBehaviour
    {
        [SerializeField] private float autoSaveIntervalSeconds = 15f;
        [Tooltip("Optional: koppelt die physischen Views an den vollen Runtime (eine Wahrheit, ein Save).")]
        [SerializeField] private RuntimeGameController runtime;

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
            if (runtime != null)
            {
                BindToRuntime();
                return; // Offline/Save macht der Runtime
            }
            if (_offline == null) return;
            LastOfflineEarned = _offline.Claim(DateTime.UtcNow.Ticks);
            if (LastOfflineEarned > 0)
                Debug.Log($"[Greybox] Waehrend du weg warst: +{LastOfflineEarned:0}  (Geld jetzt {_economy.Money:0})");
        }

        private void Update()
        {
            if (_stations == null) return;

            if (runtime != null)
            {
                // Prestige ersetzt GameModel.Idle (frischer Loop) -> Services auf den neuen State umbinden.
                if (runtime.Model != null && !ReferenceEquals(_session.State, runtime.Model.Idle))
                    BindToRuntime();
                return; // Tick + Autosave laufen im RuntimeGameController (GameSimulation.Tick)
            }

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

        /// <summary>
        /// Physischer Verkauf am Tresen bedient wartende Kunden der Runtime-Queue (ohne Doppel-Bezahlung —
        /// das Geld kam bereits aus dem Waren-Verkauf). Standalone-Greybox: no-op.
        /// </summary>
        public void NotifyPhysicalSale(int soldCount)
        {
            if (runtime == null || runtime.Model == null || soldCount <= 0) return;
            OrderQueueFormulas.Serve(runtime.Model.Orders, soldCount);
        }

        private void BindToRuntime()
        {
            _session = new GreyboxSession(runtime.IdleBalancing, runtime.Model.Idle);
            _economy = new EconomyService(_session);
            _stations = new StationService(_session);
            _workers = new WorkerAutomationService(_session, _economy);
            _upgrades = new UpgradePadService(_session, _economy);
            _plots = new PlotUnlockService(_session, _economy);
            _offline = null; // Offline-Verdienst rechnet der Runtime (GameSimulation.ComputeOffline)
            LastOfflineEarned = runtime.LastOfflineEarned;
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) PersistNow();
        }

        private void OnApplicationQuit() => PersistNow();

        private void PersistNow()
        {
            if (runtime != null) return; // HMAC-Save macht der Runtime selbst (eigene Pause/Quit-Hooks)
            if (_offline == null || _session == null) return;
            _offline.MarkSeen(DateTime.UtcNow.Ticks);
            GreyboxSave.Save(_session.State);
        }
    }
}
