using Android.Graphics;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Design-System des AR-Overlays: semantische Farb-Tokens, Typo-Schnitte, Spacing/Radius-Skala
/// und der EINE Glas-Panel-Helfer, den alle HUD-Container nutzen. Ziel: ein kohärenter, seriöser
/// Pro-Instrument-Look (Leica/Trimble-Anmutung) statt fragmentiertem "Bastel-HUD".
/// </summary>
public sealed partial class ArPointOverlayView
{
    // ── Farb-Tokens (semantisch statt dekorativ) ──────────────────────────────────────────
    // Leitprinzip: Die Kamera ist die Bühne, das HUD ist das Instrument. EINE Träger-/Glasfarbe,
    // EINE Identitätsfarbe (Orange), EIN 3-stufiger Qualitäts-Kanal (Ampel). Leicht entsättigt,
    // damit nebeneinander ruhig + bei Sonne flimmerfrei.
    private static class C
    {
        // Träger / Glas
        public static readonly Color SurfaceBase = Color.Argb(214, 20, 24, 33);   // #141821 @84% — Graphit-Glas
        public static readonly Color SurfaceRaised = Color.Argb(232, 30, 36, 48); // schwebende Pillen über Panels
        public static readonly Color PanelBorder = Color.Argb(48, 255, 255, 255); // 1px Hairline-Kante
        public static readonly Color PanelBorderActive = Color.Argb(150, 255, 138, 51);
        public static readonly Color PanelShadow = Color.Argb(80, 0, 0, 0);       // dezenter gerichteter Schatten
        public static readonly Color Scrim = Color.Argb(150, 10, 12, 16);

        // Identität + Text
        public static readonly Color Accent = Color.Argb(255, 255, 122, 26);      // #FF7A1A — Identität (wärmer als #FF6B00)
        public static readonly Color AccentDim = Color.Argb(95, 255, 122, 26);
        public static readonly Color TextPrimary = Color.Argb(255, 245, 247, 250);   // Off-White (weniger HDR-Blooming)
        public static readonly Color TextSecondary = Color.Argb(205, 178, 190, 206); // Labels/Einheiten
        public static readonly Color TextMuted = Color.Argb(155, 138, 150, 168);     // Tertiär

        // Qualitäts-Ampel (Reticle, Confidence, RubberBand, Readiness, RTK, Stabilität — DIESELBEN Werte)
        public static readonly Color Good = Color.Argb(255, 38, 198, 122);    // #26C67A Smaragd — Plane/bereit/RTK-Fix
        public static readonly Color Medium = Color.Argb(255, 245, 176, 65);  // #F5B041 Bernstein — Instant/Float/Achtung
        public static readonly Color Poor = Color.Argb(255, 235, 77, 75);     // #EB4D4B — kein Hit/Fehler
        public static readonly Color Neutral = Color.Argb(220, 178, 190, 206);// kein Treffer (Idle)
    }

    // ── Typo: echte Schnitte statt FakeBoldText ───────────────────────────────────────────
    private static readonly Typeface FontRegular = Typeface.Create("sans-serif", TypefaceStyle.Normal)!;
    private static readonly Typeface FontMedium = Typeface.Create("sans-serif-medium", TypefaceStyle.Normal)!;

    // ── Radius- + Spacing-Skala (4dp-Raster), in dp; bei Nutzung × _density ───────────────
    private const float RadiusPill = 10f;
    private const float RadiusPanel = 12f;
    private const float RadiusLarge = 16f;

    // ── Panel-Paints (im Ctor via InitDesignPaints initialisiert) ─────────────────────────
    private Paint _panelFillPaint = null!;
    private Paint _panelBorderPaint = null!;
    private Paint _panelShadowPaint = null!;
    private readonly RectF _panelShadowRect = new();
    // Wiederverwendete RectF für allokationsfreie Panel-Aufrufe im Hot-Path.
    private readonly RectF _pillRect = new();
    private readonly RectF _panelRect = new();

    /// <summary>Panel-Ton steuert NUR die Border-/Akzentfarbe (Status-Codierung), nie die Geometrie.</summary>
    private enum PanelTone { Neutral, Accent, Good, Medium, Poor }

    private static Color WithAlpha(Color c, int alpha) => Color.Argb(alpha, c.R, c.G, c.B);

    // Rand-Pfeil zum Off-Screen-Referenzpunkt (Live-Distanz-Anzeige).
    private readonly global::Android.Graphics.Path _offScreenArrowPath = new();
    private Paint _offScreenArrowPaint = null!;

    private void InitDesignPaints()
    {
        _panelFillPaint = new Paint(PaintFlags.AntiAlias) { Color = C.SurfaceBase };
        _panelFillPaint.SetStyle(Paint.Style.Fill);

        _offScreenArrowPaint = new Paint(PaintFlags.AntiAlias) { Color = C.Accent };
        _offScreenArrowPaint.SetStyle(Paint.Style.Fill);
        var s = 11f * _density;
        _offScreenArrowPath.MoveTo(s, 0);             // Spitze nach +x (Pfeil zeigt entlang Rotationsachse)
        _offScreenArrowPath.LineTo(-s * 0.7f, s * 0.7f);
        _offScreenArrowPath.LineTo(-s * 0.7f, -s * 0.7f);
        _offScreenArrowPath.Close();

        _panelBorderPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = C.PanelBorder,
            StrokeWidth = 1f * _density,
        };
        _panelBorderPaint.SetStyle(Paint.Style.Stroke);

        _panelShadowPaint = new Paint(PaintFlags.AntiAlias) { Color = C.PanelShadow };
        _panelShadowPaint.SetStyle(Paint.Style.Fill);
    }

    /// <summary>
    /// Einheitliche Glas-Panel-Optik für ALLE HUD-Container: dezenter gerichteter Schatten +
    /// SurfaceBase/Raised-Fill + 1px Hairline-Border. Status wird über die BORDER-Farbe codiert
    /// (<paramref name="tone"/>) — NICHT mehr über vollflächig knallige Panels. Das ist der zentrale
    /// "seriös statt Bastel"-Hebel. Allokationsfrei (kein BlurMaskFilter wegen GPU-Layer der View).
    /// </summary>
    private void DrawPanel(Canvas canvas, RectF rect, float radiusDp, PanelTone tone = PanelTone.Neutral, bool raised = false)
    {
        var r = radiusDp * _density;

        // Dezenter gerichteter Schatten (HW-beschleunigungs-kompatibel, ohne BlurMaskFilter).
        var sh = 2f * _density;
        _panelShadowRect.Set(rect.Left, rect.Top + sh, rect.Right, rect.Bottom + sh);
        canvas.DrawRoundRect(_panelShadowRect, r, r, _panelShadowPaint);

        _panelFillPaint.Color = raised ? C.SurfaceRaised : C.SurfaceBase;
        canvas.DrawRoundRect(rect, r, r, _panelFillPaint);

        _panelBorderPaint.Color = tone switch
        {
            PanelTone.Accent => C.PanelBorderActive,
            PanelTone.Good => WithAlpha(C.Good, 160),
            PanelTone.Medium => WithAlpha(C.Medium, 160),
            PanelTone.Poor => WithAlpha(C.Poor, 160),
            _ => C.PanelBorder,
        };
        canvas.DrawRoundRect(rect, r, r, _panelBorderPaint);
    }

    /// <summary>Kleiner gefüllter Status-Dot (8dp) — codiert Status neben Text, statt Vollfläche.</summary>
    private void DrawStatusDot(Canvas canvas, float cx, float cy, Color color, float radiusDp = 4f)
    {
        _panelFillPaint.Color = color;
        canvas.DrawCircle(cx, cy, radiusDp * _density, _panelFillPaint);
    }
}
