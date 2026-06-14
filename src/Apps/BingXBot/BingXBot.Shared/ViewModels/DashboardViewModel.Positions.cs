using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BingXBot.ViewModels;

/// <summary>
/// Teil von <see cref="DashboardViewModel"/>: Verwaltung der offenen Positionen —
/// Schließen (einzeln/alle), Erstellung der Display-Items inkl. SL/TP-Verdrahtung,
/// inkrementelles Update aus Positions-Daten, Auswahl + Chart-Overlay.
/// Reiner Struktur-Split — kein Verhaltensunterschied zur monolithischen Datei.
/// </summary>
public partial class DashboardViewModel
{
    // Ausgewählte Position für Chart-Overlay
    [ObservableProperty] private PositionDisplayItem? _selectedPosition;

    /// <summary>
    /// Zeigt den Bestätigungs-Dialog bevor eine einzelne Position geschlossen wird.
    /// </summary>
    [RelayCommand]
    private void RequestClosePosition(PositionDisplayItem? position)
    {
        if (position == null) return;

        var pnlText = position.Pnl >= 0 ? $"+{position.Pnl:N2}" : $"{position.Pnl:N2}";
        ConfirmDialogTitle = "Position schließen?";
        ConfirmDialogMessage = $"{position.Symbol} ({position.Side}, {position.Leverage}x)\nPnL: {pnlText} USDT ({position.PnlPercentText})";
        _confirmDialogAction = async () => await ExecuteClosePosition(position);
        ShowConfirmDialog = true;
    }

    /// <summary>
    /// Führt das Schließen einer Position tatsächlich aus (nach Bestätigung).
    /// </summary>
    private async Task ExecuteClosePosition(PositionDisplayItem position)
    {
        var side = position.Side;

        try
        {
            if (IsPaperMode && _paperService.Exchange != null)
            {
                _paperService.Exchange.SetCurrentPrice(position.Symbol, position.MarkPrice);
                await _paperService.Exchange.ClosePositionAsync(position.Symbol, side);
                _paperService.RemovePositionSignal(position.Symbol, side);
            }
            else if (!IsPaperMode && _liveManager.RestClient != null)
            {
                // Position-Daten für CompletedTrade merken
                var entryPrice = position.EntryPrice;
                var exitPrice = position.MarkPrice;
                var qty = position.Quantity;

                await _liveManager.RestClient!.ClosePositionAsync(position.Symbol, side);

                // Signal entfernen (Multi-TF Standalone: ein Service reicht)
                {
                    _liveManager.Service?.RemovePositionSignal(position.Symbol, side);
                }

                // CompletedTrade erstellen damit RiskManager Feedback bekommt
                // Echte Commission-Rate vom BingX-Account (je nach VIP-Level 0.02%-0.075%)
                var feeRate = _liveManager.CommissionTakerRate;
                var fee = qty * entryPrice * feeRate + qty * exitPrice * feeRate;
                var rawPnl = side == Side.Buy
                    ? (exitPrice - entryPrice) * qty
                    : (entryPrice - exitPrice) * qty;
                var trade = new CompletedTrade(position.Symbol, side, entryPrice, exitPrice,
                    qty, rawPnl - fee, fee, DateTime.UtcNow, DateTime.UtcNow,
                    "Manuell geschlossen", Core.Enums.TradingMode.Live);
                _eventBus.PublishTrade(trade);
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                $"{position.Symbol}: Position manuell geschlossen ({position.Side})", position.Symbol));

            OpenPositions.Remove(position);
            UpdatePositionsStatus();
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Trade",
                $"{position.Symbol}: Schließen fehlgeschlagen - {ex.Message}", position.Symbol));
        }
    }

    /// <summary>
    /// Zeigt den Bestätigungs-Dialog bevor alle Positionen geschlossen werden.
    /// </summary>
    [RelayCommand]
    private void CloseAllPositions()
    {
        if (OpenPositions.Count == 0) return;

        var totalPnl = OpenPositions.Sum(p => p.Pnl);
        var pnlText = totalPnl >= 0 ? $"+{totalPnl:N2}" : $"{totalPnl:N2}";
        ConfirmDialogTitle = "Alle Positionen schließen?";
        ConfirmDialogMessage = $"{OpenPositions.Count} offene Position(en)\nGesamter unrealisierter PnL: {pnlText} USDT";
        _confirmDialogAction = async () =>
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Trade",
                $"Schließe alle {OpenPositions.Count} Positionen..."));

            var positionsCopy = OpenPositions.ToList();
            foreach (var pos in positionsCopy)
                await ExecuteClosePosition(pos);
        };
        ShowConfirmDialog = true;
    }

    /// <summary>
    /// Sucht das Signal für eine Position im aktiven Service-Modus
    /// (Paper-Service oder Live-Service — Multi-TF Standalone seit 15.04.2026,
    /// MultiModeOrchestrator ist entfernt).
    /// </summary>
    private SignalResult? FindPositionSignal(string symbol, Side side)
    {
        if (IsPaperMode)
            return _paperService.GetPositionSignal(symbol, side);
        return _liveManager.Service?.GetPositionSignal(symbol, side);
    }

    private DateTime? FindEntryTime(string symbol, Side side)
    {
        if (IsPaperMode)
            return _paperService.GetEntryTime(symbol, side);
        return _liveManager.Service?.GetEntryTime(symbol, side);
    }

    /// <summary>Wählt eine Position aus und zeigt ihren Chart-Overlay an.</summary>
    [RelayCommand]
    private async Task SelectPosition(PositionDisplayItem? pos)
    {
        // Alte Auswahl deselektieren
        if (SelectedPosition != null)
            SelectedPosition.IsSelected = false;

        if (pos == null || pos == SelectedPosition)
        {
            // Deselektieren → zurück zu BTC
            SelectedPosition = null;
            await BtcTicker.SwitchSymbolCommand.ExecuteAsync("BTC-USDT");
            UpdateChartOverlay();
            return;
        }

        SelectedPosition = pos;
        pos.IsSelected = true;

        // Chart auf das Symbol der Position wechseln
        await BtcTicker.SwitchSymbolCommand.ExecuteAsync(pos.Symbol);

        // Position-Overlay (Entry/SL/TP Linien)
        var signal = FindPositionSignal(pos.Symbol, pos.Side);
        BtcTicker.ActiveOverlay = new ActivePositionOverlay(
            pos.EntryPrice, signal?.StopLoss, signal?.TakeProfit, signal?.TakeProfit2, pos.Side);
    }

    /// <summary>Aktualisiert die Trade-Markers und Positions-Overlay auf dem Chart.</summary>
    private void UpdateChartOverlay()
    {
        // Wenn eine Position ausgewählt ist → deren Overlay anzeigen
        if (SelectedPosition != null)
        {
            var signal = FindPositionSignal(SelectedPosition.Symbol, SelectedPosition.Side);
            BtcTicker.ActiveOverlay = new ActivePositionOverlay(
                SelectedPosition.EntryPrice, signal?.StopLoss, signal?.TakeProfit, signal?.TakeProfit2, SelectedPosition.Side);
            return;
        }

        // Sonst: Aktive BTC-Position als Default-Overlay (alle Modi)
        var btcPos = OpenPositions.FirstOrDefault(p => p.Symbol == BtcTicker.SelectedSymbol);
        if (btcPos != null)
        {
            var signal = FindPositionSignal(btcPos.Symbol, btcPos.Side);
            BtcTicker.ActiveOverlay = new ActivePositionOverlay(
                btcPos.EntryPrice, signal?.StopLoss, signal?.TakeProfit, signal?.TakeProfit2, btcPos.Side);
        }
        else
        {
            BtcTicker.ActiveOverlay = null;
        }
    }

    /// Erstellt ein PositionDisplayItem mit CloseRequested-Verdrahtung und SL/TP aus dem Service.
    /// </summary>
    private PositionDisplayItem CreatePositionItem(Position p)
    {
        var item = new PositionDisplayItem
        {
            Symbol = p.Symbol,
            Side = p.Side,
            EntryPrice = p.EntryPrice,
            MarkPrice = p.MarkPrice,
            Quantity = p.Quantity,
            Pnl = p.UnrealizedPnl,
            Leverage = p.Leverage
        };

        // Close-Action verdrahten
        item.CloseRequested = (pos) => { RequestClosePosition(pos); return Task.CompletedTask; };

        // SL/TP + erweiterte Infos aus dem Signal laden
        var suppressSlTpEvents = true;

        var signal = FindPositionSignal(p.Symbol, p.Side);
        if (signal != null)
        {
            item.StopLoss = signal.StopLoss;
            item.TakeProfit = signal.TakeProfit;
        }

        // Multi-TF Standalone: Navigator-TF aus ExitState → Badge
        var navTf = IsPaperMode
            ? _paperService.GetExitStatesSnapshot().GetValueOrDefault($"{p.Symbol}_{p.Side}")?.NavigatorTimeframe
            : _liveManager.Service?.GetExitStatesSnapshot().GetValueOrDefault($"{p.Symbol}_{p.Side}")?.NavigatorTimeframe;
        item.TimeframeBadge = navTf switch
        {
            Core.Enums.TimeFrame.D1 => "1D",
            Core.Enums.TimeFrame.H4 => "4H",
            Core.Enums.TimeFrame.H1 => "1H",
            Core.Enums.TimeFrame.M5 => "5m",
            Core.Enums.TimeFrame.M15 => "15m",
            Core.Enums.TimeFrame.M30 => "30m",
            _ => ""
        };

        suppressSlTpEvents = false;

        // SL/TP-Änderungen an den richtigen Service zurückschreiben
        item.PropertyChanged += (_, e) =>
        {
            if (suppressSlTpEvents) return;
            if (e.PropertyName is not (nameof(PositionDisplayItem.StopLoss) or nameof(PositionDisplayItem.TakeProfit)))
                return;
            if (!OpenPositions.Contains(item)) return;

            var side = item.Side;
            if (IsPaperMode)
            {
                _paperService.UpdatePositionSignal(item.Symbol, side, item.StopLoss, item.TakeProfit);
            }
            else
            {
                _liveManager.Service?.UpdatePositionSignal(item.Symbol, side, item.StopLoss, item.TakeProfit);
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Trade",
                $"{item.Symbol}: SL={item.StopLoss?.ToString("F8") ?? "---"} / TP={item.TakeProfit?.ToString("F8") ?? "---"}", item.Symbol));
        };

        return item;
    }

    /// <summary>
    /// Aktualisiert Positionen direkt aus Position-Daten:
    /// - Bestehende Items: Nur volatile Werte updaten (MarkPrice, Pnl, Qty, Leverage)
    /// - Neue Positionen: CreatePositionItem nur für wirklich neue Positionen aufrufen
    /// - Geschlossene Positionen: entfernen
    /// Vermeidet Wegwerf-Objekte + Event-Handler-Leaks bei jedem 5s-Update.
    /// </summary>
    private void UpdatePositionsFromData(IReadOnlyList<Position> positions)
    {
        // Map der neuen Positionen nach Symbol_Side
        var posMap = new Dictionary<string, Position>();
        foreach (var p in positions)
            posMap[$"{p.Symbol}_{p.Side}"] = p;

        // Bestehende Items updaten oder entfernen
        for (int i = OpenPositions.Count - 1; i >= 0; i--)
        {
            var existing = OpenPositions[i];
            if (posMap.TryGetValue(existing.PositionKey, out var updated))
            {
                // Update: Nur volatile Werte aktualisieren (SL/TP + PropertyChanged-Handler bleiben erhalten)
                existing.MarkPrice = updated.MarkPrice;
                existing.Pnl = updated.UnrealizedPnl;
                existing.Quantity = updated.Quantity;
                existing.Leverage = updated.Leverage;

                // Haltezeit aus ExitState berechnen
                var entryTime = FindEntryTime(existing.Symbol, existing.Side);
                if (entryTime.HasValue)
                {
                    var hold = DateTime.UtcNow - entryTime.Value;
                    existing.HoldTimeText = hold.TotalHours >= 24
                        ? $"{hold.Days}d {hold.Hours}h"
                        : hold.TotalHours >= 1
                            ? $"{(int)hold.TotalHours}h {hold.Minutes}m"
                            : $"{hold.Minutes}m";
                }

                // Liquidationspreis berechnen (Isolated-Margin-Formel)
                if (updated.Leverage > 0 && updated.EntryPrice > 0)
                {
                    const decimal mmr = 0.004m; // BingX Maintenance Margin Rate
                    var liqDist = (1m - mmr) / updated.Leverage;
                    existing.LiquidationPrice = updated.Side == Side.Buy
                        ? updated.EntryPrice * (1m - liqDist)
                        : updated.EntryPrice * (1m + liqDist);
                }
                posMap.Remove(existing.PositionKey);
            }
            else
            {
                // Position geschlossen: entfernen
                OpenPositions.RemoveAt(i);
            }
        }

        // Nur für wirklich neue Positionen Items erstellen (mit Event-Handler + SL/TP)
        foreach (var p in posMap.Values)
            OpenPositions.Add(CreatePositionItem(p));
    }

    /// <summary>Aktualisiert HasOpenPositions + PositionsStatusText + Chart-Overlay (3x genutzt).</summary>
    private void UpdatePositionsStatus()
    {
        HasOpenPositions = OpenPositions.Count > 0;
        PositionsStatusText = HasOpenPositions
            ? $"{OpenPositions.Count} offene Position{(OpenPositions.Count > 1 ? "en" : "")}"
            : "Keine offenen Positionen";
    }
}
