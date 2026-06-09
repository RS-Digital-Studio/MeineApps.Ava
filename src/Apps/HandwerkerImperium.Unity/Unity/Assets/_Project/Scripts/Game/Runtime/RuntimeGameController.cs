using System;
using System.Collections.Generic;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Config;
using HandwerkerImperium.Domain.Runtime;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Achievements;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Laufzeit-Host des Vollspiels (Szenen-Root, MVVM-konform: Config via SerializeField, kein Service-Locator):
    /// hält EINE <see cref="GameModel"/>-Instanz, lädt/persistiert sie (HMAC), rechnet Offline-Verdienst beim
    /// Start und treibt pro Frame <see cref="GameSimulation.Tick"/>. Die gesamte Spiel-Logik liegt im getesteten
    /// Domain-Orchestrator; dieser Controller ist dünne Unity-Verdrahtung. Views referenzieren ihn per SerializeField.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeGameController : MonoBehaviour
    {
        [SerializeField] private GameBalancingConfig config;
        [SerializeField] private float autosaveIntervalSeconds = 30f;

        private GameModel _model;
        private IdleBalancing _idleBal;
        private GameBalancing _bal;
        private List<MasterToolDefinition> _masterToolCatalog;
        private IReadOnlyList<AchievementDefinition> _achievementCatalog;
        private string _deviceKey;
        private float _autosaveTimer;
        private float _progressTimer;

        public GameModel Model => _model;
        public GameBalancing Balancing => _bal;
        public decimal LastOfflineEarned { get; private set; }
        public int CollectedToolsCount => _model != null ? _model.CollectedMasterTools.Count : 0;
        public int AchievementsCount => _model != null ? _model.ClaimedAchievements.Count : 0;

        private void Awake()
        {
            _idleBal = config != null ? config.ToIdleBalancing() : new IdleBalancing();
            _bal = config != null ? config.ToGameBalancing() : new GameBalancing();
            _masterToolCatalog = MasterToolFormulas.DefaultCatalog();
            _achievementCatalog = AchievementCatalog.Default();
            _deviceKey = RuntimeSave.DeviceKey;
            _model = RuntimeSave.HasSave ? RuntimeSave.Load(_deviceKey, _idleBal) : GameModel.CreateNew(_idleBal);
            if (_model == null) _model = GameModel.CreateNew(_idleBal);
        }

        private void Start()
        {
            long now = DateTime.UtcNow.Ticks;
            if (_model.Idle.LastSeenUtcTicks > 0)
            {
                double elapsed = new TimeSpan(now - _model.Idle.LastSeenUtcTicks).TotalSeconds;
                if (elapsed > 0)
                {
                    LastOfflineEarned = GameSimulation.ComputeOffline(_model, _idleBal, _bal, _masterToolCatalog, elapsed);
                    _model.Idle.Money += LastOfflineEarned;
                }
            }
            _model.Idle.LastSeenUtcTicks = now;
        }

        private void Update()
        {
            long now = DateTime.UtcNow.Ticks;
            GameSimulation.Tick(_model, _idleBal, _bal, Time.deltaTime, now);

            // Permanente Fortschritts-Systeme periodisch auswerten (Master-Tools sammeln, Achievements gutschreiben).
            _progressTimer += Time.deltaTime;
            if (_progressTimer >= 1f)
            {
                _progressTimer = 0f;
                GameProgress.CollectEligibleMasterTools(_model, _masterToolCatalog);
                GameProgress.GrantNewAchievements(_model, _achievementCatalog);
            }

            _autosaveTimer += Time.deltaTime;
            if (_autosaveTimer >= autosaveIntervalSeconds)
            {
                _autosaveTimer = 0f;
                PersistNow();
            }
        }

        /// <summary>Effektives automatisiertes Einkommen/s (für UI-Anzeige).</summary>
        public decimal EffectiveIncomePerSecond() =>
            GameSimulation.EffectiveIncomePerSecond(_model, _idleBal, _bal, _masterToolCatalog);

        public int EvaluateStar() => GameSimulation.EvaluateStar(_model, _bal);
        public bool CanPrestige() => GameSimulation.CanPrestige(_model, _bal);
        public bool TryPrestige() => GameSimulation.TryPrestige(_model, _idleBal, _bal);

        // ── Spieler-Aktionen (echte GameActions-Logik) ─────────────────────
        public decimal ServeCustomer(int stationIndex) => GameActions.ServeCustomer(_model, _idleBal, stationIndex, DateTime.UtcNow.Ticks);
        public bool BuyUpgrade(UpgradeTrack track) => GameActions.BuyUpgrade(_model, _idleBal, track);
        public bool HireWorker(int stationIndex) => GameActions.HireWorker(_model, _idleBal, stationIndex);
        public bool UnlockPlot(int stationIndex) => GameActions.UnlockPlot(_model, _idleBal, stationIndex);
        public int GainMastery(double xp) => GameActions.GainMastery(_model, _bal, xp);

        /// <summary>Convenience für die UI (ohne Domain-Typ): kauft eine Stations-Tempo-Upgrade-Stufe.</summary>
        public bool BuyTempoUpgrade() => GameActions.BuyUpgrade(_model, _idleBal, UpgradeTrack.StationSpeed);

        public void PersistNow()
        {
            if (_model == null) return;
            _model.Idle.LastSeenUtcTicks = DateTime.UtcNow.Ticks;
            RuntimeSave.Save(_model, _deviceKey);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) PersistNow();
        }

        private void OnApplicationQuit() => PersistNow();
    }
}
