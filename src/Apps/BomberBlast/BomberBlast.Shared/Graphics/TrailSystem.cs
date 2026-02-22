using BomberBlast.Models.Cosmetics;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using SkiaSharp;
using System;

namespace BomberBlast.Graphics;

/// <summary>
/// Charakter-Spuren: Ghost-Afterimages, Pontan-Feuer, Pass-Speedlines,
/// Spieler-Fußabdrücke, Boss-Eis/Lava-Spuren.
/// Struct-basierter Pool, max 40 Trail-Punkte.
/// </summary>
public sealed class TrailSystem : IDisposable
{
    private const int MAX_TRAILS = 40;

    private struct TrailPoint
    {
        public float X, Y;           // Pixel-Position (Welt-Koordinaten)
        public float Timer;          // Verbleibende Lebenszeit
        public float MaxTimer;       // Start-Lebenszeit (für Alpha-Berechnung)
        public TrailType Type;
        public float ExtraData;      // Richtung (für Speedlines), Größe (für Footprints)
        public byte R, G, B;         // Trail-Farbe
        public byte R2, G2, B2;      // Sekundärfarbe (für kosmetische Trails)
        public TrailStyle CosmeticStyle; // Kosmetischer Trail-Stil
        public bool Active;
    }

    private enum TrailType : byte
    {
        GhostAfterimage,  // Halbtransparente Geister-Kopie
        FireTrail,         // Pontan Feuer-Fleck
        SpeedLine,         // Pass Geschwindigkeits-Streifen
        Footprint,         // Spieler-Fußabdruck
        IceTrail,          // Boss IceDragon Eis-Spur
        LavaTrail,         // Boss FireDemon Lava-Spur
        CosmeticTrail      // Kosmetischer Spieler-Trail (aus Shop)
    }

    private readonly TrailPoint[] _trails = new TrailPoint[MAX_TRAILS];
    private int _nextTrail;

    // Spawn-Timer (verhindert zu viele Trails pro Frame)
    private float _playerTrailTimer;
    private float _ghostTrailTimer;
    private float _pontanTrailTimer;

    // Letzte bekannte Positionen (für Distanz-Check)
    private float _lastPlayerX, _lastPlayerY;

    // Gepoolte Paints
    private readonly SKPaint _trailPaint = new() { IsAntialias = true };
    private readonly SKMaskFilter _trailGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
    private bool _disposed;

    // ═══════════════════════════════════════════════════════════════════════
    // UPDATE - Trail-Punkte erstellen und altern
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Aktiver kosmetischer Trail (von ICustomizationService)</summary>
    public TrailDefinition? ActiveCosmeticTrail { get; set; }

    /// <summary>
    /// Trails aktualisieren: Neue Punkte erstellen, alte altern lassen.
    /// </summary>
    public void Update(float deltaTime, Player? player,
        IEnumerable<Enemy> enemies, float globalTimer)
    {
        // Timer altern
        for (int i = 0; i < MAX_TRAILS; i++)
        {
            if (!_trails[i].Active) continue;
            _trails[i].Timer -= deltaTime;
            if (_trails[i].Timer <= 0)
                _trails[i].Active = false;
        }

        // Spawn-Timer aktualisieren
        _playerTrailTimer -= deltaTime;
        _ghostTrailTimer -= deltaTime;
        _pontanTrailTimer -= deltaTime;

        // Spieler-Trails (kosmetisch oder Standard-Fußabdrücke)
        if (player != null && player.IsMoving && _playerTrailTimer <= 0)
        {
            float dx = player.X - _lastPlayerX;
            float dy = player.Y - _lastPlayerY;
            if (dx * dx + dy * dy > 16f) // Min. 4px Abstand
            {
                if (ActiveCosmeticTrail != null)
                {
                    // Kosmetischer Trail aus Shop
                    var ct = ActiveCosmeticTrail;
                    float duration = ct.Rarity >= Models.Rarity.Epic ? 1.2f : 0.8f;
                    float interval = ct.Rarity >= Models.Rarity.Epic ? 0.08f : 0.12f;
                    AddCosmeticTrail(player.X, player.Y, ct.Style, duration,
                        ct.PrimaryColor, ct.SecondaryColor);
                    _playerTrailTimer = interval;
                }
                else
                {
                    // Standard-Fußabdrücke
                    AddTrail(player.X, player.Y + GameGrid.CELL_SIZE * 0.2f,
                        TrailType.Footprint, 1.0f, 0,
                        80, 80, 80);
                    _playerTrailTimer = 0.25f;
                }
                _lastPlayerX = player.X;
                _lastPlayerY = player.Y;
            }
        }

        // Gegner-Trails
        foreach (var enemy in enemies)
        {
            if (!enemy.IsActive || enemy.IsDying || !enemy.IsMoving) continue;

            switch (enemy.Type)
            {
                case EnemyType.Ghost when _ghostTrailTimer <= 0:
                    // Ghost: Halbtransparente Afterimages
                    AddTrail(enemy.X, enemy.Y,
                        TrailType.GhostAfterimage, 0.6f, 0,
                        100, 140, 200);
                    _ghostTrailTimer = 0.12f;
                    break;

                case EnemyType.Pontan when _pontanTrailTimer <= 0:
                    // Pontan: Feuer-Flecken auf dem Boden
                    AddTrail(enemy.X, enemy.Y + GameGrid.CELL_SIZE * 0.15f,
                        TrailType.FireTrail, 0.5f, 0,
                        255, 140, 40);
                    _pontanTrailTimer = 0.15f;
                    break;

                case EnemyType.Pass:
                    // Pass: Speedlines (bestehende Logik im Renderer ergänzt)
                    // Keine zusätzlichen Trail-Punkte nötig - Pass hat bereits Speed-Lines
                    break;
            }

            // Boss-Trails
            if (enemy is BossEnemy boss)
            {
                switch (boss.BossKind)
                {
                    case BossType.IceDragon when boss.IsMoving:
                        AddTrail(boss.X, boss.Y + GameGrid.CELL_SIZE * 0.3f,
                            TrailType.IceTrail, 1.2f, GameGrid.CELL_SIZE * 0.4f,
                            150, 210, 255);
                        break;

                    case BossType.FireDemon when boss.IsMoving:
                        AddTrail(boss.X, boss.Y + GameGrid.CELL_SIZE * 0.3f,
                            TrailType.LavaTrail, 0.8f, GameGrid.CELL_SIZE * 0.35f,
                            255, 100, 30);
                        break;
                }
            }
        }
    }

    private void AddTrail(float x, float y, TrailType type, float duration,
        float extraData, byte r, byte g, byte b)
    {
        ref var t = ref _trails[_nextTrail];
        t.X = x;
        t.Y = y;
        t.Type = type;
        t.Timer = duration;
        t.MaxTimer = duration;
        t.ExtraData = extraData;
        t.R = r;
        t.G = g;
        t.B = b;
        t.Active = true;
        _nextTrail = (_nextTrail + 1) % MAX_TRAILS;
    }

    private void AddCosmeticTrail(float x, float y, TrailStyle style, float duration,
        SKColor primary, SKColor secondary)
    {
        ref var t = ref _trails[_nextTrail];
        t.X = x;
        t.Y = y;
        t.Type = TrailType.CosmeticTrail;
        t.CosmeticStyle = style;
        t.Timer = duration;
        t.MaxTimer = duration;
        t.ExtraData = 0;
        t.R = primary.Red;
        t.G = primary.Green;
        t.B = primary.Blue;
        t.R2 = secondary.Red;
        t.G2 = secondary.Green;
        t.B2 = secondary.Blue;
        t.Active = true;
        _nextTrail = (_nextTrail + 1) % MAX_TRAILS;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDER - Trail-Punkte zeichnen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Trails rendern. Aufruf im Game-World-Space (nach Grid, vor Entities).
    /// </summary>
    public void Render(SKCanvas canvas, float globalTimer)
    {
        for (int i = 0; i < MAX_TRAILS; i++)
        {
            if (!_trails[i].Active) continue;
            ref var t = ref _trails[i];
            float progress = t.Timer / t.MaxTimer; // 1→0 (1 = frisch, 0 = fast weg)

            switch (t.Type)
            {
                case TrailType.GhostAfterimage:
                    RenderGhostTrail(canvas, ref t, progress, globalTimer);
                    break;
                case TrailType.FireTrail:
                    RenderFireTrail(canvas, ref t, progress, globalTimer);
                    break;
                case TrailType.Footprint:
                    RenderFootprint(canvas, ref t, progress);
                    break;
                case TrailType.IceTrail:
                    RenderIceTrail(canvas, ref t, progress, globalTimer);
                    break;
                case TrailType.LavaTrail:
                    RenderLavaTrail(canvas, ref t, progress, globalTimer);
                    break;
                case TrailType.CosmeticTrail:
                    RenderCosmeticTrail(canvas, ref t, progress, globalTimer);
                    break;
            }
        }
    }

    // --- Ghost Afterimage ---
    private void RenderGhostTrail(SKCanvas canvas, ref TrailPoint t, float progress, float timer)
    {
        // Halbtransparente bläuliche Geister-Kopie (nur Oval + Augen-Andeutung)
        float size = GameGrid.CELL_SIZE * 0.35f;
        byte alpha = (byte)(progress * 60);

        _trailPaint.Style = SKPaintStyle.Fill;
        _trailPaint.MaskFilter = _trailGlow;
        _trailPaint.Color = new SKColor(t.R, t.G, t.B, alpha);
        canvas.DrawOval(t.X, t.Y, size, size * 1.1f, _trailPaint);

        // Leichte wellige Unterkante (Geister-Effekt)
        byte innerAlpha = (byte)(progress * 40);
        _trailPaint.Color = new SKColor(180, 210, 255, innerAlpha);
        _trailPaint.MaskFilter = null;
        canvas.DrawOval(t.X, t.Y - size * 0.15f, size * 0.6f, size * 0.7f, _trailPaint);
    }

    // --- Pontan Feuer-Spur ---
    private void RenderFireTrail(SKCanvas canvas, ref TrailPoint t, float progress, float timer)
    {
        // Glühender Feuer-Fleck auf dem Boden
        float baseR = 5f + progress * 4f;
        byte alpha = (byte)(progress * 100);

        // Äußerer Glow
        _trailPaint.Style = SKPaintStyle.Fill;
        _trailPaint.MaskFilter = _trailGlow;
        _trailPaint.Color = new SKColor(t.R, t.G, t.B, (byte)(alpha * 0.5f));
        canvas.DrawCircle(t.X, t.Y, baseR * 1.8f, _trailPaint);

        // Innerer heller Kern
        _trailPaint.MaskFilter = null;
        _trailPaint.Color = new SKColor(255, 200, 80, alpha);
        canvas.DrawCircle(t.X, t.Y, baseR, _trailPaint);

        // Kleines aufsteigendes Rauchpünktchen
        float riseY = (1f - progress) * 8f;
        byte smokeAlpha = (byte)(progress * 30);
        _trailPaint.Color = new SKColor(120, 120, 120, smokeAlpha);
        canvas.DrawCircle(t.X + MathF.Sin(timer * 3f + t.X) * 2f,
            t.Y - riseY - 5f, 2f, _trailPaint);
    }

    // --- Spieler-Fußabdruck ---
    private void RenderFootprint(SKCanvas canvas, ref TrailPoint t, float progress)
    {
        // Subtiler halbtransparenter Abdruck
        byte alpha = (byte)(progress * 25);
        float size = 3f;

        _trailPaint.Style = SKPaintStyle.Fill;
        _trailPaint.MaskFilter = null;
        _trailPaint.Color = new SKColor(t.R, t.G, t.B, alpha);

        // Zwei kleine Ovale (linker + rechter Fuß)
        canvas.DrawOval(t.X - 2f, t.Y, size, size * 1.3f, _trailPaint);
        canvas.DrawOval(t.X + 2f, t.Y, size, size * 1.3f, _trailPaint);
    }

    // --- Boss IceDragon Eis-Spur ---
    private void RenderIceTrail(SKCanvas canvas, ref TrailPoint t, float progress, float timer)
    {
        float size = t.ExtraData; // Größe aus ExtraData
        byte alpha = (byte)(progress * 60);

        // Eis-Kristall-Fleck
        _trailPaint.Style = SKPaintStyle.Fill;
        _trailPaint.MaskFilter = _trailGlow;
        _trailPaint.Color = new SKColor(t.R, t.G, t.B, (byte)(alpha * 0.4f));
        canvas.DrawOval(t.X, t.Y, size * 1.5f, size, _trailPaint);

        _trailPaint.MaskFilter = null;
        _trailPaint.Color = new SKColor(200, 230, 255, alpha);
        canvas.DrawOval(t.X, t.Y, size, size * 0.7f, _trailPaint);

        // Kristall-Linien
        byte lineAlpha = (byte)(progress * 40);
        _trailPaint.Style = SKPaintStyle.Stroke;
        _trailPaint.StrokeWidth = 1f;
        _trailPaint.Color = new SKColor(220, 240, 255, lineAlpha);
        float angle = timer * 0.5f + t.X * 0.1f;
        for (int j = 0; j < 3; j++)
        {
            float a = angle + j * 2.1f;
            float len = size * 0.6f;
            canvas.DrawLine(t.X, t.Y,
                t.X + MathF.Cos(a) * len,
                t.Y + MathF.Sin(a) * len, _trailPaint);
        }
    }

    // --- Boss FireDemon Lava-Spur ---
    private void RenderLavaTrail(SKCanvas canvas, ref TrailPoint t, float progress, float timer)
    {
        float size = t.ExtraData;
        byte alpha = (byte)(progress * 80);

        // Glühender Lava-Fleck
        _trailPaint.Style = SKPaintStyle.Fill;
        _trailPaint.MaskFilter = _trailGlow;
        _trailPaint.Color = new SKColor(255, 80, 20, (byte)(alpha * 0.6f));
        canvas.DrawOval(t.X, t.Y, size * 1.3f, size, _trailPaint);

        // Innerer oranger Kern
        _trailPaint.MaskFilter = null;
        _trailPaint.Color = new SKColor(255, 160, 40, alpha);
        float pulse = 1f + MathF.Sin(timer * 4f + t.X) * 0.15f;
        canvas.DrawOval(t.X, t.Y, size * 0.8f * pulse, size * 0.6f * pulse, _trailPaint);

        // Heller Kern-Punkt
        byte coreAlpha = (byte)(progress * 50);
        _trailPaint.Color = new SKColor(255, 230, 150, coreAlpha);
        canvas.DrawCircle(t.X, t.Y, size * 0.25f, _trailPaint);
    }

    // --- Kosmetischer Spieler-Trail ---
    private void RenderCosmeticTrail(SKCanvas canvas, ref TrailPoint t, float progress, float timer)
    {
        byte alpha = (byte)(progress * 120);
        float size = GameGrid.CELL_SIZE * 0.2f;

        switch (t.CosmeticStyle)
        {
            case TrailStyle.Dust:
            case TrailStyle.Smoke:
                // Aufsteigende Rauch-/Staubwolke
                float riseY = (1f - progress) * 6f;
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = _trailGlow;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, (byte)(alpha * 0.6f));
                canvas.DrawCircle(t.X + MathF.Sin(timer * 2f + t.X) * 2f,
                    t.Y - riseY, size * (0.8f + (1f - progress) * 0.4f), _trailPaint);
                _trailPaint.MaskFilter = null;
                break;

            case TrailStyle.Footsteps:
                RenderFootprint(canvas, ref t, progress);
                break;

            case TrailStyle.Sparkle:
            case TrailStyle.Stardust:
                // Funkelnde Partikel mit Pulsation
                float sparkle = MathF.Sin(timer * 8f + t.X * 3f + t.Y * 2f);
                byte sparkAlpha = (byte)(alpha * (0.6f + sparkle * 0.4f));
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = _trailGlow;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, sparkAlpha);
                canvas.DrawCircle(t.X, t.Y - (1f - progress) * 4f, size * 0.5f * (1f + sparkle * 0.3f), _trailPaint);
                // Sekundärer kleinerer Funke
                _trailPaint.Color = new SKColor(t.R2, t.G2, t.B2, (byte)(sparkAlpha * 0.5f));
                canvas.DrawCircle(t.X + MathF.Cos(timer * 5f) * 3f,
                    t.Y - (1f - progress) * 6f, size * 0.25f, _trailPaint);
                _trailPaint.MaskFilter = null;
                break;

            case TrailStyle.Flame:
            case TrailStyle.Phoenix:
                // Flammen-Partikel die aufsteigen
                float flameRise = (1f - progress) * 10f;
                float flicker = MathF.Sin(timer * 12f + t.X) * 0.3f + 0.7f;
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = _trailGlow;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, (byte)(alpha * flicker));
                canvas.DrawCircle(t.X + MathF.Sin(timer * 4f + t.Y) * 2f,
                    t.Y - flameRise, size * progress, _trailPaint);
                // Innerer Kern (heller)
                _trailPaint.Color = new SKColor(t.R2, t.G2, t.B2, (byte)(alpha * 0.5f * flicker));
                canvas.DrawCircle(t.X, t.Y - flameRise * 0.7f, size * 0.4f * progress, _trailPaint);
                _trailPaint.MaskFilter = null;
                break;

            case TrailStyle.Frost:
            case TrailStyle.Crystal:
                // Eis-Kristalle die kurz liegen bleiben
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = _trailGlow;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, (byte)(alpha * 0.5f));
                canvas.DrawCircle(t.X, t.Y, size * 0.8f, _trailPaint);
                // Kristall-Linien
                _trailPaint.MaskFilter = null;
                _trailPaint.Style = SKPaintStyle.Stroke;
                _trailPaint.StrokeWidth = 0.8f;
                _trailPaint.Color = new SKColor(t.R2, t.G2, t.B2, (byte)(alpha * 0.7f));
                for (int j = 0; j < 3; j++)
                {
                    float a = j * 2.1f + t.X * 0.1f;
                    float len = size * 0.5f;
                    canvas.DrawLine(t.X, t.Y,
                        t.X + MathF.Cos(a) * len,
                        t.Y + MathF.Sin(a) * len, _trailPaint);
                }
                break;

            case TrailStyle.Electric:
                // Zickzack-Blitze
                _trailPaint.Style = SKPaintStyle.Stroke;
                _trailPaint.StrokeWidth = 1.5f;
                _trailPaint.MaskFilter = _trailGlow;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, alpha);
                float ex = t.X + MathF.Sin(timer * 15f + t.Y * 2f) * 4f;
                float ey = t.Y + MathF.Cos(timer * 12f + t.X) * 3f;
                canvas.DrawLine(t.X, t.Y, ex, ey, _trailPaint);
                _trailPaint.Color = new SKColor(t.R2, t.G2, t.B2, (byte)(alpha * 0.8f));
                _trailPaint.StrokeWidth = 0.8f;
                canvas.DrawLine(ex, ey, ex + MathF.Sin(timer * 20f) * 3f,
                    ey + MathF.Cos(timer * 18f) * 3f, _trailPaint);
                _trailPaint.MaskFilter = null;
                break;

            case TrailStyle.Leaves:
            case TrailStyle.Bubbles:
                // Aufsteigende Elemente mit sanftem Schwanken
                float sway = MathF.Sin(timer * 3f + t.X + t.Y) * 4f;
                float rise = (1f - progress) * 8f;
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = null;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, alpha);
                if (t.CosmeticStyle == TrailStyle.Bubbles)
                {
                    // Blasen: Kreis mit hellem Glanzpunkt
                    canvas.DrawCircle(t.X + sway, t.Y - rise, size * 0.5f * progress, _trailPaint);
                    _trailPaint.Color = new SKColor(255, 255, 255, (byte)(alpha * 0.3f));
                    canvas.DrawCircle(t.X + sway - 1f, t.Y - rise - 1f, size * 0.15f, _trailPaint);
                }
                else
                {
                    // Blätter: Ovale mit Rotation
                    float rot = timer * 2f + t.X;
                    canvas.Save();
                    canvas.Translate(t.X + sway, t.Y - rise);
                    canvas.RotateDegrees(rot * 60f);
                    canvas.DrawOval(0, 0, size * 0.4f, size * 0.2f, _trailPaint);
                    canvas.Restore();
                }
                break;

            case TrailStyle.Plasma:
            case TrailStyle.Rainbow:
                // Leuchtende Kugeln mit Glow
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = _trailGlow;
                byte r = t.R, g = t.G, b = t.B;
                if (t.CosmeticStyle == TrailStyle.Rainbow)
                {
                    // Regenbogen: Farbe rotiert
                    float hue = (timer * 2f + t.X * 0.05f) % 6f;
                    (r, g, b) = HueToRgb(hue);
                }
                _trailPaint.Color = new SKColor(r, g, b, (byte)(alpha * 0.8f));
                float pulse = 1f + MathF.Sin(timer * 6f + t.X) * 0.2f;
                canvas.DrawCircle(t.X, t.Y, size * 0.6f * pulse * progress, _trailPaint);
                _trailPaint.Color = new SKColor(t.R2, t.G2, t.B2, (byte)(alpha * 0.3f));
                canvas.DrawCircle(t.X, t.Y, size * 1.0f * pulse * progress, _trailPaint);
                _trailPaint.MaskFilter = null;
                break;

            case TrailStyle.Shadow:
            case TrailStyle.Void:
                // Dunkle Schatten-Wisps
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = _trailGlow;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, (byte)(alpha * 0.7f));
                float wispX = MathF.Sin(timer * 4f + t.Y) * 3f;
                float wispY = (1f - progress) * 5f;
                canvas.DrawOval(t.X + wispX, t.Y - wispY,
                    size * 0.6f * progress, size * 0.3f * progress, _trailPaint);
                // Violetter Glow (für Void)
                if (t.CosmeticStyle == TrailStyle.Void)
                {
                    _trailPaint.Color = new SKColor(t.R2, t.G2, t.B2, (byte)(alpha * 0.4f));
                    canvas.DrawCircle(t.X + wispX, t.Y - wispY, size * 0.9f * progress, _trailPaint);
                }
                _trailPaint.MaskFilter = null;
                break;

            default: // GoldenPath und Fallback
                // Goldener leuchtender Pfad
                _trailPaint.Style = SKPaintStyle.Fill;
                _trailPaint.MaskFilter = _trailGlow;
                float shimmer = MathF.Sin(timer * 6f + t.X * 0.2f) * 0.3f + 0.7f;
                _trailPaint.Color = new SKColor(t.R, t.G, t.B, (byte)(alpha * shimmer));
                canvas.DrawOval(t.X, t.Y + GameGrid.CELL_SIZE * 0.15f,
                    size * 0.8f, size * 0.3f, _trailPaint);
                _trailPaint.Color = new SKColor(t.R2, t.G2, t.B2, (byte)(alpha * 0.3f * shimmer));
                canvas.DrawOval(t.X, t.Y + GameGrid.CELL_SIZE * 0.15f,
                    size * 1.2f, size * 0.5f, _trailPaint);
                _trailPaint.MaskFilter = null;
                break;
        }
    }

    /// <summary>Hue (0-6) zu RGB konvertieren (für Regenbogen-Trail)</summary>
    private static (byte R, byte G, byte B) HueToRgb(float hue)
    {
        float x = 1f - MathF.Abs(hue % 2f - 1f);
        float r, g, b;
        if (hue < 1f) { r = 1f; g = x; b = 0f; }
        else if (hue < 2f) { r = x; g = 1f; b = 0f; }
        else if (hue < 3f) { r = 0f; g = 1f; b = x; }
        else if (hue < 4f) { r = 0f; g = x; b = 1f; }
        else if (hue < 5f) { r = x; g = 0f; b = 1f; }
        else { r = 1f; g = 0f; b = x; }
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _trailPaint.Dispose();
        _trailGlow.Dispose();
    }
}
