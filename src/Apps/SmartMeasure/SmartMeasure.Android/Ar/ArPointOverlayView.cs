using Android.Content;
using Android.Graphics;
using Android.Views;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Transparentes Overlay ueber der ARCore-Kameraansicht.
/// Zeichnet Punkte an projizierten Screen-Positionen, Kontur-Linien, Auswahl-Highlight.
/// </summary>
public class ArPointOverlayView : View
{
    private readonly List<ArPoint> _points;
    private readonly List<ArContour> _contours;

    // Projizierte Screen-Positionen (vom GL-Thread aktualisiert)
    private List<(float screenX, float screenY, int pointIndex)> _projectedPoints = [];
    private List<(float screenX, float screenY, int contourIdx, int pointIdx)> _projectedContourPoints = [];
    private List<List<(float screenX, float screenY)>> _projectedPlanes = [];
    private int _selectedIndex = -1;
    private bool _showPlanes = true;

    // Gecachte Paint-Objekte
    private readonly Paint _pointPaint;
    private readonly Paint _pointOutlinePaint;
    private readonly Paint _selectedPaint;
    private readonly Paint _contourLinePaint;
    private readonly Paint _contourPointPaint;
    private readonly Paint _activeContourPaint;
    private readonly Paint _labelPaint;
    private readonly Paint _hintPaint;
    private readonly Paint _distancePaint;
    private readonly Paint _planeFillPaint;
    private readonly Paint _planeEdgePaint;
    private readonly Paint _snapIndicatorPaint;

    private readonly float _density;

    public ArPointOverlayView(Context context, List<ArPoint> points, List<ArContour> contours)
        : base(context)
    {
        _points = points;
        _contours = contours;

        SetWillNotDraw(false);
        SetBackgroundColor(Color.Transparent);

        _density = context.Resources!.DisplayMetrics!.Density;

        // Punkt-Marker (Orange, gefuellt)
        _pointPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(255, 255, 107, 0) };
        _pointPaint.SetStyle(Paint.Style.Fill);

        // Punkt-Umrandung (Weiss)
        _pointOutlinePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            StrokeWidth = 2f * _density,
        };
        _pointOutlinePaint.SetStyle(Paint.Style.Stroke);

        // Ausgewaehlter Punkt (Cyan-Glow)
        _selectedPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(200, 0, 188, 212) };
        _selectedPaint.SetStyle(Paint.Style.Fill);

        // Kontur-Linien (Cyan)
        _contourLinePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 0, 188, 212),
            StrokeWidth = 3f * _density,
        };
        _contourLinePaint.SetStyle(Paint.Style.Stroke);

        // Kontur-Punkte (kleiner, Cyan)
        _contourPointPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(200, 0, 188, 212) };
        _contourPointPaint.SetStyle(Paint.Style.Fill);

        // Aktive Kontur (Gelb, gestrichelt)
        _activeContourPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 255, 235, 59),
            StrokeWidth = 3f * _density,
        };
        _activeContourPaint.SetStyle(Paint.Style.Stroke);
        _activeContourPaint.SetPathEffect(new DashPathEffect([10f * _density, 5f * _density], 0));

        // Label-Text
        _labelPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 11f * _density,
        };
        _labelPaint.SetShadowLayer(4f, 0f, 0f, Color.Black);

        // Hinweis-Text
        _hintPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(200, 255, 255, 255),
            TextSize = 14f * _density,
            TextAlign = Paint.Align.Center,
        };
        _hintPaint.SetShadowLayer(6f, 0f, 0f, Color.Black);

        // Distanz-Text
        _distancePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(255, 255, 235, 59),
            TextSize = 13f * _density,
            TextAlign = Paint.Align.Center,
        };
        _distancePaint.SetShadowLayer(4f, 0f, 0f, Color.Black);

        // Erkannte Planes (halbtransparent)
        _planeFillPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(40, 76, 175, 80) }; // Gruen
        _planeFillPaint.SetStyle(Paint.Style.Fill);

        _planeEdgePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(120, 76, 175, 80),
            StrokeWidth = 1.5f * _density,
        };
        _planeEdgePaint.SetStyle(Paint.Style.Stroke);

        // Snap-Indikator (Magenta Ring)
        _snapIndicatorPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(200, 233, 30, 99),
            StrokeWidth = 2f * _density,
        };
        _snapIndicatorPaint.SetStyle(Paint.Style.Stroke);
    }

    /// <summary>Projizierte Positionen vom GL-Thread aktualisieren</summary>
    public void UpdateProjectedPositions(
        List<(float screenX, float screenY, int pointIndex)> points,
        List<(float screenX, float screenY, int contourIdx, int pointIdx)> contourPoints)
    {
        _projectedPoints = points;
        _projectedContourPoints = contourPoints;
        Invalidate();
    }

    /// <summary>Projizierte Plane-Polygone aktualisieren</summary>
    public void UpdateProjectedPlanes(List<List<(float screenX, float screenY)>> planes)
    {
        _projectedPlanes = planes;
        Invalidate();
    }

    /// <summary>Plane-Anzeige ein/ausschalten</summary>
    public void SetShowPlanes(bool show)
    {
        _showPlanes = show;
        Invalidate();
    }

    /// <summary>Ausgewaehlten Punkt-Index setzen (fuer Highlight)</summary>
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
        _labelPaint.Dispose();
        _hintPaint.Dispose();
        _distancePaint.Dispose();
        _planeFillPaint.Dispose();
        _planeEdgePaint.Dispose();
        _snapIndicatorPaint.Dispose();
        base.OnDetachedFromWindow();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        var width = canvas.Width;
        var height = canvas.Height;
        var pointRadius = 8f * _density;
        var contourPointRadius = 5f * _density;

        // Erkannte Planes zeichnen (halbtransparent)
        if (_showPlanes)
            DrawDetectedPlanes(canvas);

        // Hinweis wenn noch keine Punkte
        if (_projectedPoints.Count == 0 && _projectedContourPoints.Count == 0
            && _points.Count == 0 && _contours.Count == 0)
        {
            canvas.DrawText(
                "Tippe auf eine Flaeche um einen Punkt zu setzen",
                width / 2f, height / 2f, _hintPaint);
            return;
        }

        // Kontur-Linien zeichnen (abgeschlossene Konturen)
        DrawContourLines(canvas, contourPointRadius);

        // Einzelpunkte zeichnen
        foreach (var (sx, sy, idx) in _projectedPoints)
        {
            // Auswahl-Highlight
            if (idx == _selectedIndex)
            {
                canvas.DrawCircle(sx, sy, pointRadius * 2f, _selectedPaint);
            }

            // Punkt
            canvas.DrawCircle(sx, sy, pointRadius, _pointPaint);
            canvas.DrawCircle(sx, sy, pointRadius, _pointOutlinePaint);

            // Nummer
            canvas.DrawText($"{idx + 1}", sx + pointRadius + 4, sy - 4, _labelPaint);

            // Label falls vorhanden
            if (idx < _points.Count && !string.IsNullOrEmpty(_points[idx].Label))
            {
                canvas.DrawText(_points[idx].Label!, sx + pointRadius + 4, sy + 14 * _density, _labelPaint);
            }
        }

        // Abstand zwischen letzten 2 Punkten anzeigen
        if (_projectedPoints.Count >= 2)
        {
            var last = _projectedPoints[^1];
            var prev = _projectedPoints[^2];
            var midX = (last.screenX + prev.screenX) / 2f;
            var midY = (last.screenY + prev.screenY) / 2f;

            if (last.pointIndex < _points.Count && prev.pointIndex < _points.Count)
            {
                var dist = _points[last.pointIndex].DistanceTo(_points[prev.pointIndex]);
                canvas.DrawText($"{dist:F2}m", midX, midY - 10 * _density, _distancePaint);
            }
        }

        // Status unten
        var totalPoints = _points.Count + _contours.Sum(c => c.Points.Count);
        canvas.DrawText($"{totalPoints} Punkte", width / 2f, height - 80f * _density, _hintPaint);
    }

    /// <summary>Erkannte ARCore-Planes zeichnen (halbtransparente Polygone)</summary>
    private void DrawDetectedPlanes(Canvas canvas)
    {
        foreach (var plane in _projectedPlanes)
        {
            if (plane.Count < 3) continue;

            var path = new global::Android.Graphics.Path();
            path.MoveTo(plane[0].screenX, plane[0].screenY);
            for (var i = 1; i < plane.Count; i++)
                path.LineTo(plane[i].screenX, plane[i].screenY);
            path.Close();

            canvas.DrawPath(path, _planeFillPaint);
            canvas.DrawPath(path, _planeEdgePaint);
            path.Dispose();
        }
    }

    private void DrawContourLines(Canvas canvas, float pointRadius)
    {
        // Gruppiere projizierte Kontur-Punkte nach contourIdx
        var contourGroups = _projectedContourPoints
            .GroupBy(p => p.contourIdx)
            .OrderBy(g => g.Key);

        foreach (var group in contourGroups)
        {
            var sorted = group.OrderBy(p => p.pointIdx).ToList();
            if (sorted.Count < 2) continue;

            var isActive = group.Key == -1; // -1 = aktive Kontur
            var paint = isActive ? _activeContourPaint : _contourLinePaint;

            // Linien zwischen aufeinanderfolgenden Punkten
            for (var i = 1; i < sorted.Count; i++)
            {
                canvas.DrawLine(
                    sorted[i - 1].screenX, sorted[i - 1].screenY,
                    sorted[i].screenX, sorted[i].screenY,
                    paint);
            }

            // Geschlossene Konturen: letzen mit erstem verbinden
            if (!isActive && group.Key >= 0 && group.Key < _contours.Count && _contours[group.Key].IsClosed)
            {
                canvas.DrawLine(
                    sorted[^1].screenX, sorted[^1].screenY,
                    sorted[0].screenX, sorted[0].screenY,
                    paint);
            }

            // Kontur-Punkte zeichnen
            foreach (var (sx, sy, _, _) in sorted)
            {
                canvas.DrawCircle(sx, sy, pointRadius, _contourPointPaint);
            }
        }
    }
}
