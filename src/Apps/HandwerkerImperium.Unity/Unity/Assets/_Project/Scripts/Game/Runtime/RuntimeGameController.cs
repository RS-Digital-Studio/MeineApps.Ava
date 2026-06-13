using System;
using System.Collections.Generic;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Config;
using HandwerkerImperium.Domain.Runtime;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Achievements;
using HandwerkerImperium.Domain.Story;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Monetization;

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
        private IReadOnlyList<StoryBeatDefinition> _storyCatalog;
        private List<DailyTaskDefinition> _dailyTaskPool;
        private string _deviceKey;
        private float _autosaveTimer;
        private float _progressTimer;

        public GameModel Model => _model;
        public GameBalancing Balancing => _bal;
        /// <summary>Idle-Balancing (für die physische 3D-Loop-Kopplung, siehe GreyboxGameController).</summary>
        public IdleBalancing IdleBalancing => _idleBal;

        private bool _offlineDoubled;

        /// <summary>
        /// Verdoppelt den Offline-Verdienst dieses Starts einmalig (UI: "Verdoppeln per Werbung" —
        /// bis zum Ad-SDK ein direkter Stub). Liefert den zusätzlich gutgeschriebenen Betrag.
        /// </summary>
        public decimal DoubleOfflineOnce()
        {
            if (_offlineDoubled || LastOfflineEarned <= 0m || _model == null) return 0m;
            _offlineDoubled = true;
            _model.Idle.Money += LastOfflineEarned;
            return LastOfflineEarned;
        }
        public decimal LastOfflineEarned { get; private set; }
        public string LatestStoryBeat { get; private set; } = "";
        public int CollectedToolsCount => _model != null ? _model.CollectedMasterTools.Count : 0;
        public int AchievementsCount => _model != null ? _model.ClaimedAchievements.Count : 0;

        private void Awake()
        {
            // Desktop/Editor: ohne Fenster-Fokus weiterticken (Android ignoriert das; dort sichert OnApplicationPause).
            Application.runInBackground = true;
            _idleBal = config != null ? config.ToIdleBalancing() : new IdleBalancing();
            _bal = config != null ? config.ToGameBalancing() : new GameBalancing();
            _masterToolCatalog = MasterToolFormulas.DefaultCatalog();
            _achievementCatalog = AchievementCatalog.Default();
            _storyCatalog = StoryCatalog.Default();
            _dailyTaskPool = DailyTaskCatalog.Pool();
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
                var beats = GameProgress.EvaluateStory(_model, _storyCatalog);
                if (beats.Count > 0) LatestStoryBeat = beats[beats.Count - 1];
                GameProgress.EvaluateDailyTasks(_model, _dailyTaskPool, DateTime.UtcNow.Ticks);
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

        /// <summary>Holt die Tagesbelohnung ab (einkommens-skaliert). Liefert den Geld-Betrag (0 = nicht fällig).</summary>
        public decimal ClaimDaily() => GameActions.ClaimDaily(_model, _bal, 500m, EffectiveIncomePerSecond(), DateTime.UtcNow.Ticks);

        /// <summary>Kauft eine Stufe des Global-Tempo-Perks (Imperium-Marken).</summary>
        public bool BuyTempoPerk() => GameActions.BuyPerk(_model, _bal, PerkKind.GlobalTempo);

        /// <summary>Kauft (in der Endstadt) den nächsten Meistergrad (Renommee).</summary>
        public bool BuyMeistergrad() => GameActions.BuyMeistergrad(_model, _bal);

        /// <summary>Startet das Rush-Event (alle Stationen kurz 2×).</summary>
        public bool StartRush() => GameActions.StartRush(_model, _bal, DateTime.UtcNow.Ticks);

        /// <summary>True, wenn gerade ein Rush-Event läuft.</summary>
        public bool RushActive() => RushEventFormulas.IsActive(_model.Rush, DateTime.UtcNow.Ticks);

        /// <summary>Aktive Saison (oder „keine").</summary>
        public string CurrentSeason() =>
            SeasonalFormulas.TryGetActiveSeason(DateTime.UtcNow, out var s) ? s.ToString() : "keine";

        /// <summary>Fortschritt 0..1 einer Tagesaufgabe (für die UI).</summary>
        public double DailyTaskProgress01(DailyTaskRuntime t) => GameProgress.DailyTaskProgress01(_model, t);

        /// <summary>Perfekt-Aktion (GDD §6.7): setzt den temporaeren Tempo-Buff auf eine Station.</summary>
        public void ApplyStationBoost(int stationIndex, decimal multiplier, double durationSeconds) =>
            GreyboxSimulation.ApplyBoost(_model.Idle, stationIndex, (double)multiplier, durationSeconds);

        // ── Worker-Verwaltung (HUD-Panel, GDD §6.2: Anstellen + Tempo-Stufen statt Boden-Platten) ──

        /// <summary>Anzahl Stationen (für das Verwaltungs-Panel).</summary>
        public int StationCount => _model != null ? _model.Idle.Stations.Count : 0;

        /// <summary>Schnappschuss des Worker-/Station-Status für eine Zeile des Verwaltungs-Panels.</summary>
        public WorkerRowInfo GetWorkerRow(int i)
        {
            var st = _model.Idle.Stations[i];
            int max = _idleBal.WorkerMaxLevel;
            int buildMax = _idleBal.StationBuildMaxLevel;
            return new WorkerRowInfo
            {
                Unlocked = st.Unlocked,
                HasWorker = st.HasWorker,
                Level = st.WorkerLevel,
                MaxLevel = max,
                HireCost = _idleBal.WorkerHireCost,
                UpgradeCost = GreyboxSimulation.WorkerUpgradeCostFor(_model.Idle, _idleBal, i),
                AtMax = st.HasWorker && st.WorkerLevel >= max,
                BuildLevel = st.BuildLevel,
                BuildMaxLevel = buildMax,
                BuildCost = GreyboxSimulation.StationBuildCostFor(_model.Idle, _idleBal, i),
                BuildAtMax = st.BuildLevel >= buildMax
            };
        }

        // Anstellen nutzt die bereits vorhandene HireWorker(int) oben (GameActions).

        /// <summary>Kauft die nächste Worker-Tempo-Stufe (HUD-Panel). Liefert true bei Erfolg.</summary>
        public bool UpgradeWorker(int i) => GreyboxSimulation.UpgradeWorker(_model.Idle, _idleBal, i);

        // ── Werkstatt-Ausbaustufen (GDD §6.1, Open-Shop) ──
        public int StationBuildLevel(int i) =>
            i >= 0 && i < _model.Idle.Stations.Count ? _model.Idle.Stations[i].BuildLevel : 0;
        public int StationBuildMaxLevel => _idleBal.StationBuildMaxLevel;
        public decimal StationBuildCost(int i) => GreyboxSimulation.StationBuildCostFor(_model.Idle, _idleBal, i);
        /// <summary>Baut die Werkstatt eine sichtbare Stufe aus (HUD-Panel). Liefert true bei Erfolg.</summary>
        public bool UpgradeStationBuild(int i) => GreyboxSimulation.UpgradeStationBuild(_model.Idle, _idleBal, i);

        /// <summary>Aktueller Geldstand (für Affordability-Checks im Panel).</summary>
        public decimal Money => _model != null ? _model.Idle.Money : 0m;

        /// <summary>Free-Cash-Pad (per Ad): 2× Einkommen je Zeitblock. Liefert den gutgeschriebenen Betrag.</summary>
        public decimal ClaimFreeCash()
        {
            decimal reward = MonetizationFormulas.FreeCashReward(
                EffectiveIncomePerSecond(), _bal.Monetization.FreeCashBlockSeconds, _bal.Monetization.FreeCashAdMultiplier);
            if (reward > 0m) _model.Idle.Money += reward;
            return reward;
        }

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
