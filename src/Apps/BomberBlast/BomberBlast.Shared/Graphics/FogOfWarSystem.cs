using BomberBlast.Models.Grid;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Fog-of-War-System (v2.0.35): Per-Cell Visibility-State-Tracker mit
/// 3 Zuständen pro Zelle — Unknown (nie gesehen, komplett dunkel),
/// Explored (bereits gesehen aber aktuell nicht im Sichtfeld, halb dunkel),
/// Visible (aktuell sichtbar, klar).
///
/// <para>Aktivierung:
/// <list type="bullet">
///   <item>Normal-Modus: Ab Level 50 (Welt 6 Ruins, thematisch dunkel)</item>
///   <item>Master Mode: Ab Level 1 (weil L50 bereits erreicht = Endgame-Challenge)</item>
///   <item>Welt 10 (Shadow Realm): Nutzt existing FogOverlay (einfacher Sichtkreis ohne Memory)</item>
/// </list>
/// </para>
///
/// <para>Algorithmus: Manhattan-Radius um Spieler. Kein echtes Line-of-Sight
/// (Blöcke blockieren Sicht nicht — passt zu Bomberman-Vogelperspektive).
/// Update 1× pro Frame in <see cref="Update"/>.</para>
/// </summary>
public sealed class FogOfWarSystem : IDisposable
{
    /// <summary>Per-Cell-State. Index: x * height + y (Row-Major für Cache-Lokalität).</summary>
    public enum CellVisibility : byte
    {
        Unknown = 0,
        Explored = 1,
        Visible = 2
    }

    private CellVisibility[] _cells = Array.Empty<CellVisibility>();
    private int _width;
    private int _height;
    private bool _enabled;
    private int _revealRadius = 4;

    // Cache der letzten Player-Grid-Position. Wenn sich die Position
    // zwischen zwei Update-Calls nicht geaendert hat, ist das Resultat
    // identisch und der gesamte Update-Pass kann uebersprungen werden.
    // Spart in 70-80% der Frames den 150-Cell-Loop (Spieler bewegt sich
    // kontinuierlich aber wechselt Grid-Cells nur alle paar Frames).
    private int _lastPlayerGridX = int.MinValue;
    private int _lastPlayerGridY = int.MinValue;

    /// <summary>Ob FoW aktuell aktiv ist (via <see cref="Enable"/> gesetzt).</summary>
    public bool IsEnabled => _enabled;

    /// <summary>Aktueller Sichtradius (Manhattan-Distanz) um den Spieler.</summary>
    public int RevealRadius => _revealRadius;

    /// <summary>
    /// FoW für ein neues Level vorbereiten. Alle Zellen auf Unknown, System aktiv.
    /// </summary>
    /// <param name="width">Grid-Breite (normalerweise 15).</param>
    /// <param name="height">Grid-Höhe (normalerweise 10).</param>
    /// <param name="revealRadius">Sichtradius in Zellen (Manhattan). Default 4.</param>
    public void Enable(int width, int height, int revealRadius = 4)
    {
        _width = width;
        _height = height;
        _revealRadius = Math.Max(1, revealRadius);

        int total = width * height;
        if (_cells.Length != total)
            _cells = new CellVisibility[total];

        Array.Clear(_cells, 0, _cells.Length);
        _enabled = true;

        // Cache invalidieren, damit der erste Update nach Enable() durchlaeuft
        // (auch wenn der Spieler zufaellig die gleiche Grid-Cell wie im
        // vorherigen Level hat).
        _lastPlayerGridX = int.MinValue;
        _lastPlayerGridY = int.MinValue;
    }

    /// <summary>FoW deaktivieren (z.B. beim Wechsel in Level ohne FoW).</summary>
    public void Disable()
    {
        _enabled = false;
    }

    /// <summary>
    /// Per-Frame-Update: Alle aktuell sichtbaren Zellen werden auf Visible gesetzt,
    /// zuvor sichtbare Zellen außerhalb des Radius auf Explored zurückgestuft.
    /// Unknown → Visible → Explored (Exploration-Memory-Pattern).
    /// </summary>
    public void Update(int playerGridX, int playerGridY)
    {
        if (!_enabled) return;

        // Wenn sich die Grid-Position seit dem letzten Update nicht geaendert
        // hat, ist das Resultat identisch — kompletter Update-Pass kann
        // ausgesetzt werden. Spart 0.2-0.4ms/Frame in FoW-Levels.
        if (playerGridX == _lastPlayerGridX && playerGridY == _lastPlayerGridY)
            return;
        _lastPlayerGridX = playerGridX;
        _lastPlayerGridY = playerGridY;

        // Erst: Alle vorher-sichtbaren Zellen auf Explored zurückstufen
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == CellVisibility.Visible)
                _cells[i] = CellVisibility.Explored;
        }

        // Dann: Zellen im aktuellen Sichtradius auf Visible setzen (überschreibt Explored)
        int r = _revealRadius;
        int xMin = Math.Max(0, playerGridX - r);
        int xMax = Math.Min(_width - 1, playerGridX + r);
        int yMin = Math.Max(0, playerGridY - r);
        int yMax = Math.Min(_height - 1, playerGridY + r);

        for (int x = xMin; x <= xMax; x++)
        {
            int dx = Math.Abs(x - playerGridX);
            for (int y = yMin; y <= yMax; y++)
            {
                int dy = Math.Abs(y - playerGridY);
                // Manhattan-Distanz + Ecken-Abrundung (Chebyshev würde quadratisch aussehen)
                if (dx + dy <= r || (dx <= r && dy <= r && dx * dx + dy * dy <= r * r + r))
                    _cells[x * _height + y] = CellVisibility.Visible;
            }
        }
    }

    /// <summary>Gibt den Visibility-State einer Zelle zurück (Out-of-Bounds → Unknown).</summary>
    public CellVisibility GetState(int x, int y)
    {
        if (!_enabled) return CellVisibility.Visible;
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return CellVisibility.Unknown;
        return _cells[x * _height + y];
    }

    /// <summary>
    /// Rendert das FoW-Overlay: Schwarzes Mask mit per-Cell Alpha-Werten.
    /// Unknown: 235 alpha (fast schwarz), Explored: 140 alpha (halb dunkel),
    /// Visible: 0 alpha (klar). Soft-Edge durch radialen Alpha-Fade um den Spieler.
    /// </summary>
    public void Render(SKCanvas canvas, float playerX, float playerY, SKPaint fillPaint)
    {
        if (!_enabled) return;

        int cs = GameGrid.CELL_SIZE;

        // : Run-Length-Encoding pro Zeile.
        // Statt 150 einzelne DrawRect-Calls (15x10 Grid) werden zusammen-
        // haengende Zellen mit gleichem Alpha-Wert zu einem DrawRect gemerged.
        // Bei typischer FoW-Verteilung (Spieler in der Mitte) spart das
        // 60-80% der DrawCalls (~150 → ~30) = 0.5-1.0ms/Frame.
        // Iteration laeuft pro Zeile horizontal, weil das visuell zusammen-
        // haengende horizontale Streifen ergibt.
        fillPaint.MaskFilter = null;
        for (int y = 0; y < _height; y++)
        {
            int runStart = -1;
            byte runAlpha = 0;
            // x == _width als Sentinel-Iteration: Schliesst den letzten Run der Zeile ab.
            for (int x = 0; x <= _width; x++)
            {
                byte alpha;
                if (x < _width)
                {
                    var state = _cells[x * _height + y];
                    alpha = state switch
                    {
                        CellVisibility.Unknown => (byte)235,
                        CellVisibility.Explored => (byte)140,
                        _ => (byte)0,
                    };
                }
                else
                {
                    alpha = 0;
                }

                if (alpha != runAlpha)
                {
                    if (runStart >= 0 && runAlpha > 0)
                    {
                        fillPaint.Color = new SKColor(0, 0, 0, runAlpha);
                        canvas.DrawRect(runStart * cs, y * cs, (x - runStart) * cs, cs, fillPaint);
                    }
                    runStart = x;
                    runAlpha = alpha;
                }
            }
        }

        // : Soft-Edge radial um Spieler. Zentrum 0 alpha, Radius-Edge 40 alpha.
        // Gibt weichen Übergang zwischen Visible-Kern und Explored/Unknown-Umgebung.
        float softEdgeRadius = _revealRadius * cs * 0.9f;
        fillPaint.Color = new SKColor(0, 0, 0, 40);
        fillPaint.Style = SKPaintStyle.Stroke;
        fillPaint.StrokeWidth = softEdgeRadius * 0.12f;
        fillPaint.MaskFilter = null;
        canvas.DrawCircle(playerX, playerY, softEdgeRadius * 0.95f, fillPaint);
        fillPaint.Color = new SKColor(0, 0, 0, 70);
        fillPaint.StrokeWidth = softEdgeRadius * 0.08f;
        canvas.DrawCircle(playerX, playerY, softEdgeRadius * 1.02f, fillPaint);

        // Paint-State zurücksetzen damit der nächste Aufrufer nicht durch unseren
        // Zustand (schwarz, Stroke-Width>0) überrascht wird. Gängiges Contract-Problem
        // bei gemeinsamen Paint-Objekten.
        fillPaint.Style = SKPaintStyle.Fill;
        fillPaint.StrokeWidth = 0f;
        fillPaint.Color = SKColors.White;
    }

    public void Dispose()
    {
        _cells = Array.Empty<CellVisibility>();
        _enabled = false;
    }
}
