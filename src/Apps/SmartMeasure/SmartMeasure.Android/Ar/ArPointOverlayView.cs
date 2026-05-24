using Android.Content;
using Android.Graphics;
using Android.Views;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Transparentes Overlay über der ARCore-Kameraansicht.
///
/// Features:
/// - Reticle (Crosshair) in Bildmitte mit HitQuality-Farbcodierung
/// - Tracking-State-Banner (Warnung bei Verlust)
/// - Live-Stats-Panel (Fläche, Länge, Höhenbereich, Session-Zeit)
/// - Distanz-Labels zwischen ALLEN aufeinanderfolgenden Punkten
/// - Höhen-Δ-Labels pro Punkt
/// - Nord-Pfeil (rotiert mit MagneticHeading)
/// - Maßstab-Balken (mit geschätzter 1m-Referenz bei aktuellem Reticle-Abstand)
/// - Auto-Close-Hint bei geschlossener Kontur
/// - Plane-Polygone halbtransparent
/// - Transient-Hints (nach Undo/Redo etc.)
/// </summary>
public sealed partial class ArPointOverlayView : View
{
    private readonly List<ArPoint> _points;
    private readonly List<ArContour> _contours;

    // Projizierte Screen-Positionen (vom GL-Thread aktualisiert)
    private List<(float screenX, float screenY, int pointIndex)> _projectedPoints = [];
    private List<(float screenX, float screenY, int contourIdx, int pointIdx)> _projectedContourPoints = [];
    private List<List<(float screenX, float screenY)>> _projectedPlanes = [];
    private ArOverlayState _state = new();
    private int _selectedIndex = -1;
    private bool _showPlanes = true;
    private bool _showStats = true;

    // Transient-Hint Timer
    private long _transientHintUntilMs;

    // Aktuelle Screen-Bounds des Readiness-Badges. Werden nach jedem Draw aktualisiert,
    // die Activity nutzt sie für Tap-Detection (Tap → Detail-Panel).
    // RectF.Empty wenn Badge nicht sichtbar (z.B. nicht trackend).
    public global::Android.Graphics.RectF ReadinessBadgeBounds { get; private set; }
        = new global::Android.Graphics.RectF();

    /// <summary>Screen-Bounds des Nordpfeil-Buttons (oben mittig). Plan Kap. 4.13: Tap
    /// öffnet einen Kompass-Kalibrierungs-Hint. Wird pro Frame in <see cref="DrawNorthArrow"/>
    /// aktualisiert. RectF leer wenn Nordpfeil nicht sichtbar.</summary>
    public global::Android.Graphics.RectF NorthArrowBounds { get; private set; }
        = new global::Android.Graphics.RectF();

    // Paints (alle gecacht)
    private readonly Paint _pointPaint;
    private readonly Paint _pointOutlinePaint;
    private readonly Paint _selectedPaint;
    private readonly Paint _contourLinePaint;
    private readonly Paint _contourPointPaint;
    private readonly Paint _activeContourPaint;
    private readonly Paint _activeContourFillPaint;
    private readonly Paint _labelPaint;
    private readonly Paint _hintPaint;
    private readonly Paint _distancePaint;
    private readonly Paint _planeFillPaint;
    private readonly Paint _planeEdgePaint;
    private readonly Paint _snapIndicatorPaint;

    // Reticle Paints
    private readonly Paint _reticleOuterPaint;
    private readonly Paint _reticleInnerPaint;
    private readonly Paint _reticleTextPaint;

    // Tracking-Banner
    private readonly Paint _bannerBgPaint;
    private readonly Paint _bannerTextPaint;

    // Stats-Panel
    private readonly Paint _statsBgPaint;
    private readonly Paint _statsTextPaint;
    private readonly Paint _statsLabelPaint;

    // Nord-Pfeil
    private readonly Paint _northArrowPaint;
    private readonly Paint _northTextPaint;
    private readonly Paint _compassAccRingPaint;
    private readonly global::Android.Graphics.Path _northArrowPath;

    // Auto-Close-Ring
    private readonly Paint _autoCloseRingPaint;

    // Maßstab
    private readonly Paint _scalePaint;
    private readonly Paint _scaleTextPaint;

    // Live-Footer (Umfang / Fläche / Punkte) direkt über der Toolbar.
    private readonly Paint _footerBgPaint;
    private readonly Paint _footerLabelPaint;
    private readonly Paint _footerValuePaint;

    // Transient-Hint
    private readonly Paint _transientHintBgPaint;
    private readonly Paint _transientHintTextPaint;

    private readonly float _density;

    public ArPointOverlayView(Context context, List<ArPoint> points, List<ArContour> contours)
        : base(context)
    {
        _points = points;
        _contours = contours;

        SetWillNotDraw(false);
        SetBackgroundColor(Color.Transparent);

        _density = context.Resources!.DisplayMetrics!.Density;

        _pointPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(255, 255, 107, 0) };
        _pointPaint.SetStyle(Paint.Style.Fill);

        _pointOutlinePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            StrokeWidth = 2f * _density,
        };
        _pointOutlinePaint.SetStyle(Paint.Style.Stroke);

        _selectedPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(200, 0, 188, 212) };
        _selectedPaint.SetStyle(Paint.Style.Fill);

        _contourLinePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 0, 188, 212),
            StrokeWidth = 3f * _density,
        };
        _contourLinePaint.SetStyle(Paint.Style.Stroke);

        _contourPointPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(200, 0, 188, 212) };
        _contourPointPaint.SetStyle(Paint.Style.Fill);

        _activeContourPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 255, 235, 59),
            StrokeWidth = 3f * _density,
        };
        _activeContourPaint.SetStyle(Paint.Style.Stroke);
        _activeContourPaint.SetPathEffect(new DashPathEffect([10f * _density, 5f * _density], 0));

        _activeContourFillPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(50, 255, 235, 59) };
        _activeContourFillPaint.SetStyle(Paint.Style.Fill);

        _labelPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 11f * _density,
        };
        _labelPaint.SetShadowLayer(4f, 0f, 0f, Color.Black);

        _hintPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(200, 255, 255, 255),
            TextSize = 14f * _density,
            TextAlign = Paint.Align.Center,
        };
        _hintPaint.SetShadowLayer(6f, 0f, 0f, Color.Black);

        _distancePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(255, 255, 235, 59),
            TextSize = 12f * _density,
            TextAlign = Paint.Align.Center,
        };
        _distancePaint.SetShadowLayer(4f, 0f, 0f, Color.Black);

        _planeFillPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(40, 76, 175, 80) };
        _planeFillPaint.SetStyle(Paint.Style.Fill);

        _planeEdgePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(120, 76, 175, 80),
            StrokeWidth = 1.5f * _density,
        };
        _planeEdgePaint.SetStyle(Paint.Style.Stroke);

        _snapIndicatorPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(200, 233, 30, 99),
            StrokeWidth = 2f * _density,
        };
        _snapIndicatorPaint.SetStyle(Paint.Style.Stroke);

        _reticleOuterPaint = new Paint(PaintFlags.AntiAlias)
        {
            StrokeWidth = 2.5f * _density,
            Color = Color.White,
        };
        _reticleOuterPaint.SetStyle(Paint.Style.Stroke);

        _reticleInnerPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.White };
        _reticleInnerPaint.SetStyle(Paint.Style.Fill);

        _reticleTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 11f * _density,
            TextAlign = Paint.Align.Center,
        };
        _reticleTextPaint.SetShadowLayer(4f, 0f, 0f, Color.Black);

        _bannerBgPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(220, 244, 67, 54) };
        _bannerBgPaint.SetStyle(Paint.Style.Fill);

        _bannerTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 14f * _density,
            TextAlign = Paint.Align.Center,
            FakeBoldText = true,
        };

        _statsBgPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(180, 0, 0, 0) };
        _statsBgPaint.SetStyle(Paint.Style.Fill);

        _statsTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 13f * _density,
            FakeBoldText = true,
        };

        _statsLabelPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(180, 255, 255, 255),
            TextSize = 10f * _density,
        };

        _northArrowPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(240, 239, 83, 80) };
        _northArrowPaint.SetStyle(Paint.Style.Fill);

        _northTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(255, 239, 83, 80),
            TextSize = 13f * _density,
            TextAlign = Paint.Align.Center,
            FakeBoldText = true,
        };
        _northTextPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

        _northArrowPath = new global::Android.Graphics.Path();
        BuildNorthArrowPath(_northArrowPath, 14f * _density);

        // Ring um die Kompass-Rosette wenn Mag-Accuracy schlecht ist (Color wird pro Draw gesetzt)
        _compassAccRingPaint = new Paint(PaintFlags.AntiAlias)
        {
            StrokeWidth = 2f * _density,
        };
        _compassAccRingPaint.SetStyle(Paint.Style.Stroke);

        _autoCloseRingPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 76, 175, 80),
            StrokeWidth = 3f * _density,
        };
        _autoCloseRingPaint.SetStyle(Paint.Style.Stroke);

        _scalePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            StrokeWidth = 2f * _density,
        };
        _scalePaint.SetStyle(Paint.Style.Stroke);

        _scaleTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 11f * _density,
            TextAlign = Paint.Align.Center,
        };
        _scaleTextPaint.SetShadowLayer(3f, 0f, 0f, Color.Black);

        // Live-Footer-Paints — größere Schrift als Stats-Panel weil das Footer-Info primär ist.
        _footerBgPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(220, 18, 18, 28) };
        _footerBgPaint.SetStyle(Paint.Style.Fill);

        _footerLabelPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(200, 255, 255, 255),
            TextSize = 10f * _density,
            TextAlign = Paint.Align.Center,
        };

        _footerValuePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 16f * _density,
            TextAlign = Paint.Align.Center,
            FakeBoldText = true,
        };
        _footerValuePaint.SetShadowLayer(2f, 0f, 0f, Color.Black);

        _transientHintBgPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(220, 76, 175, 80) };
        _transientHintBgPaint.SetStyle(Paint.Style.Fill);

        _transientHintTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 13f * _density,
            TextAlign = Paint.Align.Center,
            FakeBoldText = true,
        };
    }

    private static void BuildNorthArrowPath(global::Android.Graphics.Path path, float size)
    {
        path.Reset();
        path.MoveTo(0, -size);           // Spitze oben
        path.LineTo(-size * 0.6f, size * 0.5f);
        path.LineTo(0, size * 0.2f);      // Einbuchtung
        path.LineTo(size * 0.6f, size * 0.5f);
        path.Close();
    }

    public void UpdateProjectedPositions(
        List<(float screenX, float screenY, int pointIndex)> points,
        List<(float screenX, float screenY, int contourIdx, int pointIdx)> contourPoints)
    {
        _projectedPoints = points;
        _projectedContourPoints = contourPoints;
        Invalidate();
    }

    public void UpdateProjectedPlanes(List<List<(float screenX, float screenY)>> planes)
    {
        _projectedPlanes = planes;
        Invalidate();
    }

    public void UpdateState(ArOverlayState state)
    {
        _state = state;
        if (!string.IsNullOrEmpty(state.TransientHint))
            _transientHintUntilMs = Java.Lang.JavaSystem.CurrentTimeMillis() + 1500;
        Invalidate();
    }

    public void SetShowPlanes(bool show)
    {
        _showPlanes = show;
        Invalidate();
    }

    public void SetShowStats(bool show)
    {
        _showStats = show;
        Invalidate();
    }

    public void SetSelectedIndex(int index)
    {
        _selectedIndex = index;
        Invalidate();
    }

    protected override void OnDetachedFromWindow()
    {
        _pointPaint.Dispose();
        _pointOutlinePaint.Dispose();
        _selectedPaint.Dispose();
        _contourLinePaint.Dispose();
        _contourPointPaint.Dispose();
        _activeContourPaint.PathEffect?.Dispose();
        _activeContourPaint.Dispose();
        _activeContourFillPaint.Dispose();
        _labelPaint.Dispose();
        _hintPaint.Dispose();
        _distancePaint.Dispose();
        _planeFillPaint.Dispose();
        _planeEdgePaint.Dispose();
        _snapIndicatorPaint.Dispose();
        _reticleOuterPaint.Dispose();
        _reticleInnerPaint.Dispose();
        _reticleTextPaint.Dispose();
        _bannerBgPaint.Dispose();
        _bannerTextPaint.Dispose();
        _statsBgPaint.Dispose();
        _statsTextPaint.Dispose();
        _statsLabelPaint.Dispose();
        _northArrowPaint.Dispose();
        _northTextPaint.Dispose();
        _compassAccRingPaint.Dispose();
        _northArrowPath.Dispose();
        _autoCloseRingPaint.Dispose();
        _scalePaint.Dispose();
        _scaleTextPaint.Dispose();
        _footerBgPaint.Dispose();
        _footerLabelPaint.Dispose();
        _footerValuePaint.Dispose();
        _transientHintBgPaint.Dispose();
        _transientHintTextPaint.Dispose();
        base.OnDetachedFromWindow();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        var width = canvas.Width;
        var height = canvas.Height;
        var pointRadius = 8f * _density;
        var contourPointRadius = 5f * _density;

        // 1. Erkannte Planes
        if (_showPlanes)
            DrawDetectedPlanes(canvas);

        // 2. Aktive Kontur-Fläche (halbtransparent, wenn >= 3 Punkte)
        DrawActiveContourFill(canvas);

        // 3. Kontur-Linien + Auto-Close-Vorschau
        DrawContourLines(canvas, contourPointRadius);

        // 4. Einzelpunkte
        DrawPoints(canvas, pointRadius);

        // 5. Distanz-Labels zwischen ALLEN aufeinanderfolgenden Punkten
        DrawInterPointDistances(canvas);

        // 6. Reticle mit HitQuality-Färbung (nur bei Tracking)
        if (_state.IsTracking)
            DrawReticle(canvas, width, height);

        // 6b. Plan-Kap. 5.3: Tape-Measure-Polyline (Cyan, gestrichelt) + Segment-Distanzen
        if (_state.TapeMeasureScreenPoints != null && _state.TapeMeasureScreenPoints.Count > 0)
            DrawTapeMeasure(canvas);

        // 6c. Plan-Kap. 5.9: Stakeout-Pfeil im Overlay-Zentrum + Distanz + Target-Label
        if (_state.IsStakeoutMode)
            DrawStakeoutOverlay(canvas, width, height);

        // 7. Auto-Close-Ring am ersten Kontur-Punkt
        if (_state.AutoCloseTarget.HasValue)
            DrawAutoCloseRing(canvas, _state.AutoCloseTarget.Value);

        // 8. Tracking-Warning-Banner
        if (!_state.IsTracking)
            DrawTrackingBanner(canvas, width);

        // 8b. Persistente System-Banner (Thermal + Battery) — stapeln sich unter dem
        // Tracking-Banner. Anders als TransientHints bleiben sie sichtbar solange das
        // System-Event aktiv ist, sodass der User nicht den Grund für reduzierte Präzision
        // verpasst.
        DrawSystemWarningBanners(canvas, width);

        // 9. Stats-Panel (oben rechts)
        if (_showStats)
            DrawStatsPanel(canvas, width);

        // 10. Nord-Pfeil (oben rechts unter Stats oder in Ecke)
        DrawNorthArrow(canvas, width);

        // 11. Maßstab (unten links über Toolbar)
        DrawScaleBar(canvas, width, height);

        // 11b. Live-Footer mit primären Mess-Werten (Umfang/Fläche/Punkte) direkt über
        // der Toolbar — prominenter als das Stats-Panel oben rechts.
        DrawLiveFooter(canvas, width, height);

        // 12. Ready-Badge + Quality-Score (oben links)
        DrawReadinessBadge(canvas);

        // 13. Transient-Hint (falls aktiv)
        DrawTransientHint(canvas, width, height);

        // 13. Empty-State wenn keine Punkte/Konturen
        if (_projectedPoints.Count == 0 && _projectedContourPoints.Count == 0
            && _points.Count == 0 && _contours.Count == 0
            && _state.IsTracking)
        {
            var hint = _projectedPlanes.Count == 0
                ? "Bewege die Kamera langsam über den Boden..."
                : "Tippe auf eine Fläche um einen Punkt zu setzen";
            canvas.DrawText(hint, width / 2f, height / 2f + 60 * _density, _hintPaint);
        }
    }

    private void DrawDetectedPlanes(Canvas canvas)
    {
        foreach (var plane in _projectedPlanes)
        {
            if (plane.Count < 3) continue;
            using var path = new global::Android.Graphics.Path();
            path.MoveTo(plane[0].screenX, plane[0].screenY);
            for (var i = 1; i < plane.Count; i++)
                path.LineTo(plane[i].screenX, plane[i].screenY);
            path.Close();

            canvas.DrawPath(path, _planeFillPaint);
            canvas.DrawPath(path, _planeEdgePaint);
        }
    }

    private void DrawActiveContourFill(Canvas canvas)
    {
        var activePts = _projectedContourPoints
            .Where(p => p.contourIdx == -1)
            .OrderBy(p => p.pointIdx)
            .Select(p => (p.screenX, p.screenY))
            .ToList();

        if (activePts.Count < 3) return;

        using var path = new global::Android.Graphics.Path();
        path.MoveTo(activePts[0].screenX, activePts[0].screenY);
        for (var i = 1; i < activePts.Count; i++)
            path.LineTo(activePts[i].screenX, activePts[i].screenY);
        path.Close();
        canvas.DrawPath(path, _activeContourFillPaint);
    }

    /// <summary>Farbe pro ArContourType (für Multi-Kontur-Visualisierung in Gartenplanung).</summary>
    private static Color GetContourTypeColor(ArContourType type) => type switch
    {
        ArContourType.Weg        => Color.Argb(220, 144, 164, 174), // Blaugrau
        ArContourType.Beet       => Color.Argb(220, 109, 76, 65),   // Braun
        ArContourType.Mauer      => Color.Argb(220, 189, 189, 189), // Grau
        ArContourType.Zaun       => Color.Argb(220, 121, 85, 72),   // Holz-Braun
        ArContourType.Terrasse   => Color.Argb(220, 215, 204, 200), // Sandstein
        ArContourType.Gebaeude   => Color.Argb(220, 96, 125, 139),  // Blaugrau
        ArContourType.Wasser     => Color.Argb(220, 33, 150, 243),  // Blau
        ArContourType.Kante      => Color.Argb(220, 255, 235, 59),  // Gelb
        _                        => Color.Argb(220, 0, 188, 212),   // Cyan (Grenze default)
    };

    private void DrawContourLines(Canvas canvas, float pointRadius)
    {
        var contourGroups = _projectedContourPoints
            .GroupBy(p => p.contourIdx)
            .OrderBy(g => g.Key);

        foreach (var group in contourGroups)
        {
            var sorted = group.OrderBy(p => p.pointIdx).ToList();
            if (sorted.Count < 1) continue;

            var isActive = group.Key == -1;

            // Abgeschlossene Konturen: typ-spezifische Farbe statt einheitlich cyan
            Paint paint;
            if (isActive)
            {
                paint = _activeContourPaint;
            }
            else if (group.Key >= 0 && group.Key < _contours.Count)
            {
                _contourLinePaint.Color = GetContourTypeColor(_contours[group.Key].ContourType);
                paint = _contourLinePaint;
            }
            else
            {
                paint = _contourLinePaint;
            }

            for (var i = 1; i < sorted.Count; i++)
            {
                canvas.DrawLine(
                    sorted[i - 1].screenX, sorted[i - 1].screenY,
                    sorted[i].screenX, sorted[i].screenY,
                    paint);
            }

            if (!isActive && group.Key >= 0 && group.Key < _contours.Count && _contours[group.Key].IsClosed)
            {
                canvas.DrawLine(
                    sorted[^1].screenX, sorted[^1].screenY,
                    sorted[0].screenX, sorted[0].screenY,
                    paint);
            }

            foreach (var (sx, sy, _, _) in sorted)
                canvas.DrawCircle(sx, sy, pointRadius, _contourPointPaint);

            // Nummer am ersten Punkt für aktive Kontur
            if (isActive && sorted.Count > 0)
            {
                canvas.DrawText("1", sorted[0].screenX + pointRadius + 4,
                    sorted[0].screenY - 4, _labelPaint);
            }
        }
    }

    /// <summary>Dauer der Pop-In-Animation für neue Punkte in Millisekunden.</summary>
    private const int PointBornAnimationMs = 250;

    private void DrawPoints(Canvas canvas, float pointRadius)
    {
        var nowUtc = DateTime.UtcNow;
        var anyAnimating = false;

        foreach (var (sx, sy, idx) in _projectedPoints)
        {
            if (idx == _selectedIndex)
                canvas.DrawCircle(sx, sy, pointRadius * 2f, _selectedPaint);

            // Confidence-basierte Darstellung: niedrige Confidence → kleinerer/blassere Punkt
            var confidence = 1f;
            int hitQuality = 3;
            float stdDev = 0f;
            DateTime timestamp = nowUtc;
            if (idx < _points.Count)
            {
                confidence = _points[idx].Confidence;
                hitQuality = _points[idx].HitQuality;
                stdDev = _points[idx].PositionStdDev;
                timestamp = _points[idx].Timestamp;
            }

            // Plan Kap. 4.12: Punkt-Radius nicht mit Confidence schrumpfen lassen — bei
            // niedriger Confidence sonst kaum sichtbar (4.8 dp). Stattdessen Mindest-Radius
            // halten (6 dp via 0.75 * pointRadius bei pointRadius=8dp) und Confidence über
            // Outline-Stärke signalisieren (dünn = unsicher, dick = sicher).
            var effectiveR = MathF.Max(pointRadius * 0.75f, pointRadius * (0.85f + 0.15f * confidence));
            var alpha = (int)(255 * (0.5f + 0.5f * confidence));
            // Outline-Stroke skaliert mit Confidence: 1.0 dp bei conf=0, 3.0 dp bei conf=1.
            var outlineStroke = (1f + 2f * confidence) * _density;

            // Pop-In-Animation: junge Punkte starten 2.2× groß und schrumpfen auf normalgröße.
            // Ease-out-Quadratic. Dauer = PointBornAnimationMs.
            var ageMs = (nowUtc - timestamp).TotalMilliseconds;
            if (ageMs >= 0 && ageMs < PointBornAnimationMs)
            {
                var t = (float)(ageMs / PointBornAnimationMs);
                var ease = 1f - (1f - t) * (1f - t); // Ease-out-Quadratic
                var scale = 2.2f - 1.2f * ease;
                effectiveR *= scale;
                anyAnimating = true;
            }

            _pointPaint.Alpha = alpha;
            canvas.DrawCircle(sx, sy, effectiveR, _pointPaint);
            // Outline mit confidence-skaliertem Stroke — Plan Kap. 4.12.
            var originalStroke = _pointOutlinePaint.StrokeWidth;
            _pointOutlinePaint.StrokeWidth = outlineStroke;
            canvas.DrawCircle(sx, sy, effectiveR, _pointOutlinePaint);
            _pointOutlinePaint.StrokeWidth = originalStroke;
            _pointPaint.Alpha = 255;

            // Hit-Quality-Indikator: kleines Symbol oberhalb des Punktes
            if (hitQuality < 3)
            {
                var qualityText = hitQuality == 2 ? "~" : "?";
                canvas.DrawText(qualityText, sx - 3, sy - effectiveR - 4, _labelPaint);
            }

            canvas.DrawText($"{idx + 1}", sx + effectiveR + 4, sy - 4, _labelPaint);

            if (idx < _points.Count)
            {
                var p = _points[idx];

                // Label
                if (!string.IsNullOrEmpty(p.Label))
                    canvas.DrawText(p.Label!, sx + effectiveR + 4, sy + 14 * _density, _labelPaint);

                // Höhen-Delta-Label (relativ zum ersten Punkt)
                if (idx > 0 && _points.Count > 0)
                {
                    var dh = p.Y - _points[0].Y;
                    if (MathF.Abs(dh) >= 0.02f)
                    {
                        var text = dh > 0 ? $"▲ {dh:F2}m" : $"▼ {MathF.Abs(dh):F2}m";
                        canvas.DrawText(text, sx + effectiveR + 4, sy + 28 * _density, _labelPaint);
                    }
                }

                // StdDev-Label (Messgenauigkeit) — nur zeigen wenn nennenswert
                if (stdDev > 0.005f)
                {
                    var sigmaText = $"σ={stdDev * 100:F1}cm";
                    canvas.DrawText(sigmaText, sx + effectiveR + 4, sy + 42 * _density, _labelPaint);
                }
            }
        }

        // Solange Pop-In-Animation läuft, alle 16ms neu zeichnen (60fps).
        // Frame-Updates vom GL-Thread sind nicht garantiert wenn Tracking pausiert.
        if (anyAnimating)
            PostInvalidateDelayed(16);
    }

    /// <summary>Plan-Kap. 5.9: Stakeout-Overlay — grosser Richtungs-Pfeil (relativ zur
    /// Kamera-Richtung), Distanz-Label, Target-Label und Fortschritts-Anzeige
    /// (3/12 Targets). Pfeil-Farbe verschiebt sich von Rot (weit) ueber Gelb (nahe) bis
    /// Gruen (im Toleranz-Bereich), analog zum 2D-StakeoutRenderer.</summary>
    private void DrawStakeoutOverlay(Canvas canvas, int width, int height)
    {
        var label = _state.StakeoutTargetLabel;
        var distance = _state.StakeoutDistanceMeters;
        var relBearing = _state.StakeoutRelativeBearingDeg;

        // Wenn kein Target oder kein Position-Fix: dezenter Hint statt Pfeil-Panik.
        var cx = width / 2f;
        var cyArrow = height * 0.42f;

        if (label == null || !distance.HasValue)
        {
            // Hinweis-Box statt Pfeil
            using var paint = new Paint(PaintFlags.AntiAlias)
            {
                Color = Color.Argb(220, 30, 30, 30),
            };
            var rect = new RectF(cx - 180f * _density, cyArrow - 28f * _density,
                                 cx + 180f * _density, cyArrow + 28f * _density);
            canvas.DrawRoundRect(rect, 10f * _density, 10f * _density, paint);

            using var textPaint = new Paint(PaintFlags.AntiAlias)
            {
                Color = Color.White,
                TextSize = 16f * _density,
                TextAlign = Paint.Align.Center,
            };
            var text = label == null
                ? "🎯 Keine Stakeout-Ziele"
                : "🎯 Warte auf GPS / VPS …";
            canvas.DrawText(text, cx, cyArrow + 5f * _density, textPaint);
            return;
        }

        // Distanz-abhaengige Farbe (analog 2D-Stakeout: gruen <10cm, gelb <1m, orange <5m, rot >5m)
        var color = distance.Value switch
        {
            < 0.10 => Color.Argb(255, 76, 175, 80),
            < 1.0  => Color.Argb(255, 255, 235, 59),
            < 5.0  => Color.Argb(255, 255, 152, 0),
            _      => Color.Argb(255, 244, 67, 54),
        };

        // Pfeil-Rendering: relBearing in Grad relativ zur Kamera-Vorwaertsrichtung.
        // 0 = nach vorne (Top des Displays), 90 = rechts, 180 = unten, -90 = links.
        // Wenn null (kein Heading): Pfeil zeigt einfach nach oben.
        using var arrowPaint = new Paint(PaintFlags.AntiAlias) { Color = color };
        arrowPaint.SetStyle(Paint.Style.Fill);

        canvas.Save();
        canvas.Translate(cx, cyArrow);
        if (relBearing.HasValue)
            canvas.Rotate((float)relBearing.Value);

        // Dreieck als Pfeil (Spitze oben, ca. 80dp lang)
        var length = 80f * _density;
        var width2 = 24f * _density;
        var arrowPath = new global::Android.Graphics.Path();
        arrowPath.MoveTo(0, -length);
        arrowPath.LineTo(width2, length * 0.3f);
        arrowPath.LineTo(width2 * 0.4f, length * 0.3f);
        arrowPath.LineTo(width2 * 0.4f, length * 0.6f);
        arrowPath.LineTo(-width2 * 0.4f, length * 0.6f);
        arrowPath.LineTo(-width2 * 0.4f, length * 0.3f);
        arrowPath.LineTo(-width2, length * 0.3f);
        arrowPath.Close();
        canvas.DrawPath(arrowPath, arrowPaint);

        // Outline
        using var arrowOutline = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(180, 0, 0, 0),
            StrokeWidth = 2f * _density,
        };
        arrowOutline.SetStyle(Paint.Style.Stroke);
        canvas.DrawPath(arrowPath, arrowOutline);

        canvas.Restore();

        // Distanz-Label unter dem Pfeil
        using var distPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 32f * _density,
            TextAlign = Paint.Align.Center,
        };
        distPaint.SetShadowLayer(6f, 0f, 2f, Color.Black);
        var distText = distance.Value < 1.0
            ? $"{distance.Value * 100:F1} cm"
            : $"{distance.Value:F2} m";
        canvas.DrawText(distText, cx, cyArrow + 130f * _density, distPaint);

        // Target-Label + Fortschritt
        using var lblPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 255, 255, 255),
            TextSize = 18f * _density,
            TextAlign = Paint.Align.Center,
        };
        lblPaint.SetShadowLayer(4f, 0f, 1f, Color.Black);
        var progress = _state.StakeoutTotalCount > 0
            ? $"  ({_state.StakeoutReachedCount}/{_state.StakeoutTotalCount})"
            : "";
        canvas.DrawText($"{label}{progress}", cx, cyArrow + 170f * _density, lblPaint);
    }

    /// <summary>Plan-Kap. 5.3: Footer im Tape-Modus — Punkt-Anzahl + Strecken-Summe + Hint
    /// "Long-Press auf Mass = Reset".</summary>
    private void DrawTapeMeasureFooter(Canvas canvas, int width, int height)
    {
        var toolbarOffset = 80 * _density + _state.BottomInsetPixels;
        var footerH = 56 * _density;
        var footerMargin = 8 * _density;
        var footerTop = height - toolbarOffset - footerH - footerMargin;
        var sidePad = 16 * _density;
        var rect = new RectF(sidePad, footerTop, width - sidePad, footerTop + footerH);

        canvas.DrawRoundRect(rect, 10 * _density, 10 * _density, _footerBgPaint);

        // 2 Spalten: Punkte / Summe
        var colW = (rect.Width()) / 2f;
        var labelY = rect.Top + 16 * _density;
        var valueY = rect.Top + 38 * _density;

        var count = _state.TapeMeasureScreenPoints?.Count ?? 0;
        var cx1 = rect.Left + colW / 2f;
        canvas.DrawText(_state.Labels.Points, cx1, labelY, _footerLabelPaint);
        canvas.DrawText(count.ToString(), cx1, valueY, _footerValuePaint);

        var cx2 = rect.Left + colW * 1.5f;
        canvas.DrawText("Σ", cx2, labelY, _footerLabelPaint);
        canvas.DrawText($"{_state.TapeMeasureTotalMeters:F2} m", cx2, valueY, _footerValuePaint);
    }

    /// <summary>Plan-Kap. 5.3: Tape-Measure-Polyline + Segment-Distanzen + Endpunkt-Reticles.
    /// Ad-hoc-Messung, visuell deutlich abgesetzt von Konturen (Cyan, durchgehend statt
    /// typ-farbig). Punkte sind kleine Kreise an jedem Tap-Punkt, Segmente sind solide
    /// Linien mit Distanz-Labels in der Mitte. Gesamtsumme oben rechts neben dem Stats-Panel.</summary>
    private void DrawTapeMeasure(Canvas canvas)
    {
        var points = _state.TapeMeasureScreenPoints;
        if (points == null || points.Count == 0) return;

        using var linePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(240, 0, 188, 212),   // Cyan
            StrokeWidth = 4f * _density,
            StrokeCap = Paint.Cap.Round,
        };
        linePaint.SetStyle(Paint.Style.Stroke);

        using var endPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(240, 255, 235, 59), // Gelb fuer Endpunkt-Reticle
        };
        endPaint.SetStyle(Paint.Style.Fill);

        using var endOutline = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(255, 0, 0, 0),
            StrokeWidth = 2f * _density,
        };
        endOutline.SetStyle(Paint.Style.Stroke);

        // Segmente zeichnen + Distanz-Labels
        var segments = _state.TapeMeasureSegmentMeters;
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            canvas.DrawLine(a.screenX, a.screenY, b.screenX, b.screenY, linePaint);

            if (segments != null && i - 1 < segments.Count)
            {
                var mx = (a.screenX + b.screenX) * 0.5f;
                var my = (a.screenY + b.screenY) * 0.5f;
                canvas.DrawText($"{segments[i - 1]:F2} m", mx, my - 8f * _density, _distancePaint);
            }
        }

        // Endpunkt-Reticles
        var r = 6f * _density;
        foreach (var (sx, sy) in points)
        {
            canvas.DrawCircle(sx, sy, r, endPaint);
            canvas.DrawCircle(sx, sy, r, endOutline);
        }
    }

    /// <summary>Distanz-Labels zwischen ALLEN aufeinanderfolgenden Punkten — vorher nur zwischen letzten 2.</summary>
    private void DrawInterPointDistances(Canvas canvas)
    {
        // Einzelpunkte in Reihenfolge
        var sortedPts = _projectedPoints.OrderBy(p => p.pointIndex).ToList();
        for (var i = 1; i < sortedPts.Count; i++)
        {
            var curr = sortedPts[i];
            var prev = sortedPts[i - 1];
            if (curr.pointIndex >= _points.Count || prev.pointIndex >= _points.Count) continue;

            var dist = _points[curr.pointIndex].DistanceTo(_points[prev.pointIndex]);
            var midX = (curr.screenX + prev.screenX) / 2f;
            var midY = (curr.screenY + prev.screenY) / 2f;
            canvas.DrawText($"{dist:F2}m", midX, midY - 10 * _density, _distancePaint);
        }

        // Kontur-Distanz-Labels (aktive Kontur)
        var activePts = _projectedContourPoints
            .Where(p => p.contourIdx == -1)
            .OrderBy(p => p.pointIdx)
            .ToList();

        if (_contours.Count > 0 || activePts.Count > 0)
        {
            // Aktive Kontur hat eigenen Punkt-Pool — wir rekonstruieren die Distanzen aus der
            // Live-Liste der contour points. Punkte kommen aus _contours[-1] = _activeContour,
            // aber wir haben keinen direkten Zugriff — wir nutzen Screen-Distanzen als Approximation.
            // Besser wäre eine separate Liste der Welt-Distanzen vom GL-Thread, für jetzt:
            // Wir zeigen Label nur wenn wir zwei aufeinanderfolgende Welt-Punkte haben.
            // Fallback: wir skippen es hier, die Live-Length wird im Stats-Panel gezeigt.
        }
    }

    private void DrawReticle(Canvas canvas, int width, int height)
    {
        var cx = _state.ReticleX > 0 ? _state.ReticleX : width / 2f;
        var cy = _state.ReticleY > 0 ? _state.ReticleY : height / 2f;

        var color = _state.HitQuality switch
        {
            ArHitQuality.Plane => Color.Argb(240, 76, 175, 80),   // Grün
            ArHitQuality.Point => Color.Argb(240, 255, 107, 0),   // Orange
            ArHitQuality.InstantPlacement => Color.Argb(240, 255, 235, 59), // Gelb
            _ => Color.Argb(180, 255, 255, 255),                   // Weiß (kein Hit)
        };

        _reticleOuterPaint.Color = color;
        _reticleInnerPaint.Color = color;

        var outerR = 22f * _density;
        var innerR = 4f * _density;

        // Außen-Ring
        canvas.DrawCircle(cx, cy, outerR, _reticleOuterPaint);

        // Crosshair-Linien
        canvas.DrawLine(cx - outerR - 8 * _density, cy, cx - outerR + 4 * _density, cy, _reticleOuterPaint);
        canvas.DrawLine(cx + outerR - 4 * _density, cy, cx + outerR + 8 * _density, cy, _reticleOuterPaint);
        canvas.DrawLine(cx, cy - outerR - 8 * _density, cx, cy - outerR + 4 * _density, _reticleOuterPaint);
        canvas.DrawLine(cx, cy + outerR - 4 * _density, cx, cy + outerR + 8 * _density, _reticleOuterPaint);

        // Zentrum-Dot
        if (_state.HitQuality != ArHitQuality.None)
            canvas.DrawCircle(cx, cy, innerR, _reticleInnerPaint);

        // Distanz-Label
        if (_state.HitDistanceMeters.HasValue)
        {
            var label = $"{_state.HitDistanceMeters.Value:F2}m";
            if (_state.HitHeightDelta.HasValue && MathF.Abs(_state.HitHeightDelta.Value) > 0.05f)
            {
                var dh = _state.HitHeightDelta.Value;
                label += dh > 0 ? $"  ▲{dh:F2}m" : $"  ▼{MathF.Abs(dh):F2}m";
            }
            canvas.DrawText(label, cx, cy + outerR + 18 * _density, _reticleTextPaint);
        }

        // Multi-Frame-Sampling-Fortschritt: gelber Ring während Messung
        if (_state.IsSampling)
        {
            var progR = outerR + 14 * _density;
            var progress = Math.Clamp(_state.SamplingProgress, 0f, 1f);
            var sweepAngle = 360f * progress;

            using var progressPaint = new Paint(PaintFlags.AntiAlias)
            {
                Color = Color.Argb(240, 255, 235, 59),
                StrokeWidth = 4f * _density,
                StrokeCap = Paint.Cap.Round,
            };
            progressPaint.SetStyle(Paint.Style.Stroke);

            var rect = new RectF(cx - progR, cy - progR, cx + progR, cy + progR);
            canvas.DrawArc(rect, -90f, sweepAngle, false, progressPaint);

            canvas.DrawText(_state.Labels.HoldStill, cx, cy - progR - 14 * _density, _reticleTextPaint);
        }

        // Stabilitäts-Balken links vom Reticle (nur wenn nicht perfekt stabil)
        if (_state.StabilityScore < 0.95f)
            DrawStabilityBar(canvas, cx - outerR - 24 * _density, cy);
    }

    private void DrawStabilityBar(Canvas canvas, float x, float y)
    {
        var barH = 40 * _density;
        var barW = 6 * _density;
        var rect = new RectF(x - barW / 2, y - barH / 2, x + barW / 2, y + barH / 2);

        using var bgPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(120, 0, 0, 0) };
        bgPaint.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(rect, 3f, 3f, bgPaint);

        var filled = Math.Clamp(_state.StabilityScore, 0f, 1f);
        var fillTop = rect.Bottom - rect.Height() * filled;
        var fillRect = new RectF(rect.Left, fillTop, rect.Right, rect.Bottom);

        var fillColor = filled > 0.7f
            ? Color.Argb(220, 76, 175, 80)
            : filled > 0.4f
                ? Color.Argb(220, 255, 193, 7)
                : Color.Argb(220, 244, 67, 54);

        using var fillPaint = new Paint(PaintFlags.AntiAlias) { Color = fillColor };
        fillPaint.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(fillRect, 3f, 3f, fillPaint);
    }

    private void DrawAutoCloseRing(Canvas canvas, (float x, float y) target)
    {
        var pulseR = 20f * _density;
        canvas.DrawCircle(target.x, target.y, pulseR, _autoCloseRingPaint);
        canvas.DrawText("Schließen", target.x, target.y - pulseR - 8 * _density, _reticleTextPaint);
    }

    private void DrawTrackingBanner(Canvas canvas, int width)
    {
        // Banner unter Top-Inset + Nord-Pfeil positionieren
        var top = MathF.Max(_state.TopInsetPixels + 64 * _density, 80 * _density);
        var bannerHeight = 40 * _density;
        var rect = new RectF(20 * _density, top, width - 20 * _density, top + bannerHeight);

        canvas.DrawRoundRect(rect, 8 * _density, 8 * _density, _bannerBgPaint);

        var text = _state.TrackingFailureReason ?? "Tracking verloren";
        var textY = top + bannerHeight / 2f + _bannerTextPaint.TextSize / 3f;
        canvas.DrawText(text, width / 2f, textY, _bannerTextPaint);
    }

    /// <summary>
    /// Stapelt persistente System-Warnungen (Thermal-Throttling, niedriger Akku) unter dem
    /// Tracking-Banner. Bleiben sichtbar solange das System-Event andauert — keine Auto-Fade
    /// wie TransientHints. Orange für Thermal, Gelb für Battery (Severity-Hierarchie).
    /// </summary>
    private void DrawSystemWarningBanners(Canvas canvas, int width)
    {
        if (string.IsNullOrEmpty(_state.ThermalWarning) && string.IsNullOrEmpty(_state.BatteryWarning))
            return;

        // Vertikale Position: unter Tracking-Banner falls aktiv, sonst direkt unter Top-Inset.
        var top = MathF.Max(_state.TopInsetPixels + 64 * _density, 80 * _density);
        if (!_state.IsTracking)
            top += 48 * _density; // Höhe Tracking-Banner (40dp + 8dp Abstand)

        var bannerH = 36 * _density;
        var gap = 6 * _density;

        if (!string.IsNullOrEmpty(_state.ThermalWarning))
        {
            DrawWarningBanner(canvas, width, top, bannerH,
                _state.ThermalWarning!, Color.Argb(230, 230, 81, 0)); // Orange
            top += bannerH + gap;
        }

        if (!string.IsNullOrEmpty(_state.BatteryWarning))
        {
            DrawWarningBanner(canvas, width, top, bannerH,
                _state.BatteryWarning!, Color.Argb(230, 245, 124, 0)); // Amber
        }
    }

    private void DrawWarningBanner(Canvas canvas, int width, float top, float height,
        string text, Color bgColor)
    {
        var rect = new RectF(20 * _density, top, width - 20 * _density, top + height);
        using var bgPaint = new Paint(PaintFlags.AntiAlias) { Color = bgColor };
        bgPaint.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(rect, 6 * _density, 6 * _density, bgPaint);
        var textY = top + height / 2f + _bannerTextPaint.TextSize / 3f;
        canvas.DrawText(text, width / 2f, textY, _bannerTextPaint);
    }

    private void DrawStatsPanel(Canvas canvas, int width)
    {
        // Panel unterhalb von Top-Inset + Nord-Pfeil starten
        var panelTop = MathF.Max(_state.TopInsetPixels + 60 * _density, 60 * _density);
        // Session-Zeit formatieren
        var seconds = _state.SessionSeconds;
        var timeText = seconds >= 60
            ? $"{seconds / 60}:{seconds % 60:D2}"
            : $"{seconds}s";

        // Nur zeigen wenn es was zu zeigen gibt
        var pointCount = _points.Count + _contours.Sum(c => c.Points.Count);
        if (pointCount == 0 && _state.DetectedPlaneCount == 0) return;

        var panelW = 150 * _density;
        var lineHeight = 18 * _density;
        var padding = 10 * _density;
        var linesNeeded = 4; // Zeit, Punkte, Flächen, Länge
        if (_state.HeightRangeMeters > 0.05f) linesNeeded++;
        if (_state.AnchorCount > 0) linesNeeded++;

        var panelH = padding * 2 + linesNeeded * lineHeight;
        var right = width - 16 * _density;
        var top = panelTop;

        var rect = new RectF(right - panelW, top, right, top + panelH);
        canvas.DrawRoundRect(rect, 8 * _density, 8 * _density, _statsBgPaint);

        var x = rect.Left + padding;
        var y = rect.Top + padding + 12 * _density;

        // Plan-Kap. 4.11: Labels aus dem lokalisierten Snapshot.
        var labels = _state.Labels;

        canvas.DrawText(labels.Time, x, y, _statsLabelPaint);
        canvas.DrawText(timeText, x + 50 * _density, y, _statsTextPaint);
        y += lineHeight;

        canvas.DrawText(labels.Points, x, y, _statsLabelPaint);
        canvas.DrawText(pointCount.ToString(), x + 50 * _density, y, _statsTextPaint);
        y += lineHeight;

        if (_state.LiveAreaSquareMeters > 0.01f)
        {
            canvas.DrawText(labels.Area, x, y, _statsLabelPaint);
            canvas.DrawText($"{_state.LiveAreaSquareMeters:F1} m²",
                x + 50 * _density, y, _statsTextPaint);
        }
        else
        {
            // "PLANES" ist ein technischer Begriff (AR-Tracking), bleibt englisch — kein RESX-Key.
            canvas.DrawText("PLANES", x, y, _statsLabelPaint);
            canvas.DrawText(_state.DetectedPlaneCount.ToString(),
                x + 50 * _density, y, _statsTextPaint);
        }
        y += lineHeight;

        canvas.DrawText(labels.Length, x, y, _statsLabelPaint);
        canvas.DrawText($"{_state.LiveLengthMeters:F2} m", x + 50 * _density, y, _statsTextPaint);

        if (_state.HeightRangeMeters > 0.05f)
        {
            y += lineHeight;
            canvas.DrawText(labels.HeightDelta, x, y, _statsLabelPaint);
            canvas.DrawText($"{_state.HeightRangeMeters:F2} m", x + 50 * _density, y, _statsTextPaint);
        }

        if (_state.AnchorCount > 0)
        {
            y += lineHeight;
            canvas.DrawText(labels.Anchors, x, y, _statsLabelPaint);
            // Farbe nach Qualität: Grün ok, Gelb nah Limit
            _statsTextPaint.Color = _state.AnchorCount >= 100
                ? Color.Argb(255, 255, 193, 7)
                : Color.White;
            canvas.DrawText(_state.AnchorCount.ToString(), x + 50 * _density, y, _statsTextPaint);
            _statsTextPaint.Color = Color.White;
        }
    }

    private void DrawNorthArrow(Canvas canvas, int width)
    {
        // Oben mittig — aber UNTER dem Punch-Hole auf Samsung S25 Ultra!
        // TopInsetPixels enthält Status-Bar + Cutout-Höhe.
        // Fallback auf 40dp wenn keine Insets verfügbar.
        var cx = width / 2f;
        var topOffset = MathF.Max(_state.TopInsetPixels, 40f * _density);
        var cy = topOffset + 22f * _density;
        var radius = 18f * _density;

        // Plan Kap. 4.13: Tap-Bounds vergroessern (Finger-Treffer-Flaeche). Reine
        // Visualisierungs-Radius bleibt 18dp, Tap-Hitbox 32dp.
        var tapPadding = 14f * _density;
        NorthArrowBounds.Set(
            cx - radius - tapPadding,
            cy - radius - tapPadding,
            cx + radius + tapPadding,
            cy + radius + tapPadding);

        // Kreis-BG. Bei niedriger Mag-Accuracy zusätzlich farbiger Ring um den Kreis —
        // visueller Hinweis dass die Nord-Richtung unzuverlässig ist (typisch in
        // Metallumgebung oder bei nicht-kalibriertem Kompass). Plan Kap. 4.10.
        canvas.DrawCircle(cx, cy, radius, _statsBgPaint);

        var (ringColor, arrowColor) = GetCompassAccuracyColors(_state.MagneticAccuracy);
        if (ringColor.HasValue)
        {
            // Gecachtes Ring-Paint, nur Farbe wechseln — keine Per-Frame-Allocation.
            _compassAccRingPaint.Color = ringColor.Value;
            canvas.DrawCircle(cx, cy, radius + 1f * _density, _compassAccRingPaint);
        }

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.Rotate(-_state.CompassHeading);

        var originalArrowColor = _northArrowPaint.Color;
        if (arrowColor.HasValue)
            _northArrowPaint.Color = arrowColor.Value;
        canvas.DrawPath(_northArrowPath, _northArrowPaint);
        _northArrowPaint.Color = originalArrowColor;
        canvas.Restore();

        canvas.DrawText("N", cx, cy + radius + 14 * _density, _northTextPaint);
    }

    /// <summary>
    /// Liefert Ring- und Pfeil-Farbe für die Kompass-Genauigkeit.
    /// Android-Magnetometer-Accuracy: 0=unreliable, 1=low, 2=medium, 3=high.
    /// Bei "high" geben wir null zurück — die Default-Paint-Farben bleiben aktiv.
    /// </summary>
    private static (Color? ring, Color? arrow) GetCompassAccuracyColors(int magAccuracy) => magAccuracy switch
    {
        0 => (Color.Argb(255, 244, 67, 54), Color.Argb(255, 244, 67, 54)),    // Rot - unkalibriert
        1 => (Color.Argb(255, 255, 152, 0), Color.Argb(255, 255, 152, 0)),    // Orange - niedrig
        2 => (Color.Argb(255, 255, 235, 59), null),                            // Gelb-Ring, Pfeil bleibt
        _ => (null, null),                                                      // High = normal
    };

    private void DrawScaleBar(Canvas canvas, int width, int height)
    {
        // Maßstab nur zeigen wenn wir eine Distanz-Referenz haben
        if (!_state.HitDistanceMeters.HasValue) return;

        // Pixel-pro-Meter schätzen: 1m nimmt in einer Standard-Kamera bei 1m Abstand
        // ~0.6 * viewport width an. Vereinfacht: scaleFactor = 400f / distance * density.
        // Das ist eine grobe Annäherung — exakt wäre ProjectionMatrix-Analyse.
        var dist = _state.HitDistanceMeters.Value;
        if (dist < 0.1f || dist > 20f) return;

        // Annahme: bei 1m Distance = 600dp für 1m Real-Width (grober Richtwert)
        var pixelsPerMeter = 600f * _density / dist;

        // Auf schöne Schrittweite runden (0.1, 0.25, 0.5, 1.0, 2.0, 5.0)
        double[] candidates = [5.0, 2.0, 1.0, 0.5, 0.25, 0.1];
        var refMeters = 1.0;
        foreach (var c in candidates)
        {
            if (c * pixelsPerMeter <= 120 * _density && c * pixelsPerMeter >= 40 * _density)
            {
                refMeters = c;
                break;
            }
        }

        var refPixels = (float)(refMeters * pixelsPerMeter);
        var baseX = 24 * _density;
        var baseY = height - 80 * _density;

        canvas.DrawLine(baseX, baseY, baseX + refPixels, baseY, _scalePaint);
        canvas.DrawLine(baseX, baseY - 5 * _density, baseX, baseY + 5 * _density, _scalePaint);
        canvas.DrawLine(baseX + refPixels, baseY - 5 * _density, baseX + refPixels, baseY + 5 * _density, _scalePaint);

        canvas.DrawText($"{refMeters:0.##} m", baseX + refPixels / 2f, baseY - 8 * _density, _scaleTextPaint);
    }

    /// <summary>
    /// Live-Footer-Bar direkt über der Toolbar — zeigt die primären Mess-Werte (Punkte,
    /// Länge, Fläche) in großer Schrift. Stats-Panel bleibt zusätzlich für Detail-Infos.
    /// Bottom-Position richtet sich nach Toolbar-Höhe (80dp) + BottomInset.
    /// </summary>
    private void DrawLiveFooter(Canvas canvas, int width, int height)
    {
        // Plan-Kap. 5.3: Im Tape-Modus eigener Footer mit Punktanzahl + Strecken-Summe.
        if (_state.IsTapeMeasureMode)
        {
            DrawTapeMeasureFooter(canvas, width, height);
            return;
        }

        // Nur zeigen wenn überhaupt etwas zu zeigen ist
        var pointCount = _points.Count + _contours.Sum(c => c.Points.Count);
        if (_state.IsTracking && pointCount == 0) return;

        var toolbarOffset = 80 * _density + _state.BottomInsetPixels;
        var footerH = 56 * _density;
        var footerMargin = 8 * _density;
        var footerTop = height - toolbarOffset - footerH - footerMargin;
        var sidePad = 16 * _density;
        var rect = new RectF(sidePad, footerTop, width - sidePad, footerTop + footerH);

        canvas.DrawRoundRect(rect, 10 * _density, 10 * _density, _footerBgPaint);

        // 3 Spalten: Punkte / Länge / Fläche. Plan-Kap. 4.11: Lokalisierte Labels.
        var fLabels = _state.Labels;
        var colW = (rect.Width()) / 3f;
        var labelY = rect.Top + 16 * _density;
        var valueY = rect.Top + 38 * _density;

        // Punkte
        var cx1 = rect.Left + colW / 2f;
        canvas.DrawText(fLabels.Points, cx1, labelY, _footerLabelPaint);
        canvas.DrawText(pointCount.ToString(), cx1, valueY, _footerValuePaint);

        // Länge
        var cx2 = rect.Left + colW * 1.5f;
        canvas.DrawText(fLabels.Length, cx2, labelY, _footerLabelPaint);
        canvas.DrawText($"{_state.LiveLengthMeters:F2} m", cx2, valueY, _footerValuePaint);

        // Fläche
        var cx3 = rect.Left + colW * 2.5f;
        canvas.DrawText(fLabels.Area, cx3, labelY, _footerLabelPaint);
        canvas.DrawText(
            _state.LiveAreaSquareMeters >= 0.01f
                ? $"{_state.LiveAreaSquareMeters:F1} m²"
                : "—",
            cx3, valueY, _footerValuePaint);
    }

    /// <summary>
    /// Zeichnet oben links ein Ready-Badge mit Quality-Score.
    /// Farbe: Grün (ready), Gelb (teilweise), Rot (nicht bereit).
    /// Zeigt zusätzlich den Tracking-Quality-Score in %.
    /// </summary>
    private void DrawReadinessBadge(Canvas canvas)
    {
        if (!_state.IsTracking)
        {
            ReadinessBadgeBounds.SetEmpty();
            return;
        }

        var padding = 8f * _density;
        var badgeX = 20 * _density;
        // Position unter Top-Inset (S25 Ultra: Status-Bar + Punch-Hole) + Platz für Nord-Pfeil
        var badgeY = MathF.Max(_state.TopInsetPixels + 110 * _density, 100 * _density);
        var badgeH = 32f * _density;

        // Text: "BEREIT" (lokalisiert) oder Checkliste der fehlenden Bedingungen.
        // ReadinessIssues kommt aus ValidatePreMeasureConditions in ArCaptureActivity —
        // dort wird die Liste bereits aus AppStrings zusammengebaut, also auch lokalisiert.
        var text = _state.IsReadyToMeasure
            ? $"✓ {_state.Labels.Ready}  {_state.TrackingQualityScore}%"
            : $"⏳ {_state.ReadinessIssues}";

        var textWidth = _bannerTextPaint.MeasureText(text);
        var badgeW = textWidth + 2 * padding;

        // Farbe nach Quality-Score
        var bgColor = _state.IsReadyToMeasure
            ? _state.TrackingQualityScore >= 80
                ? Color.Argb(220, 76, 175, 80)      // Grün
                : Color.Argb(220, 139, 195, 74)     // Hellgrün
            : _state.TrackingQualityScore >= 50
                ? Color.Argb(220, 255, 193, 7)      // Gelb
                : Color.Argb(220, 244, 67, 54);     // Rot

        using var bgPaint = new Paint(PaintFlags.AntiAlias) { Color = bgColor };
        bgPaint.SetStyle(Paint.Style.Fill);

        var rect = new RectF(badgeX, badgeY, badgeX + badgeW, badgeY + badgeH);
        canvas.DrawRoundRect(rect, 8f * _density, 8f * _density, bgPaint);
        canvas.DrawText(text, badgeX + badgeW / 2f, badgeY + badgeH / 2f + 5 * _density, _bannerTextPaint);

        // Bounds publizieren für Tap-Detection in der Activity. Etwas vergrößert (8dp) damit
        // dünne Finger leichter treffen.
        var tapPad = 8 * _density;
        ReadinessBadgeBounds.Set(
            rect.Left - tapPad, rect.Top - tapPad,
            rect.Right + tapPad, rect.Bottom + tapPad);
    }

    private void DrawTransientHint(Canvas canvas, int width, int height)
    {
        if (string.IsNullOrEmpty(_state.TransientHint)) return;
        if (Java.Lang.JavaSystem.CurrentTimeMillis() > _transientHintUntilMs) return;

        var textWidth = _transientHintTextPaint.MeasureText(_state.TransientHint);
        var bannerW = textWidth + 32 * _density;
        var bannerH = 32 * _density;
        var cx = width / 2f;
        var top = height - 140 * _density;

        var rect = new RectF(cx - bannerW / 2f, top, cx + bannerW / 2f, top + bannerH);
        canvas.DrawRoundRect(rect, 16 * _density, 16 * _density, _transientHintBgPaint);
        canvas.DrawText(_state.TransientHint, cx, top + bannerH / 2f + 5 * _density, _transientHintTextPaint);

        // Nächster Redraw für Ablauf
        PostInvalidateDelayed(200);
    }
}
