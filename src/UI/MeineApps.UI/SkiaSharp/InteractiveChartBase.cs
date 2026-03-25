using Avalonia;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Basis-Klasse für interaktive SkiaSharp-Charts mit Touch-Tooltip-Unterstützung.
/// Verarbeitet Touch/Maus-Events und verwaltet den aktiven Datenpunkt.
/// Abgeleitete Klassen implementieren OnDrawChart() und OnDrawTooltip().
/// </summary>
public abstract class InteractiveChartBase : SKCanvasView
{
    // Touch-State
    private int _activeDataPointIndex = -1;
    private float _touchX;
    private float _touchY;
    private bool _isTouching;

    // Timer für verzögertes Ausblenden
    private DispatcherTimer? _hideTimer;
    private const int HideDelayMs = 2000;

    /// <summary>
    /// Aktuell hervorgehobener Datenpunkt-Index (-1 = keiner).
    /// </summary>
    protected int ActiveDataPointIndex => _activeDataPointIndex;

    /// <summary>
    /// Touch-Position in Canvas-Koordinaten.
    /// </summary>
    protected SKPoint TouchPosition => new(_touchX, _touchY);

    /// <summary>
    /// True wenn gerade ein Touch/Maus-Event aktiv ist.
    /// </summary>
    protected bool IsTouching => _isTouching;

    protected InteractiveChartBase()
    {
        // Touch-Events aktivieren
        IsHitTestVisible = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        HandleTouch((float)pos.X, (float)pos.Y);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isTouching) return;
        var pos = e.GetPosition(this);
        HandleTouch((float)pos.X, (float)pos.Y);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isTouching = false;

        // Tooltip nach Verzögerung ausblenden
        StartHideTimer();
    }

    private void HandleTouch(float x, float y)
    {
        _isTouching = true;
        _hideTimer?.Stop();

        // Skalierung: Avalonia-Koordinaten → SkiaSharp-Koordinaten
        float scaleX = 1f;
        float scaleY = 1f;
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            // Scale wird im OnPaintSurface berücksichtigt
            scaleX = 1f;
            scaleY = 1f;
        }

        _touchX = x * scaleX;
        _touchY = y * scaleY;

        // Abgeleitete Klasse bestimmt den nächsten Datenpunkt
        int newIndex = FindNearestPoint(_touchX, _touchY);

        if (newIndex != _activeDataPointIndex)
        {
            _activeDataPointIndex = newIndex;
            InvalidateSurface();
        }
    }

    private void StartHideTimer()
    {
        // Timer einmalig erstellen und wiederverwenden (Stop/Start statt jedes Mal New)
        if (_hideTimer == null)
        {
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HideDelayMs) };
            _hideTimer.Tick += OnHideTimerTick;
        }
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        _hideTimer?.Stop();
        _activeDataPointIndex = -1;
        InvalidateSurface();
    }

    /// <summary>
    /// Blendet das Tooltip sofort aus.
    /// </summary>
    protected void DismissTooltip()
    {
        _hideTimer?.Stop();
        _activeDataPointIndex = -1;
        _isTouching = false;
        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        // Chart zeichnen (abgeleitete Klasse)
        OnDrawChart(canvas, bounds);

        // Tooltip zeichnen wenn aktiv
        if (_activeDataPointIndex >= 0)
        {
            OnDrawTooltip(canvas, bounds, _activeDataPointIndex);
        }
    }

    /// <summary>
    /// Zeichnet den Chart-Inhalt. Muss von abgeleiteten Klassen implementiert werden.
    /// </summary>
    protected abstract void OnDrawChart(SKCanvas canvas, SKRect bounds);

    /// <summary>
    /// Zeichnet das Tooltip für den aktiven Datenpunkt.
    /// Standard-Implementierung nutzt SkiaChartTooltip.
    /// Kann überschrieben werden für Custom-Tooltips.
    /// </summary>
    protected virtual void OnDrawTooltip(SKCanvas canvas, SKRect bounds, int dataPointIndex)
    {
        // Abgeleitete Klassen überschreiben diese Methode
    }

    /// <summary>
    /// Findet den nächsten Datenpunkt zur Touch-Position.
    /// Muss von abgeleiteten Klassen implementiert werden.
    /// Gibt den Datenpunkt-Index oder -1 zurück.
    /// </summary>
    protected abstract int FindNearestPoint(float touchX, float touchY);
}
