namespace BingXBot.Graphics;

/// <summary>
/// Zustand des interaktiven Charts: Viewport, Crosshair, Drag-State.
/// Wird vom InteractiveChartRenderer gehalten und von Mouse-Events aktualisiert.
/// </summary>
public class ChartState
{
    // Viewport: Welcher Candle-Bereich sichtbar ist
    public int ViewStart { get; set; }
    public int ViewEnd { get; set; }

    // Zoom-Level: Wie viele Candles maximal sichtbar (Default: alle)
    public int MaxVisibleCandles { get; set; } = 100;
    public int MinVisibleCandles { get; set; } = 20;

    // Crosshair
    public bool ShowCrosshair { get; set; }
    public float CrosshairX { get; set; }
    public float CrosshairY { get; set; }

    // Drag-State (Pan)
    public bool IsDragging { get; set; }
    public float DragStartX { get; set; }
    public int DragStartViewStart { get; set; }

    // SL/TP Drag
    public bool IsDraggingSl { get; set; }
    public bool IsDraggingTp { get; set; }
    public bool IsDraggingTp2 { get; set; }
    public decimal DragPriceValue { get; set; }

    // Indikator-Toggles
    public bool ShowEma50 { get; set; } = true;
    public bool ShowEma200 { get; set; } = true;
    public bool ShowBollingerBands { get; set; }
    public bool ShowSupertrend { get; set; } = true;
    public bool ShowRegimeBackground { get; set; } = true;

    /// <summary>Setzt den Viewport auf den gesamten Candle-Bereich.</summary>
    public void ResetViewport(int candleCount)
    {
        ViewStart = Math.Max(0, candleCount - MaxVisibleCandles);
        ViewEnd = candleCount;
    }

    /// <summary>Zoom: Ändert die Anzahl sichtbarer Candles um delta.</summary>
    public void Zoom(int delta, int totalCandles)
    {
        var visible = ViewEnd - ViewStart;
        var newVisible = Math.Clamp(visible + delta, MinVisibleCandles, Math.Min(totalCandles, 500));
        var center = (ViewStart + ViewEnd) / 2;
        ViewStart = Math.Max(0, center - newVisible / 2);
        ViewEnd = Math.Min(totalCandles, ViewStart + newVisible);
        if (ViewStart == 0) ViewEnd = Math.Min(totalCandles, newVisible);
    }

    /// <summary>Pan: Verschiebt den Viewport um delta Candles.</summary>
    public void Pan(int delta, int totalCandles)
    {
        var visible = ViewEnd - ViewStart;
        ViewStart = Math.Clamp(ViewStart + delta, 0, Math.Max(0, totalCandles - visible));
        ViewEnd = Math.Min(totalCandles, ViewStart + visible);
    }
}
