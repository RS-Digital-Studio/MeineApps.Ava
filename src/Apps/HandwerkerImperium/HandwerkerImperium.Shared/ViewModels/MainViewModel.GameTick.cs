using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Per-Tick UI-Aktualisierungen (1 Hz vom GameLoopService getrieben).
/// aus MainViewModel.cs extrahiert (12.05.2026).
/// Optimierung: tab-spezifische Refreshes nur wenn sichtbar (spart ~20 PropertyChanged-Notifications/Tick).
/// </summary>
public sealed partial class MainViewModel
{
    private void OnGameTick(object? sender, GameTickEventArgs e)
    {
        // Nur updaten wenn sich der Wert geaendert hat (vermeidet unnoetige UI-Updates)
        var state = _gameStateService.State;
        var newIncome = state.NetIncomePerSecond;
        if (newIncome != HeaderVM.IncomePerSecond)
        {
            HeaderVM.IncomePerSecond = newIncome;
            HeaderVM.IncomeDisplay = $"{FormatMoney(HeaderVM.IncomePerSecond)}/s";
            UpdateNetIncomeHeader(state);
        }

        // Tick-Counter für zeitgesteuerte UI-Updates (Worker-Warnung, etc.)
        _floatingTextCounter++;

        // QuickJob-Timer aktualisieren + Rotation (delegiert an MissionsVM)
        MissionsVM.UpdateQuickJobTimer();

        // Forschungs-Timer aktualisieren (laeuft im Hintergrund weiter)
        if (ResearchViewModel.HasActiveResearch)
        {
            ResearchViewModel.UpdateTimer();
        }

        // Rush-Timer aktualisieren
        if (IsRushActive || CanActivateRush != !_gameStateService.State.IsRushBoostActive)
        {
            UpdateRushDisplay();
        }
        // Boost-Indikator separat prüfen (SpeedBoost kann unabhängig von Rush ablaufen)
        else if (ShowBoostIndicator && !_gameStateService.State.IsSpeedBoostActive && !_gameStateService.State.IsRushBoostActive)
        {
            UpdateBoostIndicator();
        }

        // Lieferant-Anzeige aktualisieren
        if (_floatingTextCounter % 3 == 0)
        {
            UpdateDeliveryDisplay();
        }

        // Event-Anzeige aktualisieren (Timer + saisonaler Modifikator)
        if (_floatingTextCounter % 5 == 0)
        {
            UpdateEventDisplay();

            // Dashboard/Missionen-spezifische Updates nur wenn sichtbar (spart ~20 PropertyChanged)
            if (IsDashboardActive || IsMissionenActive)
            {
                MissionsVM.RefreshChallenges();
            }

            // Imperium-spezifische Updates nur wenn sichtbar
            if (IsBuildingsActive || IsDashboardActive)
            {
                RefreshReputation(state);
                RefreshPrestigeBanner(state);
            }
        }

        // Nächstes Ziel alle 60 Ticks aktualisieren
        if (_tickForGoal++ >= 60)
        {
            _tickForGoal = 0;
            RefreshCurrentGoal();
        }
        else if (HasActiveEvent)
        {
            UpdateEventTimer();
        }

        // Weekly Missions + Lucky Spin + Welcome Back + Worker-Warnung periodisch aktualisieren (alle 10 Ticks)
        if (_floatingTextCounter % 10 == 0)
        {
            // Lucky Spin + Welcome Back Timer (delegiert an MissionsVM)
            if (IsMissionenActive || IsDashboardActive)
            {
                MissionsVM.UpdatePeriodicState();
            }

            // Worker-Warnung nur aktualisieren wenn Imperium/Dashboard sichtbar
            if (IsBuildingsActive || IsDashboardActive)
                UpdateWorkerWarning(state);

            // Soft-Cap-Indikator aktualisieren (nur wenn Dashboard sichtbar)
            if (IsDashboardActive)
            {
                // Einmaliger Hinweis beim ersten Erreichen des Soft-Caps
                if (state.IsSoftCapActive && !HeaderVM.IsSoftCapActive)
                {
                    FloatingTextRequested?.Invoke(
                        _localizationService.GetString("SoftCapReached") ?? "Bonus cap reached!",
                        "warning");
                }

                HeaderVM.IsSoftCapActive = state.IsSoftCapActive;
                if (state.IsSoftCapActive && state.SoftCapReductionPercent > 0)
                {
                    // GAM-6: Differenzierter Text mit "Einkommen" Prefix
                    var incomeLabel = _localizationService.GetString("SoftCapIncome") ?? "Income";
                    HeaderVM.SoftCapText = $"{incomeLabel} -{state.SoftCapReductionPercent}%";
                }
                else if (!state.IsSoftCapActive)
                    HeaderVM.SoftCapText = "";

                // GAM-4: Saisonaler Indikator wird in UpdateEventChips() aktualisiert (SeasonalModifierText)
            }
        }

        // Arbeitsmarkt Rotations-Timer jede Sekunde aktualisieren
        if (IsWorkerMarketActive)
        {
            WorkerMarketViewModel.UpdateTimer();
        }

        // WorkerProfile-Fortschritt aktualisieren (Training/Rest-Balken in Echtzeit)
        if (IsWorkerProfileActive && _floatingTextCounter % 3 == 0)
        {
            WorkerProfileViewModel.RefreshDisplayProperties();
        }
    }

    /// <summary>
    /// Aktualisiert das "Naechstes Ziel"-Banner (delegiert an <see cref="GoalBannerVM"/>).
    /// </summary>
    private void RefreshCurrentGoal() => GoalBannerVM.Refresh();

    /// <summary>
    /// Prüft ob beim neuen Level ein Tab freigeschaltet wird und zeigt einen Hinweis.
    /// </summary>
    private void CheckTabUnlockNotification(int newLevel)
    {
        string[] tabNames = [
            _localizationService.GetString("TabWerkstatt") ?? "Workshop",
            _localizationService.GetString("TabImperium") ?? "Empire",
            _localizationService.GetString("TabMissionen") ?? "Missions",
            _localizationService.GetString("TabGilde") ?? "Guild",
            _localizationService.GetString("TabShop") ?? "Shop"
        ];

        for (int i = 0; i < TabUnlockLevels.Length; i++)
        {
            if (TabUnlockLevels[i] == newLevel)
            {
                var unlockText = string.Format(
                    _localizationService.GetString("TabUnlocked") ?? "{0} freigeschaltet!",
                    tabNames[i]);
                FloatingTextRequested?.Invoke(unlockText, "golden_screws");
            }
        }
    }
}
