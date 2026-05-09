using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Saisonale Event-Skin-Renderer (v2.0.42, Plan Task 3.4).
/// Ueberlagert das normale Spiel-Rendering mit Event-spezifischen Tints + Particle-Spawn.
/// 4 Events: Halloween (Pumpkin-Glow + Spuk-Partikel), Christmas (Schneeflocken),
/// NewYear (Feuerwerk-Sterne), Summer (tropische Bubbles).
///
/// Particles werden als gepoolte Struct-Liste verwaltet (max 80 aktiv) damit kein GC-Druck.
/// Render-Pfad wird in <see cref="GameRenderer.Render"/> nach atmosphaerischen Systemen,
/// vor HUD aufgerufen — damit das HUD nicht durch Event-Particles abgedunkelt wird.
/// </summary>
public partial class GameRenderer
{
    private const int EventParticleMax = 80;
    private struct EventParticle
    {
        public float X, Y, Vx, Vy;
        public float Life, MaxLife;
        public float Size;
        public byte ColorR, ColorG, ColorB;
        public float Rotation, RotationSpeed;
    }
    private readonly EventParticle[] _eventParticles = new EventParticle[EventParticleMax];
    private float _eventSpawnTimer;
    private readonly SKPaint _eventPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPath _eventShapePath = new();

    /// <summary>
    /// Aktualisiert + zeichnet Event-Partikel (alle 4 Events). Wird von GameRenderer.Render
    /// aufgerufen wenn HasActiveEvent == true und !SkipAtmosphere (sonst keine Partikel im Performance-Modus).
    /// </summary>
    private void RenderEventOverlay(SKCanvas canvas, float canvasWidth, float canvasHeight, float deltaTime)
    {
        if (!HasActiveEvent || SkipAtmosphere) return;

        // Spawn-Rate variiert pro Event-Typ
        float spawnIntervalSec = EventType switch
        {
            SeasonalEventType.Christmas => 0.08f,   // dichter Schneefall
            SeasonalEventType.NewYear => 0.15f,     // sporadische Feuerwerks-Sterne
            SeasonalEventType.Halloween => 0.2f,    // sparsamer Spuk
            SeasonalEventType.Summer => 0.18f,      // langsame tropische Bubbles
            _ => 0.2f,
        };
        _eventSpawnTimer += deltaTime;
        while (_eventSpawnTimer >= spawnIntervalSec)
        {
            _eventSpawnTimer -= spawnIntervalSec;
            SpawnEventParticle(canvasWidth, canvasHeight);
        }

        // Update + Render aller aktiven Particles
        for (int i = 0; i < EventParticleMax; i++)
        {
            ref var p = ref _eventParticles[i];
            if (p.Life <= 0) continue;
            p.Life -= deltaTime;
            p.X += p.Vx * deltaTime;
            p.Y += p.Vy * deltaTime;
            p.Rotation += p.RotationSpeed * deltaTime;

            // Off-Screen → killen (mit kleinem Puffer)
            if (p.Y > canvasHeight + 50 || p.X < -50 || p.X > canvasWidth + 50)
            {
                p.Life = 0;
                continue;
            }

            float lifeRatio = p.Life / p.MaxLife;
            byte alpha = (byte)Math.Clamp(lifeRatio * 255, 0, 255);
            _eventPaint.Color = new SKColor(p.ColorR, p.ColorG, p.ColorB, alpha);
            DrawEventShape(canvas, ref p);
        }
    }

    private void SpawnEventParticle(float w, float h)
    {
        // Freien Slot im Pool finden
        int slot = -1;
        for (int i = 0; i < EventParticleMax; i++)
        {
            if (_eventParticles[i].Life <= 0)
            {
                slot = i;
                break;
            }
        }
        if (slot < 0) return; // Pool voll → Spawn skippen

        ref var p = ref _eventParticles[slot];
        var rng = Random.Shared;

        switch (EventType)
        {
            case SeasonalEventType.Halloween:
                // Spuk-Partikel: violette + orange Funken steigen langsam von unten
                p.X = (float)(rng.NextDouble() * w);
                p.Y = h + 10;
                p.Vx = (float)((rng.NextDouble() - 0.5) * 30);
                p.Vy = -(float)(40 + rng.NextDouble() * 60);
                p.Size = 4 + (float)(rng.NextDouble() * 4);
                bool orange = rng.Next(2) == 0;
                p.ColorR = orange ? (byte)255 : (byte)138;
                p.ColorG = orange ? (byte)111 : (byte)43;
                p.ColorB = orange ? (byte)0 : (byte)226;
                p.MaxLife = 3.0f;
                p.Life = p.MaxLife;
                p.RotationSpeed = (float)((rng.NextDouble() - 0.5) * 4);
                break;

            case SeasonalEventType.Christmas:
                // Schneeflocken: weisse Hexagons fallen von oben
                p.X = (float)(rng.NextDouble() * w);
                p.Y = -10;
                p.Vx = (float)((rng.NextDouble() - 0.5) * 20);
                p.Vy = 60 + (float)(rng.NextDouble() * 40);
                p.Size = 3 + (float)(rng.NextDouble() * 5);
                p.ColorR = 255; p.ColorG = 255; p.ColorB = 255;
                p.MaxLife = 6.0f;
                p.Life = p.MaxLife;
                p.RotationSpeed = (float)((rng.NextDouble() - 0.5) * 2);
                break;

            case SeasonalEventType.NewYear:
                // Feuerwerk: bunte Sterne explodieren am oberen Bildrand
                p.X = (float)(rng.NextDouble() * w);
                p.Y = (float)(rng.NextDouble() * (h * 0.4));
                float speed = 80 + (float)(rng.NextDouble() * 60);
                double angle = rng.NextDouble() * Math.PI * 2;
                p.Vx = (float)(Math.Cos(angle) * speed);
                p.Vy = (float)(Math.Sin(angle) * speed);
                p.Size = 4 + (float)(rng.NextDouble() * 3);
                // Bunte Feuerwerks-Farben (rot/gelb/cyan/magenta)
                int hue = rng.Next(4);
                (p.ColorR, p.ColorG, p.ColorB) = hue switch
                {
                    0 => ((byte)255, (byte)80, (byte)80),
                    1 => ((byte)255, (byte)215, (byte)0),
                    2 => ((byte)0, (byte)200, (byte)255),
                    _ => ((byte)255, (byte)80, (byte)200),
                };
                p.MaxLife = 1.5f;
                p.Life = p.MaxLife;
                p.RotationSpeed = (float)(rng.NextDouble() * 6);
                break;

            case SeasonalEventType.Summer:
                // Tropische Bubbles: cyan-tuerkis Kreise steigen langsam auf
                p.X = (float)(rng.NextDouble() * w);
                p.Y = h + 10;
                p.Vx = (float)((rng.NextDouble() - 0.5) * 15);
                p.Vy = -(float)(30 + rng.NextDouble() * 30);
                p.Size = 6 + (float)(rng.NextDouble() * 6);
                p.ColorR = 64; p.ColorG = 224; p.ColorB = 208;
                p.MaxLife = 5.0f;
                p.Life = p.MaxLife;
                p.RotationSpeed = 0;
                break;
        }
    }

    private void DrawEventShape(SKCanvas canvas, ref EventParticle p)
    {
        switch (EventType)
        {
            case SeasonalEventType.Halloween:
                // Diamant-Funke (rotiert)
                canvas.Save();
                canvas.Translate(p.X, p.Y);
                canvas.RotateRadians(p.Rotation);
                _eventShapePath.Rewind();
                _eventShapePath.MoveTo(0, -p.Size);
                _eventShapePath.LineTo(p.Size * 0.6f, 0);
                _eventShapePath.LineTo(0, p.Size);
                _eventShapePath.LineTo(-p.Size * 0.6f, 0);
                _eventShapePath.Close();
                canvas.DrawPath(_eventShapePath, _eventPaint);
                canvas.Restore();
                break;

            case SeasonalEventType.Christmas:
                // Hexagonale Schneeflocke (rotiert)
                canvas.Save();
                canvas.Translate(p.X, p.Y);
                canvas.RotateRadians(p.Rotation);
                _eventShapePath.Rewind();
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3;
                    float ex = (float)(Math.Cos(a) * p.Size);
                    float ey = (float)(Math.Sin(a) * p.Size);
                    if (i == 0) _eventShapePath.MoveTo(ex, ey);
                    else _eventShapePath.LineTo(ex, ey);
                }
                _eventShapePath.Close();
                canvas.DrawPath(_eventShapePath, _eventPaint);
                canvas.Restore();
                break;

            case SeasonalEventType.NewYear:
                // 5-Zacken-Stern
                canvas.Save();
                canvas.Translate(p.X, p.Y);
                canvas.RotateRadians(p.Rotation);
                _eventShapePath.Rewind();
                for (int i = 0; i < 10; i++)
                {
                    double a = i * Math.PI / 5 - Math.PI / 2;
                    float r = (i % 2 == 0) ? p.Size : p.Size * 0.5f;
                    float ex = (float)(Math.Cos(a) * r);
                    float ey = (float)(Math.Sin(a) * r);
                    if (i == 0) _eventShapePath.MoveTo(ex, ey);
                    else _eventShapePath.LineTo(ex, ey);
                }
                _eventShapePath.Close();
                canvas.DrawPath(_eventShapePath, _eventPaint);
                canvas.Restore();
                break;

            case SeasonalEventType.Summer:
                // Bubble (offener Kreis mit Highlight)
                _eventPaint.Style = SKPaintStyle.Stroke;
                _eventPaint.StrokeWidth = 1.5f;
                canvas.DrawCircle(p.X, p.Y, p.Size, _eventPaint);
                _eventPaint.Style = SKPaintStyle.Fill;
                // Hellpunkt-Highlight
                var hl = _eventPaint.Color;
                _eventPaint.Color = new SKColor(255, 255, 255, (byte)(hl.Alpha * 0.6f));
                canvas.DrawCircle(p.X - p.Size * 0.3f, p.Y - p.Size * 0.3f, p.Size * 0.25f, _eventPaint);
                _eventPaint.Color = hl;
                break;
        }
    }

    /// <summary>
    /// Light Event-Tint-Overlay ueber das gesamte Spielfeld. Subtil (Alpha 25-40)
    /// damit Gameplay-Lesbarkeit erhalten bleibt — nur ein Hauch von Event-Stimmung.
    /// </summary>
    private void RenderEventTint(SKCanvas canvas, float w, float h)
    {
        if (!HasActiveEvent) return;

        byte tintAlpha = EventType switch
        {
            SeasonalEventType.Halloween => (byte)35,   // dunkle Halloween-Stimmung
            SeasonalEventType.Christmas => (byte)20,   // dezenter Eis-Hauch
            SeasonalEventType.NewYear => (byte)15,     // ganz subtil
            SeasonalEventType.Summer => (byte)18,
            _ => 0,
        };
        if (tintAlpha == 0) return;

        var tintColor = new SKColor(EventAccentColor.Red, EventAccentColor.Green, EventAccentColor.Blue, tintAlpha);
        _eventPaint.Color = tintColor;
        _eventPaint.Style = SKPaintStyle.Fill;
        _eventPaint.BlendMode = SKBlendMode.Multiply; // gemischter Tint, nicht Vollfarbe
        canvas.DrawRect(0, 0, w, h, _eventPaint);
        _eventPaint.BlendMode = SKBlendMode.SrcOver;
    }
}
