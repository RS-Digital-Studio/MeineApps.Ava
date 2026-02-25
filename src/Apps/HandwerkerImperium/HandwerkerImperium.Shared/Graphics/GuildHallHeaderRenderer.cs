using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert eine mittelalterliche Gildenhalle als animierten Header über dem Forschungsbaum.
/// Steinmauer-Hintergrund, 2 Fackeln mit Flammen-Animation, großes Gilden-Wappen in der Mitte,
/// warmer Fackelschein. Alle SKPaint-Objekte gecacht für GC-freie Performance.
/// </summary>
public class GuildHallHeaderRenderer
{
    private float _time;

    // Flammen-Partikel
    private readonly List<FlameParticle> _leftFlameParticles = [];
    private readonly List<FlameParticle> _rightFlameParticles = [];
    private float _flameTimer;

    // ═══════════════════════════════════════════════════════════════════════
    // FARBEN
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKColor StoneBase = new(0x4A, 0x3C, 0x2E);
    private static readonly SKColor StoneDark = new(0x38, 0x2C, 0x20);
    private static readonly SKColor StoneLight = new(0x5A, 0x4C, 0x3E);
    private static readonly SKColor StoneGap = new(0x28, 0x1E, 0x14);
    private static readonly SKColor TorchWood = new(0x5D, 0x40, 0x37);
    private static readonly SKColor TorchBracket = new(0x78, 0x90, 0x9C);
    private static readonly SKColor FlameCore = new(0xFF, 0xD5, 0x4F);
    private static readonly SKColor FlameMid = new(0xFF, 0x8C, 0x00);
    private static readonly SKColor FlameOuter = new(0xFF, 0x57, 0x22);
    private static readonly SKColor EmberColor = new(0xFF, 0x6F, 0x00);
    private static readonly SKColor ShieldBorder = new(0xD4, 0xA3, 0x73);
    private static readonly SKColor ShieldFill = new(0x92, 0x40, 0x0E);
    private static readonly SKColor CraftGold = new(0xFF, 0xD7, 0x00);

    // ═══════════════════════════════════════════════════════════════════════
    // GECACHTE PAINTS
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint _stonePaint = new() { IsAntialias = false, Color = StoneBase };
    private static readonly SKPaint _stoneDarkPaint = new() { IsAntialias = false, Color = StoneDark };
    private static readonly SKPaint _stoneLightPaint = new() { IsAntialias = false, Color = StoneLight };
    private static readonly SKPaint _stoneGapPaint = new() { IsAntialias = false, Color = StoneGap, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _torchWoodPaint = new() { IsAntialias = true, Color = TorchWood };
    private static readonly SKPaint _torchBracketPaint = new() { IsAntialias = true, Color = TorchBracket };

    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Vignette
    private SKShader? _vignetteShader;
    private float _lastW, _lastH;

    // ═══════════════════════════════════════════════════════════════════════
    // HAUPT-RENDER
    // ═══════════════════════════════════════════════════════════════════════

    public void Render(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        _time += deltaTime;

        float w = bounds.Width;
        float h = bounds.Height;

        // 1. Steinmauer
        DrawStoneWall(canvas, bounds);

        // 2. Fackelschein (warmes Licht)
        float torchX1 = w * 0.18f;
        float torchX2 = w * 0.82f;
        float torchY = h * 0.35f;
        DrawTorchGlow(canvas, torchX1, torchY, w, h);
        DrawTorchGlow(canvas, torchX2, torchY, w, h);

        // 3. Gilden-Wappen in der Mitte
        DrawGuildEmblem(canvas, w / 2, h * 0.52f, Math.Min(w, h) * 0.42f);

        // 4. Fackeln (über dem Schein)
        DrawTorch(canvas, torchX1, torchY, h);
        DrawTorch(canvas, torchX2, torchY, h);

        // 5. Flammen
        UpdateAndDrawFlames(canvas, torchX1, torchY - 8, deltaTime, _leftFlameParticles);
        UpdateAndDrawFlames(canvas, torchX2, torchY - 8, deltaTime, _rightFlameParticles);

        // 6. Vignette (dunkle Ränder)
        DrawVignette(canvas, w, h);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STEINMAUER
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawStoneWall(SKCanvas canvas, SKRect bounds)
    {
        // Basis-Hintergrund
        _stonePaint.Color = StoneBase;
        canvas.DrawRect(bounds, _stonePaint);

        float w = bounds.Width;
        float h = bounds.Height;

        // Steinreihen (abwechselnd versetzt)
        float stoneH = 18;
        float stoneW = 42;
        int rows = (int)(h / stoneH) + 1;

        for (int row = 0; row < rows; row++)
        {
            float y = row * stoneH;
            float offset = (row % 2 == 0) ? 0 : stoneW * 0.5f;
            int cols = (int)(w / stoneW) + 2;

            for (int col = 0; col < cols; col++)
            {
                float x = col * stoneW + offset - stoneW * 0.5f;

                // Leichte Farbvariation pro Stein (deterministisch)
                int hash = row * 17 + col * 31;
                var stoneColor = (hash % 3) switch
                {
                    0 => StoneBase,
                    1 => StoneDark,
                    _ => StoneLight
                };

                _stonePaint.Color = stoneColor;
                var stoneRect = new SKRect(x + 1, y + 1, x + stoneW - 1, y + stoneH - 1);
                canvas.DrawRect(stoneRect, _stonePaint);
            }

            // Horizontale Fugen
            canvas.DrawLine(0, y, w, y, _stoneGapPaint);
        }

        // Vertikale Fugen (versetzt)
        for (int row = 0; row < rows; row++)
        {
            float y = row * stoneH;
            float offset = (row % 2 == 0) ? 0 : stoneW * 0.5f;
            int cols = (int)(w / stoneW) + 2;

            for (int col = 0; col < cols; col++)
            {
                float x = col * stoneW + offset;
                canvas.DrawLine(x, y, x, y + stoneH, _stoneGapPaint);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FACKEL
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawTorch(SKCanvas canvas, float cx, float cy, float headerH)
    {
        // Halterung (Metall-Winkel an der Wand)
        _torchBracketPaint.StrokeWidth = 3;
        _torchBracketPaint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(cx, cy + 22, cx, cy + 40, _torchBracketPaint);
        canvas.DrawLine(cx - 6, cy + 40, cx + 6, cy + 40, _torchBracketPaint);
        _torchBracketPaint.Style = SKPaintStyle.Fill;

        // Holzstiel
        _torchWoodPaint.Color = TorchWood;
        canvas.DrawRect(cx - 3, cy - 4, 6, 26, _torchWoodPaint);

        // Dunkle Maserung
        _torchWoodPaint.Color = new SKColor(0x4E, 0x34, 0x2E);
        canvas.DrawRect(cx - 1, cy, 2, 22, _torchWoodPaint);

        // Fackelkopf (Stoff/Teer)
        _fillPaint.Color = new SKColor(0x33, 0x22, 0x11);
        canvas.DrawRect(cx - 5, cy - 10, 10, 8, _fillPaint);

        // Glühender Rand am Fackelkopf
        _fillPaint.Color = EmberColor.WithAlpha(80);
        canvas.DrawRect(cx - 5, cy - 10, 10, 2, _fillPaint);
    }

    private void DrawTorchGlow(SKCanvas canvas, float cx, float cy, float w, float h)
    {
        // Warmer Lichtkreis von der Fackel
        float pulse = 0.85f + MathF.Sin(_time * 5f) * 0.1f + MathF.Sin(_time * 7.3f) * 0.05f;
        float radius = w * 0.35f * pulse;

        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy),
            radius,
            [
                new SKColor(0xFF, 0x8C, 0x00, (byte)(20 * pulse)),
                new SKColor(0xFF, 0x8C, 0x00, (byte)(8 * pulse)),
                SKColors.Transparent
            ],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);

        _glowPaint.Shader = shader;
        canvas.DrawRect(0, 0, w, h, _glowPaint);
        _glowPaint.Shader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FLAMMEN
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawFlames(SKCanvas canvas, float cx, float cy, float deltaTime,
        List<FlameParticle> particles)
    {
        _flameTimer += deltaTime;

        // Neue Flammen-Partikel emittieren (häufig, für dichte Flamme)
        if (_flameTimer >= 0.04f)
        {
            _flameTimer = 0;
            if (particles.Count < 12)
            {
                particles.Add(new FlameParticle
                {
                    X = cx + (Random.Shared.NextSingle() - 0.5f) * 6,
                    Y = cy,
                    VX = (Random.Shared.NextSingle() - 0.5f) * 8,
                    VY = -Random.Shared.NextSingle() * 30 - 20,
                    Life = 0.4f + Random.Shared.NextSingle() * 0.3f,
                    MaxLife = 0.4f + Random.Shared.NextSingle() * 0.3f,
                    Size = 4 + Random.Shared.NextSingle() * 4
                });
            }
        }

        // Partikel aktualisieren und zeichnen
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Life -= deltaTime;
            if (p.Life <= 0)
            {
                particles.RemoveAt(i);
                continue;
            }

            p.X += p.VX * deltaTime;
            p.Y += p.VY * deltaTime;
            p.VY -= 10 * deltaTime; // Beschleunigung nach oben
            p.Size *= 0.97f;

            float lifeRatio = p.Life / p.MaxLife;

            // Farbe: Kern (gelb) → Mitte (orange) → Rand (rot) je nach Lebensdauer
            SKColor color;
            if (lifeRatio > 0.6f)
                color = FlameCore.WithAlpha((byte)(220 * lifeRatio));
            else if (lifeRatio > 0.3f)
                color = FlameMid.WithAlpha((byte)(180 * lifeRatio));
            else
                color = FlameOuter.WithAlpha((byte)(120 * lifeRatio));

            _fillPaint.Color = color;
            canvas.DrawCircle(p.X, p.Y, p.Size * lifeRatio, _fillPaint);

            // Innerer Glow
            if (lifeRatio > 0.5f)
            {
                _fillPaint.Color = FlameCore.WithAlpha((byte)(60 * lifeRatio));
                canvas.DrawCircle(p.X, p.Y, p.Size * lifeRatio * 1.8f, _fillPaint);
            }

            particles[i] = p;
        }

        // Statische Flammen-Basis (immer sichtbar)
        float flicker = MathF.Sin(_time * 12f) * 2 + MathF.Sin(_time * 8.3f) * 1.5f;

        // Äußerer Flammen-Kern
        _fillPaint.Color = FlameOuter.WithAlpha(120);
        using (var flamePath = new SKPath())
        {
            flamePath.MoveTo(cx - 6, cy);
            flamePath.QuadTo(cx - 3 + flicker, cy - 18, cx, cy - 24 - MathF.Abs(flicker));
            flamePath.QuadTo(cx + 3 - flicker, cy - 18, cx + 6, cy);
            flamePath.Close();
            canvas.DrawPath(flamePath, _fillPaint);
        }

        // Innerer Flammen-Kern
        _fillPaint.Color = FlameCore.WithAlpha(200);
        using (var corePath = new SKPath())
        {
            corePath.MoveTo(cx - 3, cy);
            corePath.QuadTo(cx - 1 + flicker * 0.5f, cy - 12, cx, cy - 16 - MathF.Abs(flicker) * 0.5f);
            corePath.QuadTo(cx + 1 - flicker * 0.5f, cy - 12, cx + 3, cy);
            corePath.Close();
            canvas.DrawPath(corePath, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GILDEN-WAPPEN
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawGuildEmblem(SKCanvas canvas, float cx, float cy, float size)
    {
        float halfW = size * 0.38f;
        float halfH = size * 0.48f;

        // Schild-Schatten
        _fillPaint.Color = new SKColor(0x00, 0x00, 0x00, 30);
        DrawShieldPath(canvas, cx + 2, cy + 2, halfW, halfH, _fillPaint);

        // Goldener Schild-Rand (Glow)
        float pulse = 0.8f + MathF.Sin(_time * 2f) * 0.15f;
        _fillPaint.Color = ShieldBorder.WithAlpha((byte)(50 * pulse));
        DrawShieldPath(canvas, cx, cy, halfW + 4, halfH + 4, _fillPaint);

        // Schild-Füllung (dunkelbraun)
        _fillPaint.Color = ShieldFill;
        DrawShieldPath(canvas, cx, cy, halfW, halfH, _fillPaint);

        // Goldener Rand
        _strokePaint.Color = ShieldBorder;
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.PathEffect = null;
        DrawShieldPath(canvas, cx, cy, halfW, halfH, _strokePaint);

        // Innerer Zierrand
        _strokePaint.Color = ShieldBorder.WithAlpha(80);
        _strokePaint.StrokeWidth = 1f;
        DrawShieldPath(canvas, cx, cy, halfW - 6, halfH - 6, _strokePaint);

        // Hammer + Zahnrad Emblem in der Mitte
        float iconSize = size * 0.18f;

        // Zahnrad (hinten)
        DrawGear(canvas, cx + iconSize * 0.3f, cy - iconSize * 0.1f, iconSize * 0.7f);

        // Hammer (vorne)
        DrawHammer(canvas, cx - iconSize * 0.15f, cy, iconSize);

        // Goldener Stern über dem Wappen
        float starY = cy - halfH - 4;
        DrawStar(canvas, cx, starY, 8, CraftGold);
    }

    private static void DrawShieldPath(SKCanvas canvas, float cx, float cy, float halfW, float halfH, SKPaint paint)
    {
        using var path = new SKPath();
        // Schild-Form: oben gerade mit abgerundeten Ecken, unten spitz zulaufend
        path.MoveTo(cx - halfW, cy - halfH * 0.6f);
        path.LineTo(cx - halfW, cy + halfH * 0.2f);
        path.QuadTo(cx - halfW * 0.3f, cy + halfH, cx, cy + halfH);
        path.QuadTo(cx + halfW * 0.3f, cy + halfH, cx + halfW, cy + halfH * 0.2f);
        path.LineTo(cx + halfW, cy - halfH * 0.6f);
        // Oberer Bogen
        path.QuadTo(cx + halfW, cy - halfH, cx, cy - halfH);
        path.QuadTo(cx - halfW, cy - halfH, cx - halfW, cy - halfH * 0.6f);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private void DrawGear(SKCanvas canvas, float cx, float cy, float radius)
    {
        // Langsam rotierendes Zahnrad
        float angle = _time * 0.5f;
        int teeth = 8;

        _fillPaint.Color = TorchBracket.WithAlpha(160);
        using var gearPath = new SKPath();

        for (int i = 0; i < teeth * 2; i++)
        {
            float a = angle + i * MathF.PI / teeth;
            float r = (i % 2 == 0) ? radius : radius * 0.72f;
            float x = cx + MathF.Cos(a) * r;
            float y = cy + MathF.Sin(a) * r;

            if (i == 0) gearPath.MoveTo(x, y);
            else gearPath.LineTo(x, y);
        }
        gearPath.Close();
        canvas.DrawPath(gearPath, _fillPaint);

        // Zentraler Kreis
        _fillPaint.Color = ShieldFill;
        canvas.DrawCircle(cx, cy, radius * 0.3f, _fillPaint);
        _strokePaint.Color = TorchBracket.WithAlpha(120);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.PathEffect = null;
        canvas.DrawCircle(cx, cy, radius * 0.3f, _strokePaint);
    }

    private static void DrawHammer(SKCanvas canvas, float cx, float cy, float size)
    {
        float handleLen = size * 1.2f;

        // Stiel (diagonal)
        _strokePaint.Color = TorchWood;
        _strokePaint.StrokeWidth = 3;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        _strokePaint.PathEffect = null;
        canvas.DrawLine(cx - handleLen * 0.35f, cy + handleLen * 0.35f,
                        cx + handleLen * 0.25f, cy - handleLen * 0.25f, _strokePaint);

        // Hammerkopf
        float headX = cx + handleLen * 0.25f;
        float headY = cy - handleLen * 0.25f;
        _fillPaint.Color = TorchBracket;
        canvas.DrawRect(headX - size * 0.35f, headY - size * 0.12f, size * 0.7f, size * 0.24f, _fillPaint);

        // Metallglanz auf Hammerkopf
        _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 30);
        canvas.DrawRect(headX - size * 0.35f, headY - size * 0.12f, size * 0.7f, size * 0.08f, _fillPaint);
    }

    private static void DrawStar(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        _fillPaint.Color = color;
        using var starPath = new SKPath();
        for (int i = 0; i < 10; i++)
        {
            float a = -MathF.PI / 2 + i * MathF.PI / 5;
            float r = (i % 2 == 0) ? size : size * 0.45f;
            float x = cx + MathF.Cos(a) * r;
            float y = cy + MathF.Sin(a) * r;
            if (i == 0) starPath.MoveTo(x, y);
            else starPath.LineTo(x, y);
        }
        starPath.Close();
        canvas.DrawPath(starPath, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIGNETTE
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawVignette(SKCanvas canvas, float w, float h)
    {
        if (_vignetteShader == null || Math.Abs(w - _lastW) > 1 || Math.Abs(h - _lastH) > 1)
        {
            _vignetteShader?.Dispose();
            _vignetteShader = SKShader.CreateRadialGradient(
                new SKPoint(w / 2, h / 2),
                Math.Max(w, h) * 0.7f,
                [SKColors.Transparent, new SKColor(0x10, 0x0A, 0x06, 100)],
                [0.5f, 1f],
                SKShaderTileMode.Clamp);
            _lastW = w;
            _lastH = h;
        }

        _glowPaint.Shader = _vignetteShader;
        canvas.DrawRect(0, 0, w, h, _glowPaint);
        _glowPaint.Shader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FLAMMEN-PARTIKEL STRUCT
    // ═══════════════════════════════════════════════════════════════════════

    private class FlameParticle
    {
        public float X, Y, VX, VY;
        public float Life, MaxLife, Size;
    }
}
