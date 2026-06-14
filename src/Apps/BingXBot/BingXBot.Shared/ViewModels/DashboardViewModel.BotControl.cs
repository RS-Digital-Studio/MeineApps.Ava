using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using CommunityToolkit.Mvvm.Input;

namespace BingXBot.ViewModels;

/// <summary>
/// Teil von <see cref="DashboardViewModel"/>: Bot-Steuerung (Start/Stop/Pause/Notfall-Stop)
/// für Paper-, Live- und Remote-Modus, Modus-Umschaltung sowie der Bestätigungs-Dialog.
/// Reiner Struktur-Split — kein Verhaltensunterschied zur monolithischen Datei.
/// </summary>
public partial class DashboardViewModel
{
    [RelayCommand]
    private async Task StartBot()
    {
        // Remote-Modus: Engine laeuft auf dem Pi — Start per HTTP delegieren
        if (IsRemoteMode)
        {
            if (!IsPaperMode)
            {
                ConfirmDialogTitle = "Live-Trading auf Server starten?";
                ConfirmDialogMessage = "Der Pi-Server wird mit ECHTEM GELD handeln.\n\nStelle sicher, dass dein Risikomanagement korrekt konfiguriert ist.";
                _confirmDialogAction = StartRemoteAsync;
                ShowConfirmDialog = true;
                return;
            }
            await StartRemoteAsync();
            return;
        }

        if (IsPaperMode)
        {
            await StartPaperTradingAsync();
        }
        else
        {
            ConfirmDialogTitle = "Live-Trading starten?";
            ConfirmDialogMessage = "Du bist dabei, den Bot mit ECHTEM GELD zu starten.\n\nDer Bot wird automatisch Trades auf BingX eröffnen und schließen. Stelle sicher, dass dein Risikomanagement korrekt konfiguriert ist.";
            _confirmDialogAction = StartLiveTradingAsync;
            ShowConfirmDialog = true;
        }
    }

    /// <summary>Remote-Start (Server uebernimmt die komplette Orchestrierung).</summary>
    private async Task StartRemoteAsync()
    {
        try
        {
            BotStatusText = "Sende Start-Request an Pi...";
            BotStatusState = BotState.Starting;
            CanStart = false;

            var requestedMode = IsPaperMode ? Core.Enums.TradingMode.Paper : Core.Enums.TradingMode.Live;
            var req = new BingXBot.Contracts.Dto.BotStartRequest(
                Mode: requestedMode,
                InitialBalance: IsPaperMode ? _botSettings.PaperInitialBalance : null,
                ActiveTimeframes: _scannerSettings.ActiveTimeframes.ToList(),
                Engine: IsCrossSectional ? Core.Enums.EngineMode.CrossSectional : Core.Enums.EngineMode.Scalper);

            var status = await _botControl.StartAsync(req);

            // Server kann den Start ablehnen wenn Engine bereits in anderem Mode laeuft — dann ist
            // LastError gesetzt und status.Mode bleibt auf dem alten Wert. In dem Fall das UI NICHT
            // optimistisch als Running + requestedMode anzeigen, sondern den echten Zustand + Fehler.
            if (!string.IsNullOrEmpty(status.LastError))
            {
                BotStatusText = $"Start abgelehnt: {status.LastError}";
                BotStatusState = status.State;
                CanStart = status.State != BotState.Running;
                // IsPaperMode dem Server-Ist-Zustand angleichen — sonst zeigt die UI weiter Live an
                // obwohl der Server in Paper festhaengt.
                IsPaperMode = status.Mode != Core.Enums.TradingMode.Live;
                return;
            }

            // Erfolgreicher Start: Server-Response ist die Wahrheit, nicht der Client-Toggle.
            // Wenn User Paper angefordert hat und Server hat Paper gestartet -> alles gut.
            // Wenn User Live angefordert hat und Server meldet Paper (abgelehnt ohne LastError?) -> UI syncen.
            var serverMode = status.Mode;
            IsPaperMode = serverMode != Core.Enums.TradingMode.Live;
            IsRunning = true;
            CanStart = false;
            BotStatusState = status.State;
            BotStatusText = status.State == BotState.Running
                ? (IsPaperMode ? "Paper (Remote)" : "LIVE (Remote) - Handelt aktiv!")
                : status.State.ToString();
            ShowWelcomeHint = false;
            if (!IsPaperMode) { LiveStatusText = "Handelt aktiv (Remote)"; IsLiveActive = true; }
        }
        catch (Exception ex)
        {
            BotStatusText = $"Start fehlgeschlagen: {ex.Message}";
            BotStatusState = BotState.Error;
            CanStart = true;
        }
    }

    /// <summary>
    /// Startet den Paper-Trading-Modus mit simuliertem Kapital.
    /// </summary>
    private async Task StartPaperTradingAsync()
    {
        // Strategie aktivieren (Multi-TF Standalone: kein Preset)
        var strategy = StrategyFactory.Create(SelectedStrategy);
        _strategyManager.SetStrategy(strategy);

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Strategie: {SelectedStrategy}"));

        // Paper-Trading unterstützt immer Hedge-Modus (SimulatedExchange erlaubt Long+Short)
        // Ohne dieses Flag werden TradFi-Symbole (Commodities, Stocks, Indices, Forex) komplett ignoriert
        _scannerSettings.IsHedgeModeActive = true;

        // Paper-Trading-Service starten
        _paperService.Start(_botSettings.PaperInitialBalance);

        IsRunning = true;
        CanStart = false;
        BotStatusText = "Läuft (Paper)";
        BotStatusState = BotState.Running;
        ShowWelcomeHint = false;

        // Account-Daten anzeigen
        HasAccountData = true;
        Balance = _botSettings.PaperInitialBalance;
        AvailableBalance = _botSettings.PaperInitialBalance;
        UnrealizedPnl = 0m;
        TotalPnl = 0m;

        // Equity-Snapshots alle 5 Minuten in DB persistieren
        _ = StartEquitySnapshotTimerAsync();

        // Account-Update Timer starten (alle 5 Sekunden)
        _accountUpdateTask = StartAccountUpdateAsync();
    }

    /// <summary>
    /// Startet den Live-Trading-Modus über den LiveTradingManager (Multi-TF Standalone seit 15.04.2026 —
    /// ein Service scannt alle aktiven Navigator-Timeframes D1/H4/H1/M15 parallel pro Symbol).
    /// </summary>
    private async Task StartLiveTradingAsync()
    {
        BotStatusText = "Verbinde mit BingX...";
        BotStatusState = BotState.Starting;
        CanStart = false;

        try
        {
            var result = await _liveManager.ConnectAsync();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Balance = result.Account.Balance;
                AvailableBalance = result.Account.AvailableBalance;
                UnrealizedPnl = result.Account.UnrealizedPnl;
                TotalPnl = result.Account.RealizedPnl + result.Account.UnrealizedPnl;
                HasAccountData = true;

                OpenPositions.Clear();
                foreach (var p in result.Positions)
                    OpenPositions.Add(CreatePositionItem(p));
                UpdatePositionsStatus();
            });

            await _liveManager.StartAsync(SelectedStrategy);

            if (result.Positions.Count > 0)
                await _liveManager.RestorePositionSignalsAsync(result.Positions);

            BotStatusText = "LIVE - Handelt aktiv!";

            IsRunning = true;
            CanStart = false;
            BotStatusState = BotState.Running;
            ShowWelcomeHint = false;
            LiveStatusText = "Handelt aktiv";
            IsLiveActive = true;

            _ = StartEquitySnapshotTimerAsync();
            _accountUpdateTask = StartAccountUpdateAsync();
        }
        catch (Exception ex)
        {
            BotStatusText = ex.Message.Contains("API-Keys") ? ex.Message : "Verbindung fehlgeschlagen";
            BotStatusState = BotState.Error;
            CanStart = true;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                $"Live-Trading fehlgeschlagen: {ex.Message}"));
        }
    }

    [RelayCommand]
    private void PauseBot()
    {
        if (!IsPaperMode && _liveManager.Service != null)
        {
            // Live-Modus: Pause/Resume über LiveTradingService (Multi-TF Standalone)
            if (_liveManager.Service!.IsPaused)
            {
                _liveManager.Service!.Resume();
                BotStatusText = "LIVE - Handelt aktiv!";
                BotStatusState = BotState.Running;
                _accountUpdateTask = StartAccountUpdateAsync();
            }
            else
            {
                _liveManager.Service!.Pause();
                _accountUpdateTimer?.Dispose();
                _accountUpdateTimer = null;
                BotStatusText = "LIVE - Pausiert";
                BotStatusState = BotState.Paused;
            }
            return;
        }

        if (_paperService.IsPaused)
        {
            // Resume
            _paperService.Resume();
            BotStatusText = "Läuft (Paper)";
            BotStatusState = BotState.Running;
        }
        else
        {
            // Pause
            _paperService.Pause();
            BotStatusText = "Pausiert";
            BotStatusState = BotState.Paused;
        }
    }

    [RelayCommand]
    private async Task StopBot()
    {
        _accountUpdateCts?.Cancel();
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        // Remote-Modus: Stop per HTTP delegieren
        if (IsRemoteMode)
        {
            try { await _botControl.StopAsync(); }
            catch (Exception ex) { BotStatusText = $"Stop fehlgeschlagen: {ex.Message}"; return; }
            IsRunning = false;
            CanStart = true;
            BotStatusText = "Gestoppt (Remote)";
            BotStatusState = BotState.Stopped;
            PositionsStatusText = "Keine offenen Positionen";
            LiveStatusText = "Getrennt";
            IsLiveActive = false;
            return;
        }

        if (IsPaperMode)
        {
            await _paperService.StopAsync();
        }
        else
        {
            if (_liveManager.IsRunning)
                await _liveManager.StopAsync();
            LiveStatusText = "Getrennt";
            IsLiveActive = false;
        }

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Gestoppt";
        BotStatusState = BotState.Stopped;
        PositionsStatusText = "Keine offenen Positionen";
    }

    [RelayCommand]
    private async Task EmergencyStop()
    {
        // Live-Modus: Bestätigung erforderlich (schließt ALLE echten Positionen!)
        if (!IsPaperMode && IsLiveActive)
        {
            ConfirmDialogTitle = "NOTFALL-STOP ausführen?";
            ConfirmDialogMessage = "ALLE echten Positionen auf BingX werden SOFORT geschlossen!\n\nDies kann nicht rückgängig gemacht werden.";
            _confirmDialogAction = ExecuteEmergencyStopAsync;
            ShowConfirmDialog = true;
            return;
        }

        await ExecuteEmergencyStopAsync();
    }

    private async Task ExecuteEmergencyStopAsync()
    {
        _accountUpdateCts?.Cancel();
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        // Remote-Modus: EmergencyStop per HTTP delegieren (Server schliesst ALLE Positionen serverseitig)
        if (IsRemoteMode)
        {
            try { await _botControl.EmergencyStopAsync(); }
            catch (Exception ex) { BotStatusText = $"Notfall-Stop fehlgeschlagen: {ex.Message}"; return; }
            IsRunning = false;
            CanStart = true;
            BotStatusText = "Notfall-Stop (Remote)";
            BotStatusState = BotState.EmergencyStop;
            LiveStatusText = "Notfall-Stop";
            IsLiveActive = false;
            OpenPositions.Clear();
            HasOpenPositions = false;
            PositionsStatusText = "Alle Positionen geschlossen";
            return;
        }

        if (IsPaperMode)
        {
            await _paperService.EmergencyStopAsync();
        }
        else
        {
            if (_liveManager.IsRunning)
                await _liveManager.EmergencyStopAsync();
            LiveStatusText = "Notfall-Stop";
            IsLiveActive = false;
        }

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Notfall-Stop ausgeführt";
        BotStatusState = BotState.Error;

        // Positionen aus UI entfernen
        OpenPositions.Clear();
        HasOpenPositions = false;
        PositionsStatusText = "Alle Positionen geschlossen";
    }

    [RelayCommand]
    private void ToggleMode()
    {
        if (IsRunning)
        {
            // Kann Modus nicht wechseln waehrend Bot laeuft
            return;
        }

        IsPaperMode = !IsPaperMode;
        if (IsPaperMode)
        {
            ModeText = "Paper-Modus";
            ModeDescription = "Simuliertes Trading ohne echtes Geld";
        }
        else
        {
            // API-Key-Status aktualisieren
            HasApiKeys = _secureStorage?.HasCredentials ?? false;
            ModeText = "Live-Modus";
            ModeDescription = HasApiKeys
                ? "Echtes Trading mit BingX - Handelt automatisch!"
                : "API-Keys erforderlich! Gehe zu Einstellungen.";
        }

        // Account-Daten zuruecksetzen bei Modus-Wechsel
        HasAccountData = false;
        Balance = 0;
        AvailableBalance = 0;
        UnrealizedPnl = 0;
        TotalPnl = 0;
        IsLiveActive = false;

        // MainViewModel über Modus-Wechsel informieren (Statusleiste unten rechts)
        _eventBus.PublishTradingMode(IsPaperMode);

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Modus gewechselt zu: {ModeText}"));
    }

    [RelayCommand]
    private void ToggleActivityExpanded()
    {
        IsActivityExpanded = !IsActivityExpanded;
    }

    [RelayCommand]
    private void DismissWelcomeHint()
    {
        ShowWelcomeHint = false;
    }

    /// <summary>Bestätigungs-Dialog: Ja → Aktion ausführen.</summary>
    [RelayCommand]
    private async Task ConfirmDialogYes()
    {
        ShowConfirmDialog = false;
        if (_confirmDialogAction != null)
            await _confirmDialogAction();
        _confirmDialogAction = null;
    }

    /// <summary>Bestätigungs-Dialog: Abbrechen.</summary>
    [RelayCommand]
    private void ConfirmDialogCancel()
    {
        ShowConfirmDialog = false;
        _confirmDialogAction = null;
    }
}
