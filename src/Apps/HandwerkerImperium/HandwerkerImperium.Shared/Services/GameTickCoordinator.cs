using System;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Per-Tick-UI-Orchestrierung (1 Hz), aus MainViewModel.GameTick.cs extrahiert.
/// Subscribed selbst auf <see cref="IGameLoopService.OnTick"/> und verteilt die Updates
/// an die Feature-VMs. Tab-spezifische Refreshes laufen nur wenn die Seite sichtbar ist
/// (spart ~20 PropertyChanged-Notifications/Tick) — das Gating läuft über den
/// <see cref="IGameTickHost"/>. Singleton im DI.
/// </summary>
public sealed class GameTickCoordinator : IGameTickCoordinator, IDisposable
{
    private readonly IGameLoopService _gameLoopService;
    private readonly IGameStateService _gameStateService;
    private readonly HeaderViewModel _headerVm;
    private readonly MissionsFeatureViewModel _missionsVm;
    private readonly ResearchViewModel _researchVm;
    private readonly GoalBannerViewModel _goalBannerVm;
    private readonly WorkerMarketViewModel _workerMarketVm;
    private readonly WorkerProfileViewModel _workerProfileVm;
    private readonly IUiEffectBus _uiEffectBus;
    private readonly ILocalizationService _localizationService;
    private IGameTickHost? _host;
    private bool _started;
    private bool _disposed;

    // Zaehler fuer zeitgesteuerte UI-Updates (Worker-Warnung, Lieferant etc.)
    private int _floatingTextCounter;
    // Zaehler fuer Ziel-Aktualisierung (alle 60 Ticks)
    private int _tickForGoal;

    public GameTickCoordinator(
        IGameLoopService gameLoopService,
        IGameStateService gameStateService,
        HeaderViewModel headerVm,
        MissionsFeatureViewModel missionsVm,
        ResearchViewModel researchVm,
        GoalBannerViewModel goalBannerVm,
        WorkerMarketViewModel workerMarketVm,
        WorkerProfileViewModel workerProfileVm,
        IUiEffectBus uiEffectBus,
        ILocalizationService localizationService)
    {
        _gameLoopService = gameLoopService;
        _gameStateService = gameStateService;
        _headerVm = headerVm;
        _missionsVm = missionsVm;
        _researchVm = researchVm;
        _goalBannerVm = goalBannerVm;
        _workerMarketVm = workerMarketVm;
        _workerProfileVm = workerProfileVm;
        _uiEffectBus = uiEffectBus;
        _localizationService = localizationService;
    }

    public void AttachHost(IGameTickHost host) => _host = host;

    public void StartListening()
    {
        if (_started) return;
        _started = true;
        _gameLoopService.OnTick += OnGameTick;
    }

    // Tab-Gating — berechnet aus host.ActivePage statt 5 einzelner Host-Member.
    private bool IsDashboardActive => _host?.ActivePage == ActivePage.Dashboard;
    private bool IsMissionenActive => _host?.ActivePage == ActivePage.Missionen;
    private bool IsBuildingsActive => _host?.ActivePage == ActivePage.Buildings;
    private bool IsWorkerMarketActive => _host?.ActivePage == ActivePage.WorkerMarket;

    private void OnGameTick(object? sender, GameTickEventArgs e)
    {
        if (_host == null) return;
        var state = _gameStateService.State;

        // Nur updaten wenn sich der Wert geaendert hat (vermeidet unnoetige UI-Updates)
        var newIncome = state.NetIncomePerSecond;
        if (newIncome != _headerVm.IncomePerSecond)
        {
            _headerVm.IncomePerSecond = newIncome;
            _headerVm.IncomeDisplay = $"{MoneyFormatter.FormatCompact(_headerVm.IncomePerSecond)}/s";
            _host.UpdateNetIncomeHeader(state);
        }

        // Tick-Counter für zeitgesteuerte UI-Updates (Worker-Warnung, etc.)
        _floatingTextCounter++;

        // QuickJob-Timer aktualisieren + Rotation (delegiert an MissionsVM)
        _missionsVm.UpdateQuickJobTimer();

        // Forschungs-Timer aktualisieren (laeuft im Hintergrund weiter)
        if (_researchVm.HasActiveResearch)
        {
            _researchVm.UpdateTimer();
        }

        // Rush-Timer aktualisieren
        if (_host.IsRushActive || _host.CanActivateRush != !state.IsRushBoostActive)
        {
            _host.UpdateRushDisplay();
        }
        // Boost-Indikator separat prüfen (SpeedBoost kann unabhängig von Rush ablaufen)
        else if (_host.ShowBoostIndicator && !state.IsSpeedBoostActive && !state.IsRushBoostActive)
        {
            _host.UpdateBoostIndicator();
        }

        // Lieferant-Anzeige aktualisieren
        if (_floatingTextCounter % 3 == 0)
        {
            _host.UpdateDeliveryDisplay();
        }

        // Event-Anzeige aktualisieren (Timer + saisonaler Modifikator)
        if (_floatingTextCounter % 5 == 0)
        {
            _host.UpdateEventDisplay();

            // Dashboard/Missionen-spezifische Updates nur wenn sichtbar (spart ~20 PropertyChanged)
            if (IsDashboardActive || IsMissionenActive)
            {
                _missionsVm.RefreshChallenges();
            }

            // Imperium-spezifische Updates nur wenn sichtbar
            if (IsBuildingsActive || IsDashboardActive)
            {
                _host.RefreshReputation(state);
                _host.RefreshPrestigeBanner(state);
            }
        }

        // Nächstes Ziel alle 60 Ticks aktualisieren
        if (_tickForGoal++ >= 60)
        {
            _tickForGoal = 0;
            _goalBannerVm.Refresh();
        }
        else if (_host.HasActiveEvent)
        {
            _host.UpdateEventTimer();
        }

        // Weekly Missions + Lucky Spin + Welcome Back + Worker-Warnung periodisch aktualisieren (alle 10 Ticks)
        if (_floatingTextCounter % 10 == 0)
        {
            // Lucky Spin + Welcome Back Timer (delegiert an MissionsVM)
            if (IsMissionenActive || IsDashboardActive)
            {
                _missionsVm.UpdatePeriodicState();
            }

            // Worker-Warnung nur aktualisieren wenn Imperium/Dashboard sichtbar
            if (IsBuildingsActive || IsDashboardActive)
                _host.UpdateWorkerWarning(state);

            // Soft-Cap-Indikator aktualisieren (nur wenn Dashboard sichtbar)
            if (IsDashboardActive)
            {
                // Einmaliger Hinweis beim ersten Erreichen des Soft-Caps
                if (state.IsSoftCapActive && !_headerVm.IsSoftCapActive)
                {
                    _uiEffectBus.RaiseFloatingText(
                        _localizationService.GetString("SoftCapReached") ?? "Bonus cap reached!",
                        "warning");
                }

                _headerVm.IsSoftCapActive = state.IsSoftCapActive;
                if (state.IsSoftCapActive && state.SoftCapReductionPercent > 0)
                {
                    // GAM-6: Differenzierter Text mit "Einkommen" Prefix
                    var incomeLabel = _localizationService.GetString("SoftCapIncome") ?? "Income";
                    _headerVm.SoftCapText = $"{incomeLabel} -{state.SoftCapReductionPercent}%";
                }
                else if (!state.IsSoftCapActive)
                    _headerVm.SoftCapText = "";
            }
        }

        // Arbeitsmarkt Rotations-Timer jede Sekunde aktualisieren
        if (IsWorkerMarketActive)
        {
            _workerMarketVm.UpdateTimer();
        }

        // WorkerProfile-Fortschritt aktualisieren (Training/Rest-Balken in Echtzeit)
        if (_host.IsWorkerProfileActive && _floatingTextCounter % 3 == 0)
        {
            _workerProfileVm.RefreshDisplayProperties();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_started)
        {
            try { _gameLoopService.OnTick -= OnGameTick; } catch { }
        }
    }
}
