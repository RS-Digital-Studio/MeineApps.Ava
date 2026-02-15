using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert animierte Branch-Header-Banner für den Forschungsbaum.
/// Jeder Branch hat eine thematische Mini-Szene:
/// - Tools: Amboss mit Funken + Hammer + rotierende Zahnräder
/// - Management: Schreibtisch mit Stift + Aktenordner + Diagramm
/// - Marketing: Megaphon mit Schallwellen + wachsendes Balkendiagramm
/// Inkl. Branch-Name, Fortschrittsanzeige (x/15 erforscht).
/// </summary>
public class ResearchBranchBannerRenderer
{
    private float _time;

    // Partikel (Funken für Tools-Branch)
    private readonly List<BannerParticle> _particles = [];
    private float _particleTimer;

    // Farb-Palette
    private static readonly SKColor BgDark = new(0x1E, 0x15, 0x10);
    private static readonly SKColor BgMid = new(0x2A, 0x1F, 0x18);
    private static readonly SKColor MetalColor = new(0x78, 0x90, 0x9C);
    private static readonly SKColor MetalDark = new(0x54, 0x6E, 0x7A);
    private static readonly SKColor WoodColor = new(0x8D, 0x6E, 0x63);
    private static readonly SKColor WoodDark = new(0x6D, 0x4C, 0x41);
    private static readonly SKColor PaperColor = new(0xE8, 0xEA, 0xED);

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _text = new() { IsAntialias = true };

    /// <summary>
    /// Rendert das Branch-Banner.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Verfügbarer Bereich (empfohlen: 60px hoch).</param>
    /// <param name="branch">Branch-Typ.</param>
    /// <param name="branchName">Lokalisierter Branch-Name.</param>
    /// <param name="researchedCount">Anzahl erforschter Items in diesem Branch.</param>
    /// <param name="totalCount">Gesamtzahl Items im Branch (15).</param>
    /// <param name="deltaTime">Zeitdelta in Sekunden.</param>
    public void Render(SKCanvas canvas, SKRect bounds, ResearchBranch branch,
        string branchName, int researchedCount, int totalCount, float deltaTime)
    {
        _time += deltaTime;

        float x = bounds.Left;
        float y = bounds.Top;
        float w = bounds.Width;
        float h = bounds.Height;
        var branchColor = ResearchItemRenderer.GetBranchColor(branch);

        // Hintergrund mit Gradient-Effekt
        DrawBackground(canvas, x, y, w, h, branchColor);

        // Branch-spezifische Szene (linke Hälfte)
        float sceneW = w * 0.4f;
        switch (branch)
        {
            case ResearchBranch.Tools:
                DrawToolsScene(canvas, x, y, sceneW, h, branchColor, deltaTime);
                break;
            case ResearchBranch.Management:
                DrawManagementScene(canvas, x, y, sceneW, h, branchColor);
                break;
            case ResearchBranch.Marketing:
                DrawMarketingScene(canvas, x, y, sceneW, h, branchColor);
                break;
        }

        // Branch-Name + Fortschritt (rechte Hälfte)
        DrawBranchInfo(canvas, x + sceneW, y, w - sceneW, h, branchColor, branchName, researchedCount, totalCount);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawBackground(SKCanvas canvas, float x, float y, float w, float h, SKColor branchColor)
    {
        // Dunkler Hintergrund
        _fill.Color = BgDark;
        var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 8);
        canvas.DrawRoundRect(rect, _fill);

        // Farbiger Gradient-Overlay (dezent)
        _fill.Color = branchColor.WithAlpha(20);
        canvas.DrawRoundRect(rect, _fill);

        // Rahmen
        _stroke.Color = branchColor.WithAlpha(60);
        _stroke.StrokeWidth = 1;
        canvas.DrawRoundRect(rect, _stroke);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOOLS-SZENE: Amboss + Hammer + Funken + Zahnräder
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawToolsScene(SKCanvas canvas, float x, float y, float w, float h,
        SKColor branchColor, float deltaTime)
    {
        float cx = x + w * 0.5f;
        float groundY = y + h * 0.78f;

        // Amboss
        float ambossW = w * 0.25f;
        float ambossH = h * 0.28f;
        float ambossCx = cx - w * 0.05f;

        // Amboss-Körper (Trapez simuliert)
        _fill.Color = MetalColor;
        canvas.DrawRect(ambossCx - ambossW * 0.4f, groundY - ambossH, ambossW * 0.8f, ambossH * 0.5f, _fill);
        _fill.Color = MetalDark;
        canvas.DrawRect(ambossCx - ambossW * 0.5f, groundY - ambossH, ambossW, ambossH * 0.2f, _fill);

        // Amboss-Sockel
        _fill.Color = WoodDark;
        canvas.DrawRect(ambossCx - ambossW * 0.3f, groundY - ambossH * 0.5f, ambossW * 0.6f, ambossH * 0.5f, _fill);

        // Hammer (animiert: leichte Rotation)
        float hammerAngle = MathF.Sin(_time * 3f) * 0.15f;
        canvas.Save();
        canvas.Translate(ambossCx + ambossW * 0.3f, groundY - ambossH * 1.2f);
        canvas.RotateDegrees(hammerAngle * 57.3f);

        // Hammer-Stiel
        _fill.Color = WoodColor;
        canvas.DrawRect(-1.5f, -h * 0.2f, 3, h * 0.25f, _fill);

        // Hammer-Kopf
        _fill.Color = MetalColor;
        canvas.DrawRect(-6, -h * 0.2f - 4, 12, 6, _fill);

        canvas.Restore();

        // Rotierendes Zahnrad (rechts oben)
        float gearCx = x + w * 0.82f;
        float gearCy = y + h * 0.28f;
        DrawMiniGear(canvas, gearCx, gearCy, 8, _time * 1.2f, branchColor);

        // Kleines Zahnrad (rechts unten, gegenläufig)
        DrawMiniGear(canvas, gearCx + 10, gearCy + 10, 5, -_time * 1.8f, branchColor);

        // Funken-Partikel
        UpdateAndDrawParticles(canvas, ambossCx, groundY - ambossH, branchColor, deltaTime);
    }

    private static void DrawMiniGear(SKCanvas canvas, float cx, float cy, float radius, float angle, SKColor color)
    {
        _fill.Color = color.WithAlpha(120);
        canvas.DrawCircle(cx, cy, radius, _fill);

        _fill.Color = BgDark;
        canvas.DrawCircle(cx, cy, radius * 0.35f, _fill);

        // 5 Zähne
        _fill.Color = color.WithAlpha(120);
        for (int i = 0; i < 5; i++)
        {
            float a = angle + i * MathF.Tau / 5;
            float tx = cx + MathF.Cos(a) * radius;
            float ty = cy + MathF.Sin(a) * radius;
            canvas.DrawCircle(tx, ty, 2.5f, _fill);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MANAGEMENT-SZENE: Schreibtisch + Stift + Aktenordner + Diagramm
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawManagementScene(SKCanvas canvas, float x, float y, float w, float h, SKColor branchColor)
    {
        float cx = x + w * 0.5f;
        float groundY = y + h * 0.8f;

        // Schreibtisch
        float deskW = w * 0.6f;
        float deskH = 5;
        _fill.Color = WoodColor;
        canvas.DrawRect(cx - deskW / 2, groundY - deskH, deskW, deskH, _fill);

        // Tischbeine
        _fill.Color = WoodDark;
        canvas.DrawRect(cx - deskW / 2 + 3, groundY, 3, h * 0.15f, _fill);
        canvas.DrawRect(cx + deskW / 2 - 6, groundY, 3, h * 0.15f, _fill);

        // Aktenordner (links auf dem Tisch)
        float folderX = cx - deskW * 0.35f;
        float folderH = h * 0.22f;
        _fill.Color = branchColor;
        canvas.DrawRect(folderX, groundY - deskH - folderH, 10, folderH, _fill);
        _fill.Color = branchColor.WithAlpha(180);
        canvas.DrawRect(folderX + 10, groundY - deskH - folderH + 2, 8, folderH - 2, _fill);

        // Papier auf dem Tisch
        _fill.Color = PaperColor.WithAlpha(200);
        float paperX = cx - 8;
        float paperY = groundY - deskH - h * 0.2f;
        canvas.DrawRect(paperX, paperY, 16, h * 0.18f, _fill);

        // Linien auf dem Papier
        _stroke.Color = new SKColor(0x90, 0x90, 0x90);
        _stroke.StrokeWidth = 0.8f;
        for (int i = 0; i < 3; i++)
        {
            float ly = paperY + 3 + i * 3.5f;
            canvas.DrawLine(paperX + 2, ly, paperX + 14, ly, _stroke);
        }

        // Stift (animiert: schreibt hin und her)
        float penOffset = MathF.Sin(_time * 2f) * 4;
        float penX = cx - 2 + penOffset;
        float penY = groundY - deskH - h * 0.1f;

        _stroke.Color = branchColor;
        _stroke.StrokeWidth = 2;
        canvas.DrawLine(penX, penY, penX + 8, penY - 12, _stroke);

        // Stiftspitze
        _fill.Color = new SKColor(0x33, 0x33, 0x33);
        canvas.DrawCircle(penX, penY, 1.5f, _fill);

        // Mini-Balkendiagramm (rechts)
        float chartX = cx + deskW * 0.1f;
        float chartBottom = groundY - deskH - 2;
        float barW = 4;

        for (int i = 0; i < 3; i++)
        {
            float barH = (8 + i * 5) * (0.7f + MathF.Sin(_time * 0.8f + i * 0.5f) * 0.3f);
            _fill.Color = branchColor.WithAlpha((byte)(140 + i * 35));
            canvas.DrawRect(chartX + i * (barW + 2), chartBottom - barH, barW, barH, _fill);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MARKETING-SZENE: Megaphon + Schallwellen + wachsendes Diagramm
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawMarketingScene(SKCanvas canvas, float x, float y, float w, float h, SKColor branchColor)
    {
        float cx = x + w * 0.4f;
        float cy = y + h * 0.5f;

        // Megaphon
        float megaW = w * 0.2f;
        float megaH = h * 0.25f;

        // Trichter (Trapez)
        using var megaPath = new SKPath();
        megaPath.MoveTo(cx - megaW * 0.3f, cy - megaH * 0.3f);
        megaPath.LineTo(cx + megaW, cy - megaH * 0.8f);
        megaPath.LineTo(cx + megaW, cy + megaH * 0.8f);
        megaPath.LineTo(cx - megaW * 0.3f, cy + megaH * 0.3f);
        megaPath.Close();
        _fill.Color = branchColor;
        canvas.DrawPath(megaPath, _fill);

        // Griff
        _fill.Color = WoodDark;
        canvas.DrawRect(cx - megaW * 0.5f, cy - 3, megaW * 0.25f, 6, _fill);

        // Schallwellen (expandierend, pulsierend)
        float waveX = cx + megaW + 5;
        for (int i = 0; i < 3; i++)
        {
            float wavePhase = (_time * 1.5f + i * 0.7f) % 2.0f;
            if (wavePhase > 1.5f) continue;

            float waveRadius = 8 + wavePhase * 14;
            byte waveAlpha = (byte)((1.0f - wavePhase / 1.5f) * 150);

            _stroke.Color = branchColor.WithAlpha(waveAlpha);
            _stroke.StrokeWidth = 2;

            // Halbbogen (nur rechte Seite)
            canvas.DrawArc(
                new SKRect(waveX - waveRadius, cy - waveRadius, waveX + waveRadius, cy + waveRadius),
                -50, 100, false, _stroke);
        }

        // Wachsendes Balkendiagramm (rechts)
        float chartX = x + w * 0.7f;
        float chartBottom = y + h * 0.82f;
        float barW = 5;

        for (int i = 0; i < 4; i++)
        {
            // Wachsende Höhe mit Animation
            float targetH = 6 + i * 6;
            float growFactor = Math.Clamp(MathF.Sin(_time * 0.5f + i * 0.3f) * 0.15f + 0.85f, 0.5f, 1.0f);
            float barH = targetH * growFactor;

            _fill.Color = branchColor.WithAlpha((byte)(100 + i * 35));
            canvas.DrawRect(chartX + i * (barW + 2), chartBottom - barH, barW, barH, _fill);
        }

        // Trendlinie (aufsteigend)
        _stroke.Color = branchColor;
        _stroke.StrokeWidth = 1.5f;
        float trendStartX = chartX;
        float trendEndX = chartX + 4 * (barW + 2) - 2;
        canvas.DrawLine(trendStartX, chartBottom - 4, trendEndX, chartBottom - 26, _stroke);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BRANCH-INFO (Name + Fortschritt)
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawBranchInfo(SKCanvas canvas, float x, float y, float w, float h,
        SKColor branchColor, string branchName, int researchedCount, int totalCount)
    {
        // Branch-Name
        using var nameFont = new SKFont { Size = 16, Embolden = true };
        _text.Color = branchColor;
        canvas.DrawText(branchName, x + 8, y + h * 0.38f, nameFont, _text);

        // Fortschritt "7/15 erforscht"
        using var progressFont = new SKFont { Size = 11 };
        _text.Color = new SKColor(0xA0, 0x90, 0x80);
        canvas.DrawText($"{researchedCount}/{totalCount}", x + 8, y + h * 0.6f, progressFont, _text);

        // Mini-Fortschrittsbalken
        float barX = x + 8;
        float barY = y + h * 0.7f;
        float barW = w - 24;
        float barH = 4;

        // Hintergrund
        _fill.Color = new SKColor(0x20, 0x15, 0x12);
        var bgRect = new SKRoundRect(new SKRect(barX, barY, barX + barW, barY + barH), 2);
        canvas.DrawRoundRect(bgRect, _fill);

        // Fortschritt
        if (totalCount > 0)
        {
            float progress = (float)researchedCount / totalCount;
            float fillW = barW * progress;
            if (fillW > 0)
            {
                _fill.Color = branchColor;
                var fillRect = new SKRoundRect(new SKRect(barX, barY, barX + fillW, barY + barH), 2);
                canvas.DrawRoundRect(fillRect, _fill);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL (Funken für Tools-Branch)
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawParticles(SKCanvas canvas, float spawnX, float spawnY,
        SKColor color, float deltaTime)
    {
        _particleTimer += deltaTime;

        // Neue Funken erzeugen
        if (_particleTimer >= 0.12f)
        {
            _particleTimer = 0;
            if (_particles.Count < 15)
            {
                _particles.Add(new BannerParticle
                {
                    X = spawnX + (Random.Shared.NextSingle() - 0.5f) * 10,
                    Y = spawnY,
                    VX = (Random.Shared.NextSingle() - 0.5f) * 30,
                    VY = -(15 + Random.Shared.NextSingle() * 25),
                    Life = 1.0f,
                    Size = 1 + Random.Shared.NextSingle() * 1.5f
                });
            }
        }

        // Aktualisieren und zeichnen
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.VX * deltaTime;
            p.Y += p.VY * deltaTime;
            p.VY += 20 * deltaTime; // Gravity
            p.Life -= deltaTime * 1.8f;

            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }

            byte alpha = (byte)(p.Life * 255);
            _fill.Color = color.WithAlpha(alpha);
            canvas.DrawCircle(p.X, p.Y, p.Size * p.Life, _fill);
        }
    }

    private class BannerParticle
    {
        public float X, Y, VX, VY, Life, Size;
    }
}
