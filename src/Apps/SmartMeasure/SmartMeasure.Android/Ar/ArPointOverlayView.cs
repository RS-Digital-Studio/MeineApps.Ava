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

    // Projizierte Screen-Positionen (vom GL-Thread aktualisiert) — mit Tiefe + Bodenprojektion
    // für die räumliche (3D) Darstellung: perspektivische Marker, Höhen-Stäbe, Tiefensortierung.
    private List<(float screenX, float screenY, int pointIndex, float depth, float groundX, float groundY, float worldY)> _projectedPoints = [];
    private List<(float screenX, float screenY, int contourIdx, int pointIdx, float depth)> _projectedContourPoints = [];
    // Wiederverwendete Sortier-Liste (Painter, fern→nah) — keine LINQ-Allokation im Hot-Path.
    private readonly List<(float screenX, float screenY, int pointIndex, float depth, float groundX, float groundY, float worldY)> _pointDrawOrder = [];
    // Wiederverwendeter Puffer für die sichtbaren Punkte der aktiven Kontur (Inter-Punkt-Pillen) —
    // hält screenX/screenY + den ECHTEN Welt-pointIdx, damit die Segment-Werte korrekt zugeordnet
    // bleiben, auch wenn ein Zwischenpunkt frustum-geclippt fehlt. Keine LINQ-Allokation pro Frame.
    private readonly List<(float screenX, float screenY, int pointIdx)> _activeContourScreenScratch = [];
    private List<List<(float screenX, float screenY)>> _projectedPlanes = [];
    // Projiziertes Boden-Raster (x1,y1,x2,y2,dist) — verankert die Szene räumlich. dist = Welt-
    // Distanz zur Kamera für den Tiefen-Fade (nahe Linien kräftig, ferne ausgeblendet).
    private List<(float x1, float y1, float x2, float y2, float dist)>? _groundGridSegments;
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
    private readonly Paint _gradientFillPaint;
    private readonly Paint _labelPaint;
    private readonly Paint _hintPaint;
    private readonly Paint _distancePaint;
    private readonly Paint _planeFillPaint;
    private readonly Paint _planeEdgePaint;
    private readonly Paint _groundGridPaint;
    private readonly Paint _snapIndicatorPaint;

    // Reticle Paints
    private readonly Paint _reticleOuterPaint;
    private readonly Paint _reticleInnerPaint;
    private readonly Paint _reticleTextPaint;

    // Tracking-Banner (Hintergrund jetzt via DrawPanel — kein eigener BG-Paint mehr)
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

    // Live-Footer (Umfang / Fläche / Punkte) direkt über der Toolbar (BG via DrawPanel).
    private readonly Paint _footerLabelPaint;
    private readonly Paint _footerValuePaint;

    // Transient-Hint (BG via DrawPanel)
    private readonly Paint _transientHintTextPaint;

    // Live-Segment ("Gummiband" letzter Punkt → Crosshair): gestrichelte Linie + Werte-Pille (BG via DrawPanel)
    private readonly Paint _segLinePaint;
    private readonly Paint _segValuePaint;
    private readonly Paint _segSubPaint;

    private readonly float _density;

    public ArPointOverlayView(Context context, List<ArPoint> points, List<ArContour> contours)
        : base(context)
    {
        _points = points;
        _contours = contours;

        SetWillNotDraw(false);
        SetBackgroundColor(Color.Transparent);

        _density = context.Resources!.DisplayMetrics!.Density;

        InitDesignPaints(); // Design-System-Paints (Panel/Border/Schatten) — Tokens in ArPointOverlayView.Design.cs

        _pointPaint = new Paint(PaintFlags.AntiAlias) { Color = C.Accent };
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

        // Flächen-Füllung mit vertikalem Tiefen-Gradient (Shader wird pro Polygon gesetzt) —
        // gibt geschlossenen Flächen plastisches Volumen statt flacher Einfärbung.
        _gradientFillPaint = new Paint(PaintFlags.AntiAlias);
        _gradientFillPaint.SetStyle(Paint.Style.Fill);

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

        // Boden-Raster: dezente helle Linien, Alpha wird pro Segment für den Tiefen-Fade gesetzt.
        _groundGridPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(70, 220, 230, 240),
            StrokeWidth = 1f * _density,
        };
        _groundGridPaint.SetStyle(Paint.Style.Stroke);

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

        _bannerTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextPrimary,
            TextSize = 14f * _density,
            TextAlign = Paint.Align.Center,
        };
        _bannerTextPaint.SetTypeface(FontMedium);

        _statsBgPaint = new Paint(PaintFlags.AntiAlias) { Color = Color.Argb(180, 0, 0, 0) };
        _statsBgPaint.SetStyle(Paint.Style.Fill);

        _statsTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextPrimary,
            TextSize = 14f * _density,
        };
        _statsTextPaint.SetTypeface(FontMedium);

        _statsLabelPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextSecondary,
            TextSize = 11f * _density,
            LetterSpacing = 0.06f,
        };
        _statsLabelPaint.SetTypeface(FontRegular);

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
        _footerLabelPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextSecondary,
            TextSize = 11f * _density,
            TextAlign = Paint.Align.Center,
            LetterSpacing = 0.06f, // gesperrte Versal-Labels = klassisches Instrument-Panel-Signal
        };
        _footerLabelPaint.SetTypeface(FontRegular);

        _footerValuePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextPrimary,
            TextSize = 17f * _density,
            TextAlign = Paint.Align.Center,
        };
        _footerValuePaint.SetTypeface(FontMedium);

        // Live-Segment: gestrichelte Linie (Farbe wird pro Frame nach HitQuality gesetzt),
        // dunkle Pille mit grossem Hauptwert + kleiner ΔH/Steigung-Zeile.
        _segLinePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(235, 255, 235, 59),
            StrokeWidth = 3f * _density,
            StrokeCap = Paint.Cap.Round,
        };
        _segLinePaint.SetStyle(Paint.Style.Stroke);
        _segLinePaint.SetPathEffect(new DashPathEffect([12f * _density, 6f * _density], 0));

        _segValuePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextPrimary,
            TextSize = 17f * _density,
            TextAlign = Paint.Align.Center,
        };
        _segValuePaint.SetTypeface(FontMedium);

        _segSubPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextSecondary,
            TextSize = 11f * _density,
            TextAlign = Paint.Align.Center,
        };
        _segSubPaint.SetTypeface(FontRegular);

        _transientHintTextPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.TextPrimary,
            TextSize = 13f * _density,
            TextAlign = Paint.Align.Center,
        };
        _transientHintTextPaint.SetTypeface(FontMedium);
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
        List<(float screenX, float screenY, int pointIndex, float depth, float groundX, float groundY, float worldY)> points,
        List<(float screenX, float screenY, int contourIdx, int pointIdx, float depth)> contourPoints)
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

    /// <summary>Übernimmt das projizierte Boden-Raster (null = aus / kein Ground-Tracking).</summary>
    public void UpdateGroundGrid(List<(float x1, float y1, float x2, float y2, float dist)>? segments)
    {
        _groundGridSegments = segments;
        Invalidate();
    }

    /// <summary>Plan-Kap. 5.15: Heatmap-Berechnung im GL-Thread braucht den letzten
    /// Plane-Snapshot. View liefert die Live-Liste zur Iteration — Caller darf nicht
    /// mutieren. List&lt;List&lt;...&gt;&gt; kann nicht als IReadOnlyList&lt;IReadOnlyList&gt;
    /// covariant zurueckgegeben werden, daher direkter Typ.</summary>
    public IReadOnlyList<List<(float screenX, float screenY)>> GetProjectedPlanesSnapshot()
        => _projectedPlanes;

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
        _gradientFillPaint.Dispose();
        _labelPaint.Dispose();
        _hintPaint.Dispose();
        _distancePaint.Dispose();
        _planeFillPaint.Dispose();
        _planeEdgePaint.Dispose();
        _groundGridPaint.Dispose();
        _snapIndicatorPaint.Dispose();
        _reticleOuterPaint.Dispose();
        _reticleInnerPaint.Dispose();
        _reticleTextPaint.Dispose();
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
        _footerLabelPaint.Dispose();
        _footerValuePaint.Dispose();
        _transientHintTextPaint.Dispose();
        _segLinePaint.Dispose();
        _segValuePaint.Dispose();
        _segSubPaint.Dispose();
        DisposeDesignPaints(); // Design-System-Paints/-Path (ArPointOverlayView.Design.cs)
        base.OnDetachedFromWindow();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        var width = canvas.Width;
        var height = canvas.Height;
        var pointRadius = 8f * _density;
        var contourPointRadius = 5f * _density;

        // 0. Boden-Raster (3D-Tiefenanker der Szene) — ganz hinten, unter allen Messdaten.
        DrawGroundGrid(canvas);

        // 1. Erkannte Planes
        if (_showPlanes)
            DrawDetectedPlanes(canvas);

        // 2. Kontur-Flächen (geschlossen typ-gefärbt + aktiv) mit Tiefen-Gradient
        DrawContourFills(canvas);

        // 3. Kontur-Linien + Auto-Close-Vorschau
        DrawContourLines(canvas, contourPointRadius);

        // 4. Einzelpunkte
        DrawPoints(canvas, pointRadius);

        // 5. Distanz-Labels zwischen ALLEN aufeinanderfolgenden Punkten
        DrawInterPointDistances(canvas);

        // 5c. Live-Segment ("Gummiband") vom letzten gesetzten Punkt zum Crosshair —
        // unter dem Reticle, damit das Reticle obenauf liegt.
        if (_state.ShowLiveSegment)
            DrawRubberBand(canvas, width, height);
        else if (_state.LiveSegmentActive)
            DrawOffScreenLiveSegment(canvas, width, height); // Distanz/ΔH wenn Vorpunkt off-screen

        // 5d. Rechteck-/Quadrat-Vorschau (gefuehrte 3-Punkt-Methode) — unter dem Reticle.
        if (_state.IsRectangleMode)
            DrawRectanglePreview(canvas, width, height);

        // 6. Reticle mit HitQuality-Färbung (nur bei Tracking)
        if (_state.IsTracking)
            DrawReticle(canvas, width, height);

        // 5b. Plan-Kap. 5.15: Quality-Heatmap-Overlay (halb-transparent ueber Kamera-Frame).
        // Liegt VOR Site-Markern und Punkten, damit Vermessungs-Daten lesbar bleiben.
        if (_state.QualityHeatmapGrid != null)
            DrawQualityHeatmap(canvas, width, height);

        // 6a. Plan-Kap. 5.2: Site-Marker (Earth-Anchor-Cache, bestehende Projekt-Punkte)
        // VOR Tape-Measure und Reticle, damit aktive Punkte/Reticle ueber den Site-Markern liegen.
        if (_state.SiteMarkerScreenPoints != null && _state.SiteMarkerScreenPoints.Count > 0)
            DrawSiteMarkers(canvas);

        // 6a2. Plan-Kap. 5.8: RTK-Stab-Live-Marker (eigene Fix-Quality-Farbe)
        if (_state.RtkStabScreenPos.HasValue)
            DrawRtkStabMarker(canvas);

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

        // 12b. Modus-Chip (oben mittig): aktiver Modus + nächster Schritt. Nur bei Tracking,
        // sonst belegt das Tracking-Banner die obere Mitte.
        if (_state.IsTracking)
            DrawModeChip(canvas, width);

        // 13. Transient-Hint (falls aktiv)
        DrawTransientHint(canvas, width, height);

        // 13. Empty-State wenn keine Punkte/Konturen (im Rechteck-Modus erst, solange noch
        // keine Ecke gesetzt wurde — danach fuehrt die Transient-Hint durch die Schritte).
        if (_projectedPoints.Count == 0 && _projectedContourPoints.Count == 0
            && _points.Count == 0 && _contours.Count == 0
            && _state.RectangleCornerCount == 0
            && !_state.IsStakeoutMode && !_state.IsTapeMeasureMode
            && _state.IsTracking)
        {
            string hint;
            if (_projectedPlanes.Count == 0)
                hint = "Bewege die Kamera langsam über den Boden…";
            else if (_state.IsRectangleMode)
                hint = "Rechteck: erste Ecke der Basiskante antippen";
            else
                hint = "Tippe auf eine Fläche um einen Punkt zu setzen";

            // Dezentes Glas-Panel um den Hinweis (design-konsistent, besser lesbar auf hellem Boden).
            var hintW = _hintPaint.MeasureText(hint);
            var hcx = width / 2f;
            var panelH = 44f * _density;
            var panelTop = height / 2f + 40f * _density;
            var hpadH = 20f * _density;
            _panelRect.Set(hcx - hintW / 2f - hpadH, panelTop, hcx + hintW / 2f + hpadH, panelTop + panelH);
            DrawPanel(canvas, _panelRect, RadiusPanel, PanelTone.Neutral, raised: true);
            canvas.DrawText(hint, hcx, panelTop + panelH / 2f + 5f * _density, _hintPaint);
        }
    }

    /// <summary>Zeichnet das projizierte Boden-Raster mit Tiefen-Fade (nahe Linien kräftig,
    /// ferne ausgeblendet) — erzeugt den räumlichen Boden-Bezug der AR-Szene.</summary>
    private void DrawGroundGrid(Canvas canvas)
    {
        var segs = _groundGridSegments;
        if (segs == null || segs.Count == 0) return;

        const float nearDist = 1.5f;
        const float farDist = 7.75f;
        const int nearAlpha = 85;
        const int farAlpha = 10;

        foreach (var (x1, y1, x2, y2, dist) in segs)
        {
            var t = Math.Clamp((dist - nearDist) / (farDist - nearDist), 0f, 1f);
            _groundGridPaint.Alpha = (int)(nearAlpha + (farAlpha - nearAlpha) * t);
            canvas.DrawLine(x1, y1, x2, y2, _groundGridPaint);
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

    /// <summary>Füllt geschlossene Konturen (Typ-Farbe) und die aktive Kontur (Accent) mit einem
    /// vertikalen Tiefen-Gradient — plastisches Flächen-Volumen statt flacher Einfärbung.</summary>
    private void DrawContourFills(Canvas canvas)
    {
        // Geschlossene Konturen typ-gefärbt füllen (unter den Linien).
        var groups = _projectedContourPoints
            .Where(p => p.contourIdx >= 0)
            .GroupBy(p => p.contourIdx);
        foreach (var g in groups)
        {
            var idx = g.Key;
            if (idx >= _contours.Count || !_contours[idx].IsClosed) continue;
            var pts = g.OrderBy(p => p.pointIdx).Select(p => (p.screenX, p.screenY)).ToList();
            if (pts.Count < 3) continue;
            FillPolygonGradient(canvas, pts, GetContourTypeColor(_contours[idx].ContourType));
        }

        // Aktive Kontur (Accent) füllen, sobald sie eine Fläche aufspannt.
        var activePts = _projectedContourPoints
            .Where(p => p.contourIdx == -1)
            .OrderBy(p => p.pointIdx)
            .Select(p => (p.screenX, p.screenY))
            .ToList();
        if (activePts.Count >= 3)
            FillPolygonGradient(canvas, activePts, C.Accent);
    }

    /// <summary>Zeichnet ein gefülltes Polygon mit vertikalem Alpha-Gradient (oben dezent, unten
    /// kräftiger) in der Basisfarbe. Shader wird pro Polygon erzeugt — bei den wenigen gleichzeitig
    /// sichtbaren Flächen einer AR-Session vernachlässigbar gegenüber dem Kamera-Frame-Upload.</summary>
    private void FillPolygonGradient(Canvas canvas, List<(float screenX, float screenY)> pts, Color baseColor)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var (_, sy) in pts)
        {
            if (sy < minY) minY = sy;
            if (sy > maxY) maxY = sy;
        }
        if (maxY - minY < 1f) maxY = minY + 1f;

        using var path = new global::Android.Graphics.Path();
        path.MoveTo(pts[0].screenX, pts[0].screenY);
        for (var i = 1; i < pts.Count; i++)
            path.LineTo(pts[i].screenX, pts[i].screenY);
        path.Close();

        using var shader = new LinearGradient(
            0, minY, 0, maxY,
            Color.Argb(38, baseColor.R, baseColor.G, baseColor.B),
            Color.Argb(96, baseColor.R, baseColor.G, baseColor.B),
            Shader.TileMode.Clamp!);
        _gradientFillPaint.SetShader(shader);
        canvas.DrawPath(path, _gradientFillPaint);
        _gradientFillPaint.SetShader(null);
    }

    /// <summary>Farbe pro ArContourType (für Multi-Kontur-Visualisierung in Gartenplanung).</summary>
    internal static Color GetContourTypeColor(ArContourType type) => type switch
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

            // Konturpunkte perspektivisch skalieren (nah = größer) — konsistent mit Einzelpunkten.
            foreach (var (sx, sy, _, _, depth) in sorted)
            {
                var ds = Math.Clamp(MarkerReferenceDepth / MathF.Max(depth, 0.3f), 0.5f, 1.7f);
                canvas.DrawCircle(sx, sy, pointRadius * ds, _contourPointPaint);
            }

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

    /// <summary>Referenz-Tiefe (m) für die perspektivische Marker-Skalierung: ein Punkt in
    /// dieser Entfernung wird in Normalgröße gezeichnet, näher = größer, ferner = kleiner.</summary>
    private const float MarkerReferenceDepth = 2.5f;

    private void DrawPoints(Canvas canvas, float pointRadius)
    {
        if (_projectedPoints.Count == 0) return;

        var nowUtc = DateTime.UtcNow;
        var anyAnimating = false;

        // Painter-Algorithmus: ferne Punkte zuerst, damit nahe Marker + Stäbe sie überdecken.
        // 3D-Tiefenwirkung entsteht erst durch korrekte Zeichenreihenfolge.
        _pointDrawOrder.Clear();
        _pointDrawOrder.AddRange(_projectedPoints);
        _pointDrawOrder.Sort(static (a, b) => b.depth.CompareTo(a.depth));

        foreach (var (sx, sy, idx, depth, gx, gy, _) in _pointDrawOrder)
        {
            // Perspektive: nahe Punkte wirken groß, ferne klein. Begrenzt, damit ein Punkt
            // direkt vor der Linse nicht den ganzen Screen einnimmt (1.9×) bzw. ferne Punkte
            // noch antippbar bleiben (0.45×).
            var depthScale = Math.Clamp(MarkerReferenceDepth / MathF.Max(depth, 0.3f), 0.45f, 1.9f);

            var confidence = 1f;
            float stdDev = 0f;
            DateTime timestamp = nowUtc;
            if (idx < _points.Count)
            {
                confidence = _points[idx].Confidence;
                stdDev = _points[idx].PositionStdDev;
                timestamp = _points[idx].Timestamp;
            }
            // Confidence-Ampel ersetzt das kryptische ~/?-HitQuality-Zeichen: grün/gelb/rot
            // direkt als Marker-Outline-Ring (siehe unten).
            var confColor = confidence >= 0.7f ? C.Good : confidence >= 0.45f ? C.Medium : C.Poor;

            var effectiveR = MathF.Max(pointRadius * 0.75f, pointRadius * (0.85f + 0.15f * confidence)) * depthScale;

            // Pop-In-Animation: junge Punkte starten 2.2× groß und schrumpfen auf Normalgröße.
            var ageMs = (nowUtc - timestamp).TotalMilliseconds;
            if (ageMs >= 0 && ageMs < PointBornAnimationMs)
            {
                var t = (float)(ageMs / PointBornAnimationMs);
                var ease = 1f - (1f - t) * (1f - t); // Ease-out-Quadratic
                effectiveR *= 2.2f - 1.2f * ease;
                anyAnimating = true;
            }

            // ── 3D-Höhenbezug: Bodenschatten + Stab vom Boden zum Punkt ──────────────────
            // Macht die Höhe über Grund räumlich lesbar (Punkt schwebt über seinem Schatten).
            var stickPx = gy - sy;                          // >0 wenn Punkt über seiner Bodenprojektion
            var hasStick = stickPx > 6f * _density;
            if (hasStick)
            {
                // Bodenschatten: flache, perspektivisch gestauchte Ellipse am Fußpunkt.
                var shW = effectiveR * 1.15f;
                var shH = effectiveR * 0.42f;
                canvas.DrawOval(gx - shW, gy - shH, gx + shW, gy + shH, _groundShadowPaint);

                // Fußpunkt-Tick (kurze Querlinie am Boden) — verankert den Stab sichtbar.
                _heightStickPaint.Color = WithAlpha(confColor, 130);
                _heightStickPaint.StrokeWidth = MathF.Max(1.5f * _density, 2f * _density * depthScale);
                canvas.DrawLine(gx - 5f * _density, gy, gx + 5f * _density, gy, _heightStickPaint);

                // Höhen-Stab: Linie Boden → Punkt, in Confidence-Farbe.
                _heightStickPaint.Color = WithAlpha(confColor, 210);
                _heightStickPaint.StrokeWidth = MathF.Max(1.5f * _density, 2.5f * _density * depthScale);
                canvas.DrawLine(gx, gy, sx, sy, _heightStickPaint);
            }

            // ── Marker selbst ────────────────────────────────────────────────────────────
            if (idx == _selectedIndex)
                canvas.DrawCircle(sx, sy, effectiveR + 7f * _density, _selectedPaint);

            canvas.DrawCircle(sx, sy, effectiveR, _pointPaint);

            // Confidence-Ring statt weißer Outline: Farbe codiert Messqualität (Ampel).
            var origColor = _pointOutlinePaint.Color;
            var origStroke = _pointOutlinePaint.StrokeWidth;
            _pointOutlinePaint.Color = confColor;
            _pointOutlinePaint.StrokeWidth = MathF.Max(1.75f * _density, 2.5f * _density * depthScale);
            canvas.DrawCircle(sx, sy, effectiveR, _pointOutlinePaint);
            _pointOutlinePaint.Color = origColor;
            _pointOutlinePaint.StrokeWidth = origStroke;

            // Punkt-Nummer im Marker-Zentrum — bei sehr kleinen fernen Markern weglassen.
            if (effectiveR >= 7f * _density)
                canvas.DrawText($"{idx + 1}", sx, sy + 3.5f * _density, _markerNumPaint);

            // Höhen-Delta am Stab-Kopf (relativ zum ersten Punkt) — die wichtigste 3D-Info,
            // immer sichtbar (nicht nur bei Auswahl), aber bei winzigen fernen Markern gespart.
            if (idx > 0 && idx < _points.Count && effectiveR >= 6f * _density)
            {
                var dh = _points[idx].Y - _points[0].Y;
                if (MathF.Abs(dh) >= 0.02f)
                {
                    var text = dh > 0 ? $"+{dh:0.00} m" : $"−{MathF.Abs(dh):0.00} m";
                    canvas.DrawText(text, sx, sy - effectiveR - 7f * _density, _markerLabelPaint);
                }
            }

            // Progressive Disclosure: Label, σ und Kameradistanz nur am SELEKTIERTEN Punkt —
            // hält die Szene ruhig, Details auf Abruf.
            if (idx == _selectedIndex && idx < _points.Count)
            {
                var p = _points[idx];
                var ly = sy + effectiveR + 16f * _density;
                if (!string.IsNullOrEmpty(p.Label))
                {
                    canvas.DrawText(p.Label!, sx, ly, _markerLabelPaint);
                    ly += 15f * _density;
                }
                if (stdDev > 0.005f)
                {
                    canvas.DrawText($"±{stdDev * 100:F1} cm", sx, ly, _markerLabelPaint);
                    ly += 15f * _density;
                }
                canvas.DrawText($"{depth:0.0} m", sx, ly, _markerLabelPaint);
            }
        }

        // Solange Pop-In-Animation läuft, alle 16ms neu zeichnen (60fps).
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
                ? "Keine Stakeout-Ziele"
                : "Warte auf GPS / VPS …";
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

    /// <summary>Plan-Kap. 5.15: Quality-Heatmap als halb-transparenter Overlay.
    /// Patch-Score 0..1 wird auf Rot→Gelb→Gruen interpoliert. Patches mit Score=0 werden
    /// nicht gezeichnet (kein Sky-Rot ueber dem ganzen Bild).</summary>
    private void DrawQualityHeatmap(Canvas canvas, int width, int height)
    {
        var grid = _state.QualityHeatmapGrid;
        if (grid == null) return;
        var cols = _state.QualityHeatmapCols;
        var rows = _state.QualityHeatmapRows;
        if (cols <= 0 || rows <= 0) return;

        var patchW = width / (float)cols;
        var patchH = height / (float)rows;

        using var paint = new Paint(PaintFlags.AntiAlias);

        for (var c = 0; c < cols; c++)
        {
            for (var r = 0; r < rows; r++)
            {
                var score = grid[c, r];
                if (score < 0.05f) continue; // unbelegte Patches transparent lassen

                paint.Color = HeatmapColor(score);
                var x = c * patchW;
                var y = r * patchH;
                canvas.DrawRect(x, y, x + patchW, y + patchH, paint);
            }
        }
    }

    /// <summary>Rot (Score 0) → Gelb (0.5) → Gruen (1.0). Alpha 90 fuer dezente
    /// Halb-Transparenz ueber dem Kamera-Frame.</summary>
    private static Color HeatmapColor(float score)
    {
        score = Math.Clamp(score, 0f, 1f);
        int r, g;
        if (score < 0.5f)
        {
            r = 244;
            g = (int)(score * 2f * 235);
        }
        else
        {
            r = (int)((1f - (score - 0.5f) * 2f) * 244);
            g = 175;
        }
        return Color.Argb(90, r, g, 0);
    }

    /// <summary>Plan-Kap. 5.8: RTK-Stab-Live-Marker. Pulsierender Ring + Fix-Quality-Farbe
    /// (Gruen=RTK-Fix, Gelb=Float, Orange=DGPS, Rot=GPS-only). Visuell deutlich, weil der
    /// User den Stab im Garten manchmal aus dem Sichtfeld verliert und froh ist wenn er
    /// sieht "ah, da steht er".</summary>
    private void DrawRtkStabMarker(Canvas canvas)
    {
        if (!_state.RtkStabScreenPos.HasValue) return;
        var (sx, sy) = _state.RtkStabScreenPos.Value;

        var color = _state.RtkStabFixQuality switch
        {
            4 => Color.Argb(240, 76, 175, 80),     // RTK-Fix: Gruen
            5 => Color.Argb(240, 255, 235, 59),    // Float: Gelb
            2 => Color.Argb(240, 255, 152, 0),     // DGPS: Orange
            1 => Color.Argb(240, 244, 67, 54),     // GPS-only: Rot
            _ => Color.Argb(180, 158, 158, 158),   // Aus: Grau
        };

        using var ringPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
            StrokeWidth = 3f * _density,
        };
        ringPaint.SetStyle(Paint.Style.Stroke);

        using var dotPaint = new Paint(PaintFlags.AntiAlias) { Color = color };
        dotPaint.SetStyle(Paint.Style.Fill);

        // Pulsierender Outer-Ring (1Hz)
        var pulse = (float)(Math.Sin(DateTime.UtcNow.Millisecond / 1000.0 * Math.PI * 2) * 0.5 + 0.5);
        var outerR = 14f * _density + 4f * _density * pulse;
        canvas.DrawCircle(sx, sy, outerR, ringPaint);

        // Innerer Dot
        canvas.DrawCircle(sx, sy, 5f * _density, dotPaint);

        // Label "Stab"
        using var lblPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = 12f * _density,
            TextAlign = Paint.Align.Center,
        };
        lblPaint.SetShadowLayer(3f, 0f, 1f, Color.Black);
        canvas.DrawText("Stab", sx, sy + outerR + 12f * _density, lblPaint);

        // Damit der Pulse animiert, eine kleine Invalidate-Schleife (~30fps)
        PostInvalidateDelayed(33);
    }

    /// <summary>Plan-Kap. 5.2: Site-Marker (bestehende Projekt-Punkte aus Earth-Anchor-Cache).
    /// Visualisiert als kleine graue Kreise mit duenner Outline und Label — bewusst dezent,
    /// damit aktive Punkte (orange) klar im Vordergrund bleiben.</summary>
    private void DrawSiteMarkers(Canvas canvas)
    {
        var markers = _state.SiteMarkerScreenPoints;
        if (markers == null || markers.Count == 0) return;

        using var fillPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(180, 158, 158, 158), // Grau-transparent
        };
        fillPaint.SetStyle(Paint.Style.Fill);

        using var outlinePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 33, 33, 33),
            StrokeWidth = 1.5f * _density,
        };
        outlinePaint.SetStyle(Paint.Style.Stroke);

        using var labelPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(220, 240, 240, 240),
            TextSize = 11f * _density,
            TextAlign = Paint.Align.Left,
        };
        labelPaint.SetShadowLayer(3f, 0f, 1f, Color.Black);

        var r = 5f * _density;
        foreach (var (sx, sy, label) in markers)
        {
            canvas.DrawCircle(sx, sy, r, fillPaint);
            canvas.DrawCircle(sx, sy, r, outlinePaint);
            if (!string.IsNullOrEmpty(label))
                canvas.DrawText(label, sx + r + 4f * _density, sy + 4f * _density, labelPaint);
        }
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

        DrawPanel(canvas, rect, RadiusPanel);

        // 2 Spalten: Punkte / Summe
        var colW = (rect.Width()) / 2f;
        var labelY = rect.Top + 16 * _density;
        var valueY = rect.Top + 38 * _density;

        var count = _state.TapeMeasureScreenPoints?.Count ?? 0;
        var cx1 = rect.Left + colW / 2f;
        canvas.DrawText(_state.Labels.Points, cx1, labelY, _footerLabelPaint);
        canvas.DrawText(count.ToString(), cx1, valueY, _footerValuePaint);

        var cx2 = rect.Left + colW * 1.5f;
        canvas.DrawText("Summe", cx2, labelY, _footerLabelPaint);
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

    /// <summary>
    /// Live-Vorschau des gefuehrten Rechtecks/Quadrats: gesetzte Ecken als Marken, die
    /// Basiskante bzw. das aufgespannte Rechteck als Polygon (Fill + Kanten) plus Laenge/
    /// Tiefe/Flaeche als Pillen. Gruen-getoent + "Quadrat"-Unterzeile, wenn der Quadrat-Snap greift.
    /// </summary>
    private void DrawRectanglePreview(Canvas canvas, int width, int height)
    {
        var corners = _state.RectangleCornerScreenPoints;
        var preview = _state.RectanglePreviewScreenPoints;
        var cx = _state.ReticleX > 0 ? _state.ReticleX : width / 2f;
        var cy = _state.ReticleY > 0 ? _state.ReticleY : height / 2f;

        var accent = _state.RectangleIsSquare
            ? Color.Argb(255, 76, 175, 80)     // Gruen bei Quadrat-Snap
            : Color.Argb(255, 255, 107, 0);    // Orange sonst

        using var edgePaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = accent,
            StrokeWidth = 4f * _density,
            StrokeCap = Paint.Cap.Round,
            StrokeJoin = Paint.Join.Round,
        };
        edgePaint.SetStyle(Paint.Style.Stroke);

        // Branch-Entscheidung gegen die ECHTE Eckenzahl (nicht die ggf. frustum-geclippte
        // Screen-Liste — sonst kollabiert die Vorschau, wenn eine Ecke hinter die Kamera kommt).
        var cornerCount = _state.RectangleCornerCount;

        if (preview != null && preview.Count == 4)
        {
            // Vollstaendiges Rechteck (2 Ecken gesetzt + alle vier Ecken projizierbar)
            using var path = new global::Android.Graphics.Path();
            path.MoveTo(preview[0].screenX, preview[0].screenY);
            for (var i = 1; i < 4; i++)
                path.LineTo(preview[i].screenX, preview[i].screenY);
            path.Close();
            // Plastischer Tiefen-Gradient in der Akzentfarbe (grün bei Quadrat-Snap, sonst orange).
            FillPolygonGradient(canvas,
                [(preview[0].screenX, preview[0].screenY), (preview[1].screenX, preview[1].screenY),
                 (preview[2].screenX, preview[2].screenY), (preview[3].screenX, preview[3].screenY)],
                accent);
            canvas.DrawPath(path, edgePaint);

            // Kanten-Pillen: Basislaenge (Kante 0->1), Tiefe (Kante 1->2)
            DrawEdgeLabel(canvas, preview[0], preview[1], FormatMeters(_state.RectangleLengthMeters));
            DrawEdgeLabel(canvas, preview[1], preview[2], FormatMeters(_state.RectangleDepthMeters));

            // Flaeche + ggf. Quadrat-Hinweis im Zentrum. Schwelle = Builder-Minimum (0,05²),
            // damit auch sehr kleine gueltige Rechtecke eine Flaechen-Pille bekommen.
            var mcx = (preview[0].screenX + preview[2].screenX) * 0.5f;
            var mcy = (preview[0].screenY + preview[2].screenY) * 0.5f;
            // Vom Reticle wegschieben, falls das Zentrum zu nah an der Bildmitte liegt
            // (bei kleinen/zentrierten Rechtecken sonst Overlap mit Reticle + Distanz-Label).
            if (MathF.Abs(mcy - cy) < 46f * _density && MathF.Abs(mcx - cx) < 80f * _density)
                mcy = cy + 52f * _density;
            if (_state.RectangleAreaMeters >= 0.0025f)
                DrawValuePill(canvas, mcx, mcy, $"{_state.RectangleAreaMeters:F2} m²",
                    _state.RectangleIsSquare ? "Quadrat" : null);
        }
        else if (cornerCount == 2 && corners is { Count: 2 })
        {
            // 2 Ecken gesetzt, aber das Tiefen-Polygon (noch) nicht voll projizierbar
            // (kein Reticle-Hit oder Gegenecke hinter der Kamera) → nur die Basiskante.
            canvas.DrawLine(corners[0].screenX, corners[0].screenY,
                corners[1].screenX, corners[1].screenY, edgePaint);
            DrawEdgeLabel(canvas, corners[0], corners[1], FormatMeters(_state.RectangleLengthMeters));
        }
        else if (cornerCount == 1 && corners is { Count: 1 })
        {
            // 1 Ecke gesetzt → gestrichelte Live-Basiskante zur Reticle-Position + Distanz-Pille.
            using var dash = new Paint(PaintFlags.AntiAlias)
            {
                Color = accent,
                StrokeWidth = 3f * _density,
            };
            dash.SetStyle(Paint.Style.Stroke);
            using var dashEffect = new global::Android.Graphics.DashPathEffect(
                [12f * _density, 8f * _density], 0f);
            dash.SetPathEffect(dashEffect);
            canvas.DrawLine(corners[0].screenX, corners[0].screenY, cx, cy, dash);

            // Live-Distanz der entstehenden Basiskante am Linien-Mittelpunkt (nur bei Reticle-Hit).
            if (_state.RectangleLengthMeters > 0.001f)
                DrawValuePill(canvas, (corners[0].screenX + cx) * 0.5f, (corners[0].screenY + cy) * 0.5f,
                    FormatMeters(_state.RectangleLengthMeters), null);
        }

        // Gesetzte Eckmarken obenauf
        if (corners is { Count: > 0 })
        {
            using var cornerFill = new Paint(PaintFlags.AntiAlias) { Color = accent };
            cornerFill.SetStyle(Paint.Style.Fill);
            using var cornerOutline = new Paint(PaintFlags.AntiAlias)
            {
                Color = Color.Argb(255, 0, 0, 0),
                StrokeWidth = 2f * _density,
            };
            cornerOutline.SetStyle(Paint.Style.Stroke);
            var cr = 7f * _density;
            foreach (var (sx, sy) in corners)
            {
                canvas.DrawCircle(sx, sy, cr, cornerFill);
                canvas.DrawCircle(sx, sy, cr, cornerOutline);
            }
        }
    }

    /// <summary>Distanz-Pille am Mittelpunkt einer Kante.</summary>
    private void DrawEdgeLabel(Canvas canvas,
        (float screenX, float screenY) a, (float screenX, float screenY) b, string text)
    {
        var mx = (a.screenX + b.screenX) * 0.5f;
        var my = (a.screenY + b.screenY) * 0.5f;
        DrawValuePill(canvas, mx, my, text, null);
    }

    /// <summary>Distanz-Labels zwischen ALLEN aufeinanderfolgenden Punkten — vorher nur zwischen letzten 2.</summary>
    private void DrawInterPointDistances(Canvas canvas)
    {
        // Einzelpunkte (Point-Modus): horizontale Distanz + ΔH zwischen aufeinanderfolgenden
        // Punkten als Pille — bleibt nach dem Setzen stehen. _projectedPoints liegt bereits
        // aufsteigend nach pointIndex vor (ProjectPointsToScreen pusht i=0..n), daher direkt
        // iterieren — keine Per-Frame-Sortierung/-Liste. Die Distanz kommt aus den echten
        // Punkt-Objekten (Index-robust), egal welche Zwischenpunkte off-screen wandern.
        for (var i = 1; i < _projectedPoints.Count; i++)
        {
            var curr = _projectedPoints[i];
            var prev = _projectedPoints[i - 1];
            if (curr.pointIndex >= _points.Count || prev.pointIndex >= _points.Count) continue;

            var pa = _points[prev.pointIndex];
            var pb = _points[curr.pointIndex];
            var midX = (curr.screenX + prev.screenX) / 2f;
            var midY = (curr.screenY + prev.screenY) / 2f;
            DrawSegmentPill(canvas, midX, midY, pa.Distance2DTo(pb), pb.Y - pa.Y);
        }

        // Aktive Kontur: horizontale Distanz + ΔH pro gesetztem Segment (Welt-Werte vom
        // GL-Thread in ActiveContourSegments — der Overlay-View kennt nur Screen-Pixel).
        // ActiveContourSegments ist lückenlos über die ECHTEN Punkt-Indizes gebaut (Eintrag i =
        // Segment Punkt i→i+1), die projizierten Screen-Punkte können dagegen frustum-geclippt
        // fehlen. Daher das Segment über den ECHTEN pointIdx greifen und nur echte Nachbar-Paare
        // (pointIdx+1) beschriften — sonst landet die Strecke des falschen Segments am falschen Ort.
        var segs = _state.ActiveContourSegments;
        if (segs == null) return;

        _activeContourScreenScratch.Clear();
        foreach (var p in _projectedContourPoints)
            if (p.contourIdx == -1)
                _activeContourScreenScratch.Add((p.screenX, p.screenY, p.pointIdx));

        for (var i = 0; i < _activeContourScreenScratch.Count - 1; i++)
        {
            var a = _activeContourScreenScratch[i];
            var b = _activeContourScreenScratch[i + 1];
            if (b.pointIdx != a.pointIdx + 1) continue;        // Lücke durch geclippten Zwischenpunkt
            if (a.pointIdx < 0 || a.pointIdx >= segs.Count) continue;
            var midX = (a.screenX + b.screenX) / 2f;
            var midY = (a.screenY + b.screenY) / 2f;
            DrawSegmentPill(canvas, midX, midY, segs[a.pointIdx].horizontal, segs[a.pointIdx].heightDelta);
        }
    }

    /// <summary>Gesetztes Segment: Pille mit Distanz (Haupt) + ΔH (Unterzeile, nur wenn ≥ 5 mm).
    /// Identischer Stil wie das Live-Gummiband → die Werte "frieren ein", sobald der naechste
    /// Punkt gesetzt ist.</summary>
    private void DrawSegmentPill(Canvas canvas, float midX, float midY, float horizontal, float heightDelta)
    {
        var sub = MathF.Abs(heightDelta) >= 0.005f ? $"ΔH {heightDelta:+0.00;-0.00;0.00} m" : null;
        DrawValuePill(canvas, midX, midY, FormatMeters(horizontal), sub);
    }

    /// <summary>Live-Segment-Gummiband: gestrichelte Linie vom zuletzt gesetzten Punkt zum
    /// Crosshair + schwebende Werte-Pille (Horizontaldistanz gross, ΔH + Steigung klein) am
    /// Linien-Mittelpunkt. Zeigt dem Vermesser beim Zeichnen kontinuierlich Strecke und
    /// Hoehenunterschied zum Vorgaengerpunkt — der primaere Mess-Wert am Blickpunkt.</summary>
    private void DrawRubberBand(Canvas canvas, int width, int height)
    {
        if (_state.LiveSegmentFromScreen is not { } from) return;

        var cx = _state.ReticleX > 0 ? _state.ReticleX : width / 2f;
        var cy = _state.ReticleY > 0 ? _state.ReticleY : height / 2f;

        // Linienfarbe nach HitQuality (gleiche Codierung wie Reticle + gesetzte Punkte).
        _segLinePaint.Color = _state.HitQuality switch
        {
            ArHitQuality.Plane => Color.Argb(235, 76, 175, 80),
            ArHitQuality.Point => Color.Argb(235, 255, 107, 0),
            ArHitQuality.InstantPlacement => Color.Argb(235, 255, 235, 59),
            _ => Color.Argb(200, 255, 255, 255),
        };
        canvas.DrawLine(from.screenX, from.screenY, cx, cy, _segLinePaint);

        if (_state.LiveSegmentHorizontalMeters is not { } horiz) return;

        var mainText = FormatMeters(horiz);
        string? sub = null;
        if (_state.LiveSegmentHeightDelta is { } dh)
        {
            var slope = horiz > 0.05f ? $"   {dh / horiz * 100f:+0.0;-0.0;0.0} %" : "";
            sub = $"ΔH {dh:+0.00;-0.00;0.00} m{slope}";
        }

        var mx = (from.screenX + cx) * 0.5f;
        var my = (from.screenY + cy) * 0.5f;
        DrawValuePill(canvas, mx, my, mainText, sub);
    }

    /// <summary>Live-Distanz + ΔH zum vorherigen Punkt am Reticle anzeigen, wenn der Vorpunkt
    /// AUSSERHALB des Bildes liegt (Gummiband nicht zeichenbar). Plus Rand-Pfeil zur Richtung,
    /// damit der Nutzer sieht, wo der Bezugspunkt ist. Behebt: "Distanz/Höhe verschwindet,
    /// sobald der vorherige Punkt aus dem Bild wandert".</summary>
    private void DrawOffScreenLiveSegment(Canvas canvas, int width, int height)
    {
        if (_state.LiveSegmentHorizontalMeters is not { } horiz) return;
        var cx = _state.ReticleX > 0 ? _state.ReticleX : width / 2f;
        var cy = _state.ReticleY > 0 ? _state.ReticleY : height / 2f;

        var mainText = FormatMeters(horiz);
        string? sub = null;
        if (_state.LiveSegmentHeightDelta is { } dh)
        {
            var slope = horiz > 0.05f ? $"   {dh / horiz * 100f:+0.0;-0.0;0.0} %" : "";
            sub = $"ΔH {dh:+0.00;-0.00;0.00} m{slope}";
        }
        // Feste Pille unter dem Reticle (kein Gummiband-Mittelpunkt vorhanden).
        DrawValuePill(canvas, cx, cy + 72 * _density, mainText, sub);

        // Rand-Pfeil in Richtung des Off-Screen-Punkts (falls vor der Kamera, Richtung bekannt).
        if (_state.LiveSegmentOffScreenDirectionDeg is { } dirDeg)
        {
            var rad = dirDeg * MathF.PI / 180f;
            var margin = 72f * _density;
            var px = cx + MathF.Cos(rad) * (width / 2f - margin);
            var py = cy + MathF.Sin(rad) * (height / 2f - margin);
            canvas.Save();
            canvas.Translate(px, py);
            canvas.Rotate(dirDeg);
            canvas.DrawPath(_offScreenArrowPath, _offScreenArrowPaint);
            canvas.Restore();
        }
    }

    /// <summary>Abgerundete dunkle Pille mit zentriertem Hauptwert + optionaler Unterzeile.
    /// Halbtransparenter Hintergrund garantiert Lesbarkeit auch bei Sonne (statt nur Schatten).</summary>
    private void DrawValuePill(Canvas canvas, float centerX, float centerY, string main, string? sub)
    {
        var padH = 12f * _density;
        var padV = 8f * _density;
        var mainW = _segValuePaint.MeasureText(main);
        var subW = sub != null ? _segSubPaint.MeasureText(sub) : 0f;
        var w = MathF.Max(mainW, subW) + padH * 2f;
        var mainLineH = 19f * _density;
        var subLineH = sub != null ? 15f * _density : 0f;
        var h = padV * 2f + mainLineH + subLineH;

        var left = centerX - w / 2f;
        var top = centerY - h / 2f;
        _pillRect.Set(left, top, left + w, top + h);
        DrawPanel(canvas, _pillRect, RadiusPill, PanelTone.Neutral, raised: true);

        var mainBaseline = top + padV + mainLineH - 5f * _density;
        canvas.DrawText(main, centerX, mainBaseline, _segValuePaint);
        if (sub != null)
            canvas.DrawText(sub, centerX, mainBaseline + subLineH, _segSubPaint);
    }

    /// <summary>Meter-Formatierung: unter 1 m in cm (Vermesser denken bei Feindistanzen in cm),
    /// ab 1 m in Metern mit 2 Nachkommastellen.</summary>
    private static string FormatMeters(float meters)
        => MathF.Abs(meters) < 1f ? $"{meters * 100f:F0} cm" : $"{meters:F2} m";

    private void DrawReticle(Canvas canvas, int width, int height)
    {
        var cx = _state.ReticleX > 0 ? _state.ReticleX : width / 2f;
        var cy = _state.ReticleY > 0 ? _state.ReticleY : height / 2f;

        var color = _state.HitQuality switch
        {
            ArHitQuality.Plane => C.Good,                 // Smaragd — sicherer Plane-Hit
            ArHitQuality.Point => C.Medium,               // Bernstein — Feature-Point
            ArHitQuality.InstantPlacement => C.Medium,    // Bernstein — Instant-Schätzung
            _ => C.Neutral,                               // neutral-grau — kein Hit
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
                label += dh > 0 ? $"  +{dh:F2}m" : $"  -{MathF.Abs(dh):F2}m";
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
        _panelRect.Set(20 * _density, top, width - 20 * _density, top + bannerHeight);
        // Glas-Panel mit rotem Border + Status-Dot statt vollflächigem Rot — seriöser, blendet nicht.
        DrawPanel(canvas, _panelRect, RadiusPanel, PanelTone.Poor);
        DrawStatusDot(canvas, _panelRect.Left + 18 * _density, top + bannerHeight / 2f, C.Poor);

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
            DrawWarningBanner(canvas, width, top, bannerH, _state.ThermalWarning!, C.Medium);
            top += bannerH + gap;
        }

        if (!string.IsNullOrEmpty(_state.BatteryWarning))
        {
            DrawWarningBanner(canvas, width, top, bannerH, _state.BatteryWarning!, C.Medium);
        }
    }

    private void DrawWarningBanner(Canvas canvas, int width, float top, float height,
        string text, Color dotColor)
    {
        _panelRect.Set(20 * _density, top, width - 20 * _density, top + height);
        DrawPanel(canvas, _panelRect, RadiusPanel, PanelTone.Medium);
        DrawStatusDot(canvas, _panelRect.Left + 18 * _density, top + height / 2f, dotColor);
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
        DrawPanel(canvas, rect, RadiusPanel);

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
            // "Flächen" — deutsch, konsistent mit dem Readiness-Dialog ("Erkannte Flächen"),
            // verständlicher als "PLANES" für die AR-First-Laien-Zielgruppe.
            canvas.DrawText("FLÄCHEN", x, y, _statsLabelPaint);
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

        // Footer = primäre Mess-Summe → Accent-Border wenn Punkte gesetzt sind.
        DrawPanel(canvas, rect, RadiusPanel, pointCount > 0 ? PanelTone.Accent : PanelTone.Neutral);

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

        // Text: "BEREIT" (lokalisiert) oder die WICHTIGSTE fehlende Bedingung + Tap-Hinweis "(i)".
        // Die volle ReadinessIssues-Liste (·-getrennt) machte das Badge sonst fast bildschirmbreit
        // und überlappte Stats-Panel/Nordpfeil; die komplette Checkliste öffnet der Tap aufs Badge.
        string text;
        if (_state.IsReadyToMeasure)
        {
            text = $"{_state.Labels.Ready}  {_state.TrackingQualityScore}%";
        }
        else
        {
            var firstIssue = (_state.ReadinessIssues ?? "").Split('·')[0].Trim();
            text = string.IsNullOrEmpty(firstIssue) ? "Nicht bereit  (i)" : $"{firstIssue}  (i)";
        }

        var textWidth = _bannerTextPaint.MeasureText(text);
        var dotSpace = 18 * _density;
        var badgeW = textWidth + 2 * padding + dotSpace;

        // Status über Ton (Border) + Status-Dot links, NICHT über vollflächige Knallfarbe.
        var (tone, dotColor) = _state.IsReadyToMeasure
            ? (PanelTone.Good, C.Good)
            : _state.TrackingQualityScore >= 50
                ? (PanelTone.Medium, C.Medium)
                : (PanelTone.Poor, C.Poor);

        var rect = new RectF(badgeX, badgeY, badgeX + badgeW, badgeY + badgeH);
        DrawPanel(canvas, rect, RadiusPanel, tone);
        DrawStatusDot(canvas, rect.Left + 13 * _density, badgeY + badgeH / 2f, dotColor);
        canvas.DrawText(text, rect.Left + dotSpace + (textWidth + 2 * padding) / 2f,
            badgeY + badgeH / 2f + 5 * _density, _bannerTextPaint);

        // Bounds publizieren für Tap-Detection in der Activity. Etwas vergrößert (8dp) damit
        // dünne Finger leichter treffen.
        var tapPad = 8 * _density;
        ReadinessBadgeBounds.Set(
            rect.Left - tapPad, rect.Top - tapPad,
            rect.Right + tapPad, rect.Bottom + tapPad);
    }

    /// <summary>Permanenter Modus-Chip oben mittig: aktiver Erfassungs-Modus (Titel) + nächster
    /// Schritt bzw. Fortschritt (Detail). Design-konsistenter Glas-Chip mit Accent-Border +
    /// Status-Dot — ersetzt den früheren nativen Modus-Text und Punkt-Zähler.</summary>
    private void DrawModeChip(Canvas canvas, int width)
    {
        var title = _state.ModeChipTitle;
        if (string.IsNullOrEmpty(title)) return;
        var detail = _state.ModeChipDetail;
        var hasDetail = !string.IsNullOrEmpty(detail);

        var top = _state.TopInsetPixels + 12f * _density;
        var titleW = _modeChipTitlePaint.MeasureText(title);
        var detailW = hasDetail ? _modeChipDetailPaint.MeasureText(detail) : 0f;
        var dotSpace = 16f * _density;          // Akzent-Dot links + Abstand
        var padH = 16f * _density;
        var contentW = MathF.Max(titleW, detailW);
        var panelW = contentW + dotSpace + 2f * padH;
        var panelH = (hasDetail ? 46f : 36f) * _density;

        var cx = width / 2f;
        var left = cx - panelW / 2f;
        _panelRect.Set(left, top, left + panelW, top + panelH);
        DrawPanel(canvas, _panelRect, RadiusPill, PanelTone.Accent, raised: true);

        // Akzent-Dot links — signalisiert den aktiven Modus.
        DrawStatusDot(canvas, left + padH * 0.75f, top + panelH / 2f, C.Accent);

        // Texte zentriert (leichter Rechts-Offset, damit sie nicht in den Dot laufen).
        var textCx = cx + dotSpace * 0.25f;
        if (hasDetail)
        {
            canvas.DrawText(title, textCx, top + 20f * _density, _modeChipTitlePaint);
            canvas.DrawText(detail!, textCx, top + 37f * _density, _modeChipDetailPaint);
        }
        else
        {
            canvas.DrawText(title, textCx, top + panelH / 2f + 5f * _density, _modeChipTitlePaint);
        }
    }

    private void DrawTransientHint(Canvas canvas, int width, int height)
    {
        if (string.IsNullOrEmpty(_state.TransientHint)) return;
        if (Java.Lang.JavaSystem.CurrentTimeMillis() > _transientHintUntilMs) return;

        // Schweregrad → Panel-Ton + Status-Dot (Info neutral, Erfolg grün, Warnung bernstein).
        var (tone, dotColor) = _state.TransientHintSeverity switch
        {
            TransientSeverity.Success => (PanelTone.Good, C.Good),
            TransientSeverity.Warning => (PanelTone.Medium, C.Medium),
            _ => (PanelTone.Neutral, C.Neutral),
        };

        var textWidth = _transientHintTextPaint.MeasureText(_state.TransientHint);
        var dotSpace = 18 * _density;
        var bannerW = textWidth + 32 * _density + dotSpace;
        var bannerH = 32 * _density;
        var cx = width / 2f;
        var top = height - 140 * _density;
        var left = cx - bannerW / 2f;

        var rect = new RectF(left, top, left + bannerW, top + bannerH);
        DrawPanel(canvas, rect, RadiusPill, tone, raised: true);
        DrawStatusDot(canvas, left + 13 * _density, top + bannerH / 2f, dotColor);
        canvas.DrawText(_state.TransientHint, left + dotSpace + (textWidth + 32 * _density) / 2f,
            top + bannerH / 2f + 5 * _density, _transientHintTextPaint);

        // Nächster Redraw für Ablauf
        PostInvalidateDelayed(200);
    }
}
