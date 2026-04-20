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
public sealed class ArPointOverlayView : View
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
    private readonly global::Android.Graphics.Path _northArrowPath;

    // Auto-Close-Ring
    private readonly Paint _autoCloseRingPaint;

    // Maßstab
    private readonly Paint _scalePaint;
    private readonly Paint _scaleTextPaint;

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
        _northArrowPath.Dispose();
        _autoCloseRingPaint.Dispose();
        _scalePaint.Dispose();
        _scaleTextPaint.Dispose();
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

        // 7. Auto-Close-Ring am ersten Kontur-Punkt
        if (_state.AutoCloseTarget.HasValue)
            DrawAutoCloseRing(canvas, _state.AutoCloseTarget.Value);

        // 8. Tracking-Warning-Banner
        if (!_state.IsTracking)
            DrawTrackingBanner(canvas, width);

        // 9. Stats-Panel (oben rechts)
        if (_showStats)
            DrawStatsPanel(canvas, width);

        // 10. Nord-Pfeil (oben rechts unter Stats oder in Ecke)
        DrawNorthArrow(canvas, width);

        // 11. Maßstab (unten links über Toolbar)
        DrawScaleBar(canvas, width, height);

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

    private void DrawPoints(Canvas canvas, float pointRadius)
    {
        foreach (var (sx, sy, idx) in _projectedPoints)
        {
            if (idx == _selectedIndex)
                canvas.DrawCircle(sx, sy, pointRadius * 2f, _selectedPaint);

            // Confidence-basierte Darstellung: niedrige Confidence → kleinerer/blassere Punkt
            var confidence = 1f;
            int hitQuality = 3;
            float stdDev = 0f;
            if (idx < _points.Count)
            {
                confidence = _points[idx].Confidence;
                hitQuality = _points[idx].HitQuality;
                stdDev = _points[idx].PositionStdDev;
            }

            var effectiveR = pointRadius * (0.6f + 0.4f * confidence);
            var alpha = (int)(255 * (0.5f + 0.5f * confidence));

            _pointPaint.Alpha = alpha;
            canvas.DrawCircle(sx, sy, effectiveR, _pointPaint);
            canvas.DrawCircle(sx, sy, effectiveR, _pointOutlinePaint);
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

            canvas.DrawText("STILL HALTEN", cx, cy - progR - 14 * _density, _reticleTextPaint);
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

        canvas.DrawText("ZEIT", x, y, _statsLabelPaint);
        canvas.DrawText(timeText, x + 50 * _density, y, _statsTextPaint);
        y += lineHeight;

        canvas.DrawText("PUNKTE", x, y, _statsLabelPaint);
        canvas.DrawText(pointCount.ToString(), x + 50 * _density, y, _statsTextPaint);
        y += lineHeight;

        if (_state.LiveAreaSquareMeters > 0.01f)
        {
            canvas.DrawText("FLÄCHE", x, y, _statsLabelPaint);
            canvas.DrawText($"{_state.LiveAreaSquareMeters:F1} m²",
                x + 50 * _density, y, _statsTextPaint);
        }
        else
        {
            canvas.DrawText("PLANES", x, y, _statsLabelPaint);
            canvas.DrawText(_state.DetectedPlaneCount.ToString(),
                x + 50 * _density, y, _statsTextPaint);
        }
        y += lineHeight;

        canvas.DrawText("LÄNGE", x, y, _statsLabelPaint);
        canvas.DrawText($"{_state.LiveLengthMeters:F2} m", x + 50 * _density, y, _statsTextPaint);

        if (_state.HeightRangeMeters > 0.05f)
        {
            y += lineHeight;
            canvas.DrawText("ΔH", x, y, _statsLabelPaint);
            canvas.DrawText($"{_state.HeightRangeMeters:F2} m", x + 50 * _density, y, _statsTextPaint);
        }

        if (_state.AnchorCount > 0)
        {
            y += lineHeight;
            canvas.DrawText("ANCHORS", x, y, _statsLabelPaint);
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

        // Kreis-BG
        canvas.DrawCircle(cx, cy, radius, _statsBgPaint);

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.Rotate(-_state.CompassHeading);
        canvas.DrawPath(_northArrowPath, _northArrowPaint);
        canvas.Restore();

        canvas.DrawText("N", cx, cy + radius + 14 * _density, _northTextPaint);
    }

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
    /// Zeichnet oben links ein Ready-Badge mit Quality-Score.
    /// Farbe: Grün (ready), Gelb (teilweise), Rot (nicht bereit).
    /// Zeigt zusätzlich den Tracking-Quality-Score in %.
    /// </summary>
    private void DrawReadinessBadge(Canvas canvas)
    {
        if (!_state.IsTracking) return;

        var padding = 8f * _density;
        var badgeX = 20 * _density;
        // Position unter Top-Inset (S25 Ultra: Status-Bar + Punch-Hole) + Platz für Nord-Pfeil
        var badgeY = MathF.Max(_state.TopInsetPixels + 110 * _density, 100 * _density);
        var badgeH = 32f * _density;

        // Text: "BEREIT" oder Checkliste der fehlenden Bedingungen
        var text = _state.IsReadyToMeasure
            ? $"✓ BEREIT  {_state.TrackingQualityScore}%"
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
