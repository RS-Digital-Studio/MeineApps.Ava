using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using System.Net.Http;

namespace BingXBot.ViewModels;

/// <summary>
/// Teil von <see cref="DashboardViewModel"/>: Account-/Equity-Synchronisation.
/// Local-Mode: periodische Account-Update- und Equity-Snapshot-Timer, Rolling-Metriken,
/// Trade-Marker-Pflege. Remote-Mode: SignalR-Push-Handler + Account-Polling-Loop vom Pi.
/// Reiner Struktur-Split — kein Verhaltensunterschied zur monolithischen Datei.
/// </summary>
public partial class DashboardViewModel
{
    /// <summary>
    /// Benannter Handler für TradeCompleted (statt anonymem Lambda, damit -= in Dispose möglich).
    /// </summary>
    private void OnTradeCompletedForMarkers(object? sender, CompletedTrade trade)
    {
        // Rolling-Metriken sofort aktualisieren (nicht 5 Min warten)
        if (IsRunning)
            UpdateRollingMetrics();

        if (trade.Symbol != "BTC-USDT") return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            BtcTicker.TradeMarkers.Add(new TradeMarker(trade.EntryTime, trade.EntryPrice, trade.Side, true));
            BtcTicker.TradeMarkers.Add(new TradeMarker(trade.ExitTime, trade.ExitPrice, trade.Side, false, trade.Pnl));
            // Max 50 Marker behalten
            while (BtcTicker.TradeMarkers.Count > 50)
                BtcTicker.TradeMarkers.RemoveAt(0);
        });
    }

    /// <summary>
    /// Aktualisiert Account-Daten und offene Positionen alle 5 Sekunden.
    /// Nutzt je nach Modus die SimulatedExchange (Paper) oder den echten BingXRestClient (Live).
    /// </summary>
    private async Task StartAccountUpdateAsync()
    {
        // Im Remote-Modus laeuft die Engine server-seitig — Account-Updates kommen via SignalR-Push.
        // Ein lokaler Polling-Timer hat nichts zu tun und wuerde nur Paper/Live-Services-Pfade
        // triggern, die keine Daten haben.
        if (IsRemoteMode) return;

        _accountUpdateCts?.Cancel();
        _accountUpdateCts?.Dispose();
        _accountUpdateCts = new CancellationTokenSource();
        var ct = _accountUpdateCts.Token;

        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (_accountUpdateTimer != null && await _accountUpdateTimer.WaitForNextTickAsync(ct))
            {
                if (!IsRunning) continue;

                try
                {
                    AccountInfo? account = null;
                    IReadOnlyList<Position>? positions = null;

                    if (!IsPaperMode && _liveManager.RestClient != null)
                    {
                        // Live-Modus: Echte Daten von BingX
                        account = await _liveManager.RestClient!.GetAccountInfoAsync();
                        positions = await _liveManager.RestClient!.GetPositionsAsync();
                    }
                    else if (IsPaperMode && _paperService.Exchange != null)
                    {
                        // Paper Single-Mode: Simulierte Daten
                        account = await _paperService.Exchange.GetAccountInfoAsync();
                        positions = await _paperService.Exchange.GetPositionsAsync();
                    }

                    if (account == null) continue;

                    var acct = account;
                    var pos = positions ?? Array.Empty<Position>();
                    var isPaper = IsPaperMode;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        // Paper: Balance ist Equity (Wallet + Unrealisiert) → Wallet extrahieren
                        // Live: BingX liefert Wallet-Balance direkt im "balance" Feld
                        var walletBalance = acct.Balance - acct.UnrealizedPnl;
                        Balance = isPaper ? walletBalance : acct.Balance;
                        AvailableBalance = acct.AvailableBalance;
                        UnrealizedPnl = acct.UnrealizedPnl;
                        // TotalPnl: Paper = Equity - Startkapital, Live = Realisiert + Unrealisiert
                        TotalPnl = isPaper
                            ? acct.Balance - _botSettings.PaperInitialBalance
                            : acct.RealizedPnl + acct.UnrealizedPnl;

                        // Inkrementell: bestehende Items updaten, nur für neue CreatePositionItem aufrufen
                        UpdatePositionsFromData(pos);
                        UpdatePositionsStatus();
                    });
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Dashboard",
                        $"Account-Update Fehler: {ex.Message}"));

                    // Im Live-Modus bei Verbindungsproblemen Warnung zeigen
                    if (!IsPaperMode)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            LiveStatusText = $"Fehler: {ex.Message}";
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { } // Timer wurde disposed während WaitForNextTickAsync
    }

    /// <summary>
    /// Startet periodische Equity-Snapshots (alle 5 Minuten) in die DB.
    /// </summary>
    private async Task StartEquitySnapshotTimerAsync()
    {
        // Im Remote-Modus wird Equity serverseitig getrackt — kein lokaler Timer noetig.
        if (IsRemoteMode) return;
        if (_dbService == null) return;

        StopEquitySnapshotTimer();
        _equityCts = new CancellationTokenSource();
        _equityTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            // Ersten Snapshot sofort speichern
            await SaveEquitySnapshotAsync();

            // Eigener CTS: Unabhängig von _accountUpdateCts, wird bei StopBot/Dispose gecancelt
            while (await _equityTimer.WaitForNextTickAsync(_equityCts.Token))
                await SaveEquitySnapshotAsync();
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { } // Timer wurde disposed bevor CancellationToken feuerte
    }

    /// <summary>
    /// Speichert einen einzelnen Equity-Snapshot in der DB.
    /// Wird alle 5 Minuten aufgerufen (Paper + Live). Läuft nur im Desktop-Standalone-/Local-Mode
    /// (Server hat kein DashboardViewModel). `EventBus.PublishEquity` bleibt bewusst bei
    /// `PaperTradingService.PublishNewTrades` — doppeltes Publishing vom Dashboard aus wäre
    /// im Standalone-Modus nur lokaler Lärm ohne Abnehmer und im Server-Modus läuft der Code
    /// ohnehin nicht. Remote-Equity-Kurve erfordert einen HostedService-basierten Tracker.
    /// </summary>
    private async Task SaveEquitySnapshotAsync()
    {
        if (_dbService == null || !HasAccountData) return;
        try
        {
            var point = new EquityPoint(DateTime.UtcNow, Balance + UnrealizedPnl);
            await _dbService.SaveEquitySnapshotAsync(point);

            // Auch in die ObservableCollection für das UI (max 500 Punkte, älteste entfernen)
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EquityData.Add(point);
                while (EquityData.Count > 500)
                    EquityData.RemoveAt(0);
            });

            // Rolling-Metriken vom RiskManager aktualisieren
            UpdateRollingMetrics();
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Dashboard",
                $"Equity-Snapshot speichern fehlgeschlagen: {ex.Message}"));
        }
    }

    /// <summary>Aktualisiert Rolling-Metriken + Widget-Daten aus dem aktiven Trading-Service.</summary>
    private void UpdateRollingMetrics()
    {
        // RiskManager des aktiven Services (Multi-TF Standalone: ein Service)
        Engine.Risk.RiskManager? rm;
        if (IsPaperMode)
            rm = _paperService.RiskManager;
        else
            rm = _liveManager.Service?.RiskManager;

        if (rm == null) return;

        // Daten auf dem Timer-Thread vorbereiten (Snapshots erstellen, nicht das Dictionary direkt mutieren)
        var winRate = rm.RollingWinRate * 100m;
        var sharpe = rm.RollingSharpeRatio;
        var profitFactor = rm.RollingProfitFactor;
        var health = rm.CheckStrategyHealth();

        // DailyPnl aus Trades berechnen (Snapshot auf Timer-Thread, Zuweisung auf UI-Thread)
        var dailyPnlSnapshot = BuildDailyPnlSnapshot(rm);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RollingWinRate = winRate;
            RollingSharpe = sharpe;
            RollingProfitFactor = profitFactor;
            HasStrategyWarning = health != null;
            StrategyHealthText = health ?? "OK";

            DailyPnl = dailyPnlSnapshot.ToDictionary(x => x.Day, x => x.Pnl);
            WidgetCanvasInvalidationRequested?.Invoke();
        });
    }

    /// <summary>Erstellt einen Snapshot der täglichen PnL aus RiskManager-Trades (thread-safe).</summary>
    private static List<(DateTime Day, decimal Pnl)> BuildDailyPnlSnapshot(Engine.Risk.RiskManager rm)
    {
        try
        {
            var result = new Dictionary<DateTime, decimal>();
            foreach (var trade in rm.RecentTrades)
            {
                var day = trade.ExitTime.Date;
                result.TryGetValue(day, out var existing);
                result[day] = existing + trade.Pnl;
            }
            return result.Select(kv => (kv.Key, kv.Value)).ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Stoppt den Equity-Snapshot-Timer.
    /// </summary>
    private void StopEquitySnapshotTimer()
    {
        _equityCts?.Cancel();
        _equityCts?.Dispose();
        _equityCts = null;
        _equityTimer?.Dispose();
        _equityTimer = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // Remote-Mode Event-Handler (Client/Server-Architektur)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Remote: Position wurde updated (SignalR-Push vom Server).</summary>
    private void OnRemotePositionUpdated(BingXBot.Contracts.Dto.PositionDto pos)
    {
        // SignalR-Callback kann auf beliebigem Thread feuern → UI-Operationen MÜSSEN marshalled werden.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(OpenPositions));
        });
    }

    /// <summary>Remote: Equity-Snapshot vom Server (SignalR-Push).</summary>
    private void OnRemoteEquityUpdate(BingXBot.Contracts.Dto.EquityPointDto pt)
    {
        // SignalR-Callback → UI-Thread-Marshalling für Property-Setter mit Bindings.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Balance = pt.Equity;
        });
    }

    /// <summary>Remote: Bot-Status-Change (Started/Stopped/Paused). Übernimmt auch Paper/Live-Modus vom Pi.</summary>
    private void OnRemoteStatusChanged(BingXBot.Contracts.Dto.BotStatusDto status)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var running = status.State == BotState.Running;
            IsRunning = running;
            CanStart = !running;  // Start-Button deaktivieren wenn Bot auf Pi bereits läuft
            BotStatusText = running
                ? (status.Mode == Core.Enums.TradingMode.Paper ? "Paper (Remote)" : "LIVE (Remote) - Handelt aktiv!")
                : status.State.ToString();
            BotStatusState = status.State;

            // BotEventBus feuern — damit MainViewModel.TradingMode + Statusleiste synchron bleiben
            _eventBus.PublishBotState(status.State);

            // Modus vom Server übernehmen (Paper/Live) — Statusleiste + interne Flags konsistent halten
            var isPaper = status.Mode == Core.Enums.TradingMode.Paper;
            if (IsPaperMode != isPaper)
            {
                IsPaperMode = isPaper;
                ModeText = isPaper ? "Paper-Modus" : "Live-Modus";
            }
            // Fix 17.04.2026: Client-lokalen BotSettings.LastMode auf Server-Authority syncen,
            // damit SettingsPersistenceService.SaveAllAsync den aktuellen Mode mitsendet und
            // nicht versehentlich den Default (Paper) ueberschreibt.
            _botSettings.LastMode = status.Mode;
            IsLiveActive = !isPaper && running;
            _eventBus.PublishTradingMode(isPaper);

            // Welcome-Hint ausblenden wenn Bot aktiv
            if (running) ShowWelcomeHint = false;
        });
    }

    /// <summary>Remote: Polling-Loop für Account-Snapshot + Status (alle 5s).</summary>
    private async Task StartRemoteAccountPollingAsync()
    {
        _remoteAccountPollCts = new CancellationTokenSource();
        var ct = _remoteAccountPollCts.Token;

        // Initialen Status sofort holen (nicht auf ersten SignalR-Push warten)
        try
        {
            var initialStatus = await _botControl.GetStatusAsync(ct).ConfigureAwait(false);
            if (initialStatus != null) OnRemoteStatusChanged(initialStatus);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Remote",
                $"Initialer Status-Call fehlgeschlagen (Retry im Poll): {ex.Message}"));
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var snap = await _accountService.GetSnapshotAsync(ct).ConfigureAwait(false);
                    if (snap != null)
                    {
                        // Property-Setter triggern PropertyChanged + abgeleitete PnL-Farb-Bindings —
                        // MUSS auf dem UI-Thread laufen (der Loop laeuft per ConfigureAwait(false) auf
                        // einem ThreadPool-Thread). Off-UI-Thread-Mutation gebundener Controls crasht
                        // auf Android (Hauptbetriebsmodus). Analog zum Positions-Block direkt darunter.
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            Balance = snap.Balance;
                            AvailableBalance = snap.Available;
                            UnrealizedPnl = snap.UnrealizedPnl;
                            TotalPnl = snap.RealizedPnlToday;  // heute realisierter PnL
                            // "Bot starten um Account-Daten zu sehen"-Hinweis ausblenden sobald echte Daten da sind
                            if (Balance > 0 || AvailableBalance > 0) HasAccountData = true;
                        });
                    }

                    // Status ebenfalls im Poll-Zyklus nachziehen (deckt verpasste SignalR-Pushes ab)
                    var status = await _botControl.GetStatusAsync(ct).ConfigureAwait(false);
                    if (status != null) OnRemoteStatusChanged(status);

                    // Offene Positionen holen — SignalR pusht nur bei Änderung, beim App-Start sind sonst keine da
                    var positions = await _accountService.GetPositionsAsync(ct).ConfigureAwait(false);
                    if (positions != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            // Nur neu aufbauen wenn Anzahl oder Keys unterschiedlich sind (verhindert Flackern)
                            var newKeys = positions.Select(p => $"{p.Symbol}_{p.Side}").OrderBy(k => k).ToList();
                            var oldKeys = OpenPositions.Select(p => $"{p.Symbol}_{p.Side}").OrderBy(k => k).ToList();
                            if (!newKeys.SequenceEqual(oldKeys))
                            {
                                OpenPositions.Clear();
                                foreach (var p in positions)
                                {
                                    OpenPositions.Add(new PositionDisplayItem
                                    {
                                        Symbol = p.Symbol,
                                        Side = p.Side,
                                        EntryPrice = p.EntryPrice,
                                        MarkPrice = p.MarkPrice,
                                        Quantity = p.Quantity,
                                        Pnl = p.UnrealizedPnl,
                                        Leverage = p.Leverage,
                                        StopLoss = p.StopLoss,
                                        TakeProfit = p.TakeProfit,
                                        LiquidationPrice = p.LiquidationPrice ?? 0m
                                    });
                                }
                                HasOpenPositions = OpenPositions.Count > 0;
                            }
                            else
                            {
                                // Gleiche Keys — nur Preise/PnL aktualisieren (keine Clear/Add)
                                foreach (var p in positions)
                                {
                                    var item = OpenPositions.FirstOrDefault(x => x.Symbol == p.Symbol && x.Side == p.Side);
                                    if (item == null) continue;
                                    item.MarkPrice = p.MarkPrice;
                                    item.Pnl = p.UnrealizedPnl;
                                    item.StopLoss = p.StopLoss;
                                    item.TakeProfit = p.TakeProfit;
                                }
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (ex is HttpRequestException or TimeoutException or TaskCanceledException)
                {
                    // Nur Netzwerk-Fehler schlucken — echte Bugs sollen durchschlagen
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Remote",
                        $"Poll-Fehler, Retry in 5s: {ex.Message}"));
                }

                await Task.Delay(5000, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* Stopp */ }
    }

    /// <summary>
    /// Stoppt die Remote-Account-Polling-Schleife (cancelt das CTS, Loop bricht beim naechsten
    /// <c>Task.Delay</c>/REST-Call ab). Idempotent — kein Effekt wenn nichts laeuft.
    /// </summary>
    private void StopRemoteAccountPolling()
    {
        _remoteAccountPollCts?.Cancel();
        _remoteAccountPollCts?.Dispose();
        _remoteAccountPollCts = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // App-Lifecycle (Akku): Client-seitige Polls/Timer im Hintergrund stoppen
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// App ging in den Hintergrund: die client-seitigen Poll-Loops und Timer anhalten (Akku).
    /// Der Bot laeuft autonom auf dem Pi weiter — die App ist nur Monitor, im Hintergrund wird
    /// nichts angezeigt, also ist kein Poll noetig. Im Vordergrund liefert zusaetzlich SignalR
    /// Echtzeit-Updates; der Poll ist nur Lueckenfueller. Local-Mode (Desktop) erreicht diesen
    /// Pfad nicht (kein Broker injiziert).
    /// </summary>
    private void OnAppPaused()
    {
        // Remote-Account-Poll-Schleife (3 REST-Calls/5s) stoppen.
        if (IsRemoteMode)
            StopRemoteAccountPolling();

        // Stats-Breakdown-Timer (30s) stoppen.
        _statsRefreshTimer?.Stop();
    }

    /// <summary>
    /// App kam zurueck in den Vordergrund: Poll-Loop und Timer wieder anwerfen — mit sofortigem
    /// erstem Poll, damit die Anzeige beim Wiedereintritt sofort frisch ist.
    /// </summary>
    private void OnAppResumed()
    {
        if (_disposed) return;

        // Remote-Account-Poll-Schleife neu starten (StartRemoteAccountPollingAsync holt sofort
        // initialen Status + Snapshot, bevor der 5s-Zyklus beginnt → Daten sofort frisch).
        if (IsRemoteMode)
        {
            StopRemoteAccountPolling(); // sicherheitshalber Alt-Loop beenden (Doppellauf vermeiden)
            _ = StartRemoteAccountPollingAsync();
        }

        // Stats-Breakdown-Timer wieder starten + sofortiger Refresh.
        if (_statsRefreshTimer != null)
        {
            _statsRefreshTimer.Start();
            _ = RefreshStatsAsync();
        }
    }
}
