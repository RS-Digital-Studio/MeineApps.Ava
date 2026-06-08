using System;
using UnityEngine;
using VContainer;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Zentraler Greybox-Coordinator (P0): tickt die P0-§3-Services pro Frame (Produktion +
    /// Worker-Automatisierung), rechnet den Offline-Verdienst beim Start an (P0-§3 OfflineProgressService),
    /// speichert periodisch und bei Pause/Quit (CLAUDE.md-Gotcha: Save in OnApplicationPause(true)).
    /// Die Interaktions-/View-MonoBehaviours rufen die hier exponierten, injizierten Services.
    /// </summary>
    public sealed class GreyboxGameController : MonoBehaviour
    {
        [SerializeField] private float autoSaveIntervalSeconds = 15f;

        private GreyboxSession _session;
        private StationService _stations;
        private WorkerAutomationService _workers;
        private OfflineProgressService _offline;
        private EconomyService _economy;
        private float _saveTimer;

        public EconomyService Economy => _economy;
        public StationService Stations => _stations;
        public WorkerAutomationService Workers => _workers;

        /// <summary>Letzter berechneter Offline-Verdienst beim Start (fuer den „Waehrend du weg warst"-Dialog).</summary>
        public decimal LastOfflineEarned { get; private set; }

        [Inject]
        public void Construct(GreyboxSession session, StationService stations, WorkerAutomationService workers,
            OfflineProgressService offline, EconomyService economy)
        {
            _session = session;
            _stations = stations;
            _workers = workers;
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
