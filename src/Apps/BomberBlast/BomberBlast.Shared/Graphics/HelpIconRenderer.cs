using BomberBlast.Models.Entities;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Statische Render-Methoden für Gegner-, Boss-, PowerUp- und Bomben-Icons.
/// Gleiche Farben, Formen und Proportionen wie GameRenderer (ohne Animationen).
/// Verwendet in HelpView, CollectionView und DeckView.
/// </summary>
public static class HelpIconRenderer
{
    /// <summary>
    /// Zeichnet einen Gegner (statisch, ohne Wobble/Blink) - passend zum einzigartigen GameRenderer-Design.
    /// </summary>
    public static void DrawEnemy(SKCanvas canvas, float cx, float cy, float size, EnemyType type)
    {
        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };

        // Schatten unter jedem Gegner
        fillPaint.Color = new SKColor(0, 0, 0, 30);
        canvas.DrawOval(cx, cy + size * 0.35f, size * 0.25f, size * 0.06f, fillPaint);

        switch (type)
        {
            case EnemyType.Ballom: DrawBallom(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Onil: DrawOnil(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Doll: DrawDoll(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Minvo: DrawMinvo(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Kondoria: DrawKondoria(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Ovapi: DrawOvapi(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Pass: DrawPass(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Pontan: DrawPontan(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Tanker: DrawTanker(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Ghost: DrawGhost(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Splitter: DrawSplitter(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case EnemyType.Mimic: DrawMimic(canvas, cx, cy, size, fillPaint, strokePaint); break;
        }
    }

    /// <summary>Ballom: Runder gelber Blob, große doofe Augen, breites Grinsen</summary>
    private static void DrawBallom(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.32f;

        // Runder Blob-Körper
        fill.Color = new SKColor(255, 180, 50);
        canvas.DrawOval(cx, cy, r, r, fill);
        // Bauch-Highlight
        fill.Color = new SKColor(255, 210, 100, 80);
        canvas.DrawOval(cx, cy + r * 0.15f, r * 0.5f, r * 0.4f, fill);

        // Große doofe Augen
        float eyeY = cy - r * 0.2f;
        float eyeSp = r * 0.4f;
        DrawSimpleEyes(canvas, cx, eyeY, eyeSp, s * 0.12f, s * 0.065f, fill, false);

        // Breites Grinsen
        stroke.Color = new SKColor(120, 60, 0);
        stroke.StrokeWidth = 1.2f;
        using var path = new SKPath();
        float my = cy + r * 0.35f;
        path.MoveTo(cx - s * 0.12f, my);
        path.QuadTo(cx, my + s * 0.09f, cx + s * 0.12f, my);
        canvas.DrawPath(path, stroke);
    }

    /// <summary>Onil: Tropfenform, listige schräge Augen, ein Fangzahn</summary>
    private static void DrawOnil(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.3f;

        // Tropfenform (breiter unten)
        fill.Color = new SKColor(80, 120, 255);
        using var body = new SKPath();
        body.MoveTo(cx, cy - r * 1.1f);
        body.CubicTo(cx + r * 1.2f, cy - r * 0.3f, cx + r * 1.1f, cy + r * 0.8f, cx, cy + r);
        body.CubicTo(cx - r * 1.1f, cy + r * 0.8f, cx - r * 1.2f, cy - r * 0.3f, cx, cy - r * 1.1f);
        body.Close();
        canvas.DrawPath(body, fill);

        // Listige schräge Augen
        float eyeY = cy - r * 0.1f;
        float eyeSp = r * 0.45f;
        DrawSimpleEyes(canvas, cx, eyeY, eyeSp, s * 0.09f, s * 0.055f, fill, true);

        // Ein Fangzahn
        fill.Color = SKColors.White;
        float my = eyeY + r * 0.55f;
        canvas.DrawRect(cx + 1, my, s * 0.06f, s * 0.09f, fill);
    }

    /// <summary>Doll: Rund mit Haarschleife, große niedliche Augen, Füßchen</summary>
    private static void DrawDoll(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.28f;

        // Runder Körper
        fill.Color = new SKColor(255, 150, 200);
        canvas.DrawCircle(cx, cy, r, fill);
        // Bauch-Highlight
        fill.Color = new SKColor(255, 190, 220, 80);
        canvas.DrawOval(cx, cy + 2, r * 0.55f, r * 0.4f, fill);

        // Haarschleife oben
        fill.Color = new SKColor(255, 60, 100);
        float bow = s * 0.09f;
        canvas.DrawCircle(cx - bow, cy - r - 1, bow, fill);
        canvas.DrawCircle(cx + bow, cy - r - 1, bow, fill);
        canvas.DrawCircle(cx, cy - r, bow * 0.66f, fill);

        // Große niedliche Augen (größer als normal)
        float eyeY = cy - r * 0.15f;
        float eyeSp = r * 0.5f;
        fill.Color = SKColors.White;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.12f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.12f, fill);
        // Große dunkle Pupillen mit Glanzpunkt
        fill.Color = new SKColor(60, 30, 60);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.075f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.075f, fill);
        fill.Color = new SKColor(255, 255, 255, 200);
        canvas.DrawCircle(cx - eyeSp - 1, eyeY - 1, s * 0.03f, fill);
        canvas.DrawCircle(cx + eyeSp - 1, eyeY - 1, s * 0.03f, fill);

        // Kleine Füßchen unten
        fill.Color = new SKColor(200, 100, 140);
        float footY = cy + r - 1;
        canvas.DrawOval(cx - bow, footY, bow, s * 0.06f, fill);
        canvas.DrawOval(cx + bow, footY, bow, s * 0.06f, fill);
    }

    /// <summary>Minvo: Eckig mit Hörnern, wütende Augen, Zähne</summary>
    private static void DrawMinvo(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float w = s * 0.5f, h = s * 0.55f;

        // Hörner
        fill.Color = new SKColor(180, 30, 30);
        using var hornL = new SKPath();
        hornL.MoveTo(cx - w * 0.35f, cy - h * 0.4f);
        hornL.LineTo(cx - w * 0.5f, cy - h * 0.75f);
        hornL.LineTo(cx - w * 0.15f, cy - h * 0.35f);
        hornL.Close();
        canvas.DrawPath(hornL, fill);
        using var hornR = new SKPath();
        hornR.MoveTo(cx + w * 0.35f, cy - h * 0.4f);
        hornR.LineTo(cx + w * 0.5f, cy - h * 0.75f);
        hornR.LineTo(cx + w * 0.15f, cy - h * 0.35f);
        hornR.Close();
        canvas.DrawPath(hornR, fill);

        // Eckiger Körper
        fill.Color = new SKColor(255, 60, 60);
        canvas.DrawRoundRect(cx - w / 2, cy - h / 2, w, h, 3, 3, fill);

        // Zusammengekniffene wütende Augen
        float eyeY = cy - h * 0.08f;
        float eyeSp = w * 0.25f;
        fill.Color = new SKColor(255, 230, 200);
        canvas.DrawOval(cx - eyeSp, eyeY, s * 0.09f, s * 0.06f, fill);
        canvas.DrawOval(cx + eyeSp, eyeY, s * 0.09f, s * 0.06f, fill);
        fill.Color = new SKColor(200, 0, 0);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.045f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.045f, fill);

        // V-förmige wütende Augenbrauen
        stroke.Color = new SKColor(100, 0, 0);
        stroke.StrokeWidth = s * 0.06f;
        float browY = eyeY - s * 0.09f;
        canvas.DrawLine(cx - eyeSp - s * 0.09f, browY - 2, cx - eyeSp + s * 0.06f, browY + 1, stroke);
        canvas.DrawLine(cx + eyeSp + s * 0.09f, browY - 2, cx + eyeSp - s * 0.06f, browY + 1, stroke);

        // Zähne-fletschendes Maul
        stroke.Color = new SKColor(100, 0, 0);
        stroke.StrokeWidth = 1.2f;
        float my = eyeY + h * 0.28f;
        canvas.DrawLine(cx - s * 0.12f, my, cx + s * 0.12f, my, stroke);
        fill.Color = SKColors.White;
        for (int i = -2; i <= 2; i++)
            canvas.DrawRect(cx + i * s * 0.065f - 0.5f, my - 1, s * 0.045f, s * 0.075f, fill);
    }

    /// <summary>Kondoria: Pilzform mit breitem Hut, schläfrige Augen</summary>
    private static void DrawKondoria(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.28f;

        // Stiel (Fuß)
        fill.Color = new SKColor(200, 120, 240);
        canvas.DrawRoundRect(cx - r * 0.4f, cy - r * 0.2f, r * 0.8f, r * 1.1f, 3, 3, fill);

        // Pilzhut (breite Halbkugel oben)
        fill.Color = new SKColor(180, 80, 220);
        canvas.DrawOval(cx, cy - r * 0.5f, r * 1.3f, r * 0.8f, fill);
        // Punkte auf dem Hut
        fill.Color = new SKColor(220, 160, 255, 120);
        canvas.DrawCircle(cx - r * 0.5f, cy - r * 0.7f, s * 0.06f, fill);
        canvas.DrawCircle(cx + r * 0.3f, cy - r * 0.8f, s * 0.045f, fill);

        // Schläfrige halbgeschlossene Augen
        float eyeY = cy - r * 0.1f;
        float eyeSp = r * 0.45f;
        fill.Color = SKColors.White;
        canvas.DrawOval(cx - eyeSp, eyeY, s * 0.09f, s * 0.055f, fill);
        canvas.DrawOval(cx + eyeSp, eyeY, s * 0.09f, s * 0.055f, fill);
        // Halb geschlossene Lider (obere Hälfte)
        fill.Color = new SKColor(200, 120, 240);
        canvas.DrawRect(cx - eyeSp - s * 0.09f, eyeY - s * 0.075f, s * 0.18f, s * 0.06f, fill);
        canvas.DrawRect(cx + eyeSp - s * 0.09f, eyeY - s * 0.075f, s * 0.18f, s * 0.06f, fill);
        // Kleine Pupillen
        fill.Color = SKColors.Black;
        canvas.DrawCircle(cx - eyeSp, eyeY + 0.5f, s * 0.036f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY + 0.5f, s * 0.036f, fill);
    }

    /// <summary>Ovapi: Oktopus mit Tentakeln, leuchtende Augen, Schlitz-Pupillen</summary>
    private static void DrawOvapi(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.26f;

        // Tentakel (4 Stück, statisch wellig)
        stroke.Color = new SKColor(60, 220, 220);
        stroke.StrokeWidth = s * 0.075f;
        for (int i = 0; i < 4; i++)
        {
            float tx = cx + (i - 1.5f) * r * 0.6f;
            float wave = (i % 2 == 0 ? 3f : -3f);
            using var tent = new SKPath();
            tent.MoveTo(tx, cy + r * 0.3f);
            tent.QuadTo(tx + wave, cy + r * 0.3f + r * 0.5f, tx - wave * 0.5f, cy + r * 0.3f + r * 0.9f);
            canvas.DrawPath(tent, stroke);
        }

        // Runder Kopf
        fill.Color = new SKColor(80, 255, 255);
        canvas.DrawOval(cx, cy - r * 0.1f, r * 1.1f, r, fill);

        // Leuchtende Augen
        float eyeY = cy - r * 0.2f;
        float eyeSp = r * 0.5f;
        fill.Color = new SKColor(0, 255, 200);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.09f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.09f, fill);
        // Dunkle Schlitz-Pupillen
        fill.Color = new SKColor(0, 80, 80);
        canvas.DrawOval(cx - eyeSp, eyeY, s * 0.03f, s * 0.075f, fill);
        canvas.DrawOval(cx + eyeSp, eyeY, s * 0.03f, s * 0.075f, fill);
    }

    /// <summary>Pass: Pfeilförmig/keilförmig, aggressive schmale rote Augen</summary>
    private static void DrawPass(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float w = s * 0.5f, h = s * 0.5f;

        // Keilform (Pfeilspitze nach rechts)
        fill.Color = new SKColor(255, 255, 80);
        using var body = new SKPath();
        body.MoveTo(cx + w * 0.5f, cy);
        body.LineTo(cx - w * 0.4f, cy - h * 0.45f);
        body.LineTo(cx - w * 0.4f, cy + h * 0.45f);
        body.Close();
        canvas.DrawPath(body, fill);

        // Aggressive schmale Augen
        float eyeY = cy - 1;
        float eyeSp = w * 0.15f;
        fill.Color = new SKColor(200, 0, 0);
        canvas.DrawOval(cx - eyeSp, eyeY, s * 0.075f, s * 0.045f, fill);
        canvas.DrawOval(cx + eyeSp, eyeY, s * 0.075f, s * 0.045f, fill);
        fill.Color = SKColors.Black;
        canvas.DrawOval(cx - eyeSp, eyeY, s * 0.036f, s * 0.036f, fill);
        canvas.DrawOval(cx + eyeSp, eyeY, s * 0.036f, s * 0.036f, fill);
    }

    /// <summary>Pontan: Flammenform, mehrschichtig, glühende rote Augen</summary>
    private static void DrawPontan(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.28f;

        // Flammenkörper (3 Zungen, statisch)
        for (int i = 2; i >= 0; i--)
        {
            float tongueH = r * (1.4f + i * 0.15f);
            byte alpha = (byte)(200 - i * 40);
            fill.Color = i == 0 ? new SKColor(255, 255, 255, alpha) :
                i == 1 ? new SKColor(255, 200, 60, alpha) :
                         new SKColor(255, 100, 20, alpha);
            float tongueW = r * (0.8f + i * 0.2f);
            using var flame = new SKPath();
            flame.MoveTo(cx - tongueW, cy + r * 0.3f);
            flame.QuadTo(cx, cy - tongueH, cx + tongueW, cy + r * 0.3f);
            flame.Close();
            canvas.DrawPath(flame, fill);
        }

        // Glühende rote Augen
        float eyeY = cy - r * 0.15f;
        float eyeSp = r * 0.4f;
        fill.Color = new SKColor(255, 50, 20);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.075f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.075f, fill);
        // Heller Kern
        fill.Color = new SKColor(255, 200, 100);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.036f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.036f, fill);
    }

    /// <summary>Tanker: Kastenform mit Rüstungsplatten, Visier-Schlitz statt Augen</summary>
    private static void DrawTanker(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float w = s * 0.55f, h = s * 0.6f;

        // Schwerer Kastenförmiger Körper
        fill.Color = new SKColor(100, 100, 120);
        canvas.DrawRoundRect(cx - w / 2, cy - h / 2, w, h, 4, 4, fill);

        // Rüstungsplatten (horizontale Streifen + Nieten)
        stroke.Color = new SKColor(130, 130, 150);
        stroke.StrokeWidth = 1.5f;
        float plateY1 = cy - h * 0.25f;
        float plateY2 = cy + h * 0.1f;
        canvas.DrawLine(cx - w * 0.4f, plateY1, cx + w * 0.4f, plateY1, stroke);
        canvas.DrawLine(cx - w * 0.35f, plateY2, cx + w * 0.35f, plateY2, stroke);
        // Nieten
        fill.Color = new SKColor(160, 160, 180);
        float nrx = w * 0.35f;
        canvas.DrawCircle(cx - nrx, plateY1, s * 0.045f, fill);
        canvas.DrawCircle(cx + nrx, plateY1, s * 0.045f, fill);
        canvas.DrawCircle(cx - nrx + 2, plateY2, s * 0.045f, fill);
        canvas.DrawCircle(cx + nrx - 2, plateY2, s * 0.045f, fill);

        // Visier-Schlitz (statt Augen)
        float visorY = cy - h * 0.1f;
        fill.Color = new SKColor(255, 60, 40);
        canvas.DrawRoundRect(cx - w * 0.3f, visorY - s * 0.045f, w * 0.6f, s * 0.09f, 1, 1, fill);
    }

    /// <summary>Ghost: Klassische Geisterform mit welligem Saum, leuchtende hohle Augen</summary>
    private static void DrawGhost(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.3f;

        // Geisterkörper (oben rund, unten wellig)
        fill.Color = new SKColor(180, 200, 255, 220);
        using var ghost = new SKPath();
        ghost.MoveTo(cx - r, cy);
        ghost.ArcTo(new SKRect(cx - r, cy - r * 1.6f, cx + r, cy), 180, 180, false);
        // Welliger unterer Saum
        float bottomY = cy + r * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            float segW = r * 0.5f;
            float x1 = cx + r - i * segW;
            float x2 = x1 - segW;
            float wave = (i % 2 == 0) ? 3f : -2f;
            ghost.QuadTo(x1 - segW * 0.5f, bottomY + wave + 3, x2, bottomY);
        }
        ghost.Close();
        canvas.DrawPath(ghost, fill);

        // Leuchtende hohle Augen
        float eyeY = cy - r * 0.3f;
        float eyeSp = r * 0.4f;
        fill.Color = new SKColor(100, 180, 255);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.09f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.09f, fill);
        // Dunkle Pupillen
        fill.Color = new SKColor(20, 30, 60);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.045f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.045f, fill);

        // "O"-Mund
        stroke.Color = new SKColor(60, 80, 120);
        stroke.StrokeWidth = 1f;
        canvas.DrawCircle(cx, eyeY + r * 0.5f, s * 0.06f, stroke);
    }

    /// <summary>Splitter: Zellform mit Noppen, nervöse Augen, Teilungslinie</summary>
    private static void DrawSplitter(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.27f;

        // Zellkörper
        fill.Color = new SKColor(255, 200, 0);
        canvas.DrawCircle(cx, cy, r, fill);

        // Noppen/Pseudopodien (4 Stück)
        fill.Color = new SKColor(255, 180, 0);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * MathF.PI / 2f;
            float nx = cx + MathF.Cos(angle) * (r + s * 0.06f);
            float ny = cy + MathF.Sin(angle) * (r + s * 0.06f);
            canvas.DrawCircle(nx, ny, s * 0.075f, fill);
        }

        // Nervöse Augen
        float eyeY = cy - 2;
        float eyeSp = r * 0.35f;
        fill.Color = SKColors.White;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.09f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.09f, fill);
        fill.Color = SKColors.Black;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.055f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.055f, fill);

        // Teilungs-Linie (zeigt dass er sich teilen kann)
        stroke.Color = new SKColor(200, 150, 0, 120);
        stroke.StrokeWidth = 1f;
        canvas.DrawLine(cx, cy - r * 0.8f, cx, cy + r * 0.8f, stroke);
    }

    /// <summary>Mimic: Aktiver Modus - Block mit aufgerissenem Maul, rote Augen</summary>
    private static void DrawMimic(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float w = s * 0.5f, h = s * 0.5f;
        float mawOpen = 3f;

        // Geöffneter Block-Körper (Split-Maul)
        fill.Color = new SKColor(180, 120, 60);
        // Obere Hälfte
        canvas.DrawRoundRect(cx - w / 2, cy - h / 2 - mawOpen, w, h * 0.45f, 3, 3, fill);
        // Untere Hälfte
        canvas.DrawRoundRect(cx - w / 2, cy + mawOpen, w, h * 0.45f, 3, 3, fill);

        // Zähne im Maul
        fill.Color = SKColors.White;
        float teethY = cy - mawOpen + 1;
        for (int i = -2; i <= 2; i++)
            canvas.DrawRect(cx + i * s * 0.09f, teethY, s * 0.06f, s * 0.09f, fill);
        float teethYBot = cy + mawOpen - s * 0.09f;
        for (int i = -2; i <= 2; i++)
            canvas.DrawRect(cx + i * s * 0.09f + s * 0.045f, teethYBot, s * 0.06f, s * 0.09f, fill);

        // Rote Augen
        float eyeY = cy - h * 0.25f - mawOpen;
        float eyeSp = w * 0.25f;
        fill.Color = new SKColor(255, 40, 40);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.075f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.075f, fill);
        fill.Color = SKColors.Black;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.036f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.036f, fill);
    }

    /// <summary>Standard-Augen (rund oder schräg, wiederverwendbar)</summary>
    private static void DrawSimpleEyes(SKCanvas canvas, float cx, float eyeY, float eyeSp,
        float eyeR, float pupilR, SKPaint fill, bool slanted)
    {
        fill.Color = SKColors.White;
        if (slanted)
        {
            canvas.DrawOval(cx - eyeSp, eyeY, eyeR, eyeR * 0.7f, fill);
            canvas.DrawOval(cx + eyeSp, eyeY, eyeR, eyeR * 0.7f, fill);
        }
        else
        {
            canvas.DrawCircle(cx - eyeSp, eyeY, eyeR, fill);
            canvas.DrawCircle(cx + eyeSp, eyeY, eyeR, fill);
        }

        // Pupillen (mittig, keine Richtung in HelpIcon)
        fill.Color = SKColors.Black;
        canvas.DrawCircle(cx - eyeSp, eyeY, pupilR, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, pupilR, fill);

        // Glanzpunkte
        fill.Color = new SKColor(255, 255, 255, 180);
        canvas.DrawCircle(cx - eyeSp - 0.5f, eyeY - 0.5f, pupilR * 0.35f, fill);
        canvas.DrawCircle(cx + eyeSp - 0.5f, eyeY - 0.5f, pupilR * 0.35f, fill);
    }

    /// <summary>
    /// Zeichnet ein PowerUp (statisch, ohne Bobbing/Blink) - identisch zum Spiel.
    /// </summary>
    public static void DrawPowerUp(SKCanvas canvas, float cx, float cy, float size, PowerUpType type)
    {
        var color = GetPowerUpColor(type);
        float radius = size * 0.35f;

        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };

        // Farbiger Kreis-Hintergrund
        fillPaint.Color = color;
        canvas.DrawCircle(cx, cy, radius, fillPaint);

        // Weißes Icon
        fillPaint.Color = SKColors.White;
        DrawPowerUpIcon(canvas, type, cx, cy, radius * 0.6f, fillPaint, strokePaint);
    }

    /// <summary>
    /// Zeichnet das PowerUp-Icon (gleiche Logik wie GameRenderer.RenderPowerUpIcon).
    /// </summary>
    private static void DrawPowerUpIcon(SKCanvas canvas, PowerUpType type,
        float cx, float cy, float size, SKPaint fillPaint, SKPaint strokePaint)
    {
        strokePaint.Color = SKColors.White;
        strokePaint.StrokeWidth = size * 0.18f;

        switch (type)
        {
            case PowerUpType.BombUp:
                // Kleine Bombe
                canvas.DrawCircle(cx, cy + size * 0.08f, size * 0.5f, fillPaint);
                canvas.DrawLine(cx, cy - size * 0.3f, cx + size * 0.3f, cy - size * 0.6f, strokePaint);
                break;

            case PowerUpType.Fire:
                // Flammenform (Dreieck)
                using (var path = new SKPath())
                {
                    path.MoveTo(cx, cy - size * 0.7f);
                    path.LineTo(cx + size * 0.4f, cy + size * 0.5f);
                    path.LineTo(cx - size * 0.4f, cy + size * 0.5f);
                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                }
                break;

            case PowerUpType.Speed:
                // Pfeil nach rechts
                using (var path = new SKPath())
                {
                    path.MoveTo(cx - size * 0.4f, cy - size * 0.3f);
                    path.LineTo(cx + size * 0.5f, cy);
                    path.LineTo(cx - size * 0.4f, cy + size * 0.3f);
                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                }
                break;

            case PowerUpType.Wallpass:
                // Ghost-Form
                canvas.DrawCircle(cx, cy - size * 0.15f, size * 0.35f, fillPaint);
                canvas.DrawRect(cx - size * 0.35f, cy, size * 0.7f, size * 0.3f, fillPaint);
                break;

            case PowerUpType.Detonator:
                // Blitz
                using (var path = new SKPath())
                {
                    path.MoveTo(cx + size * 0.15f, cy - size * 0.6f);
                    path.LineTo(cx - size * 0.2f, cy + size * 0.05f);
                    path.LineTo(cx + size * 0.1f, cy + size * 0.05f);
                    path.LineTo(cx - size * 0.15f, cy + size * 0.6f);
                    canvas.DrawPath(path, strokePaint);
                }
                break;

            case PowerUpType.Bombpass:
                // Kreis mit Pfeil
                strokePaint.StrokeWidth = size * 0.14f;
                canvas.DrawCircle(cx, cy, size * 0.4f, strokePaint);
                canvas.DrawLine(cx - size * 0.6f, cy, cx + size * 0.6f, cy, strokePaint);
                break;

            case PowerUpType.Flamepass:
                // Schildform
                using (var path = new SKPath())
                {
                    path.MoveTo(cx, cy - size * 0.5f);
                    path.LineTo(cx + size * 0.4f, cy - size * 0.2f);
                    path.LineTo(cx + size * 0.3f, cy + size * 0.4f);
                    path.LineTo(cx, cy + size * 0.6f);
                    path.LineTo(cx - size * 0.3f, cy + size * 0.4f);
                    path.LineTo(cx - size * 0.4f, cy - size * 0.2f);
                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                }
                break;

            case PowerUpType.Mystery:
                // Fragezeichen
                using (var font = new SKFont { Size = size * 1.4f })
                using (var textPaint = new SKPaint { IsAntialias = true, Color = SKColors.White })
                {
                    canvas.DrawText("?", cx, cy + size * 0.25f, SKTextAlign.Center, font, textPaint);
                }
                break;

            case PowerUpType.Kick:
                // Schuh/Boot + Bombe
                using (var path = new SKPath())
                {
                    path.MoveTo(cx - size * 0.5f, cy - size * 0.3f);
                    path.LineTo(cx + size * 0.5f, cy);
                    path.LineTo(cx - size * 0.5f, cy + size * 0.3f);
                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                }
                canvas.DrawCircle(cx + size * 0.3f, cy - size * 0.4f, size * 0.2f, fillPaint);
                break;

            case PowerUpType.LineBomb:
                // Drei Kreise in einer Reihe
                canvas.DrawCircle(cx - size * 0.4f, cy, size * 0.2f, fillPaint);
                canvas.DrawCircle(cx, cy, size * 0.2f, fillPaint);
                canvas.DrawCircle(cx + size * 0.4f, cy, size * 0.2f, fillPaint);
                break;

            case PowerUpType.PowerBomb:
                // Großer Kreis mit Stern
                canvas.DrawCircle(cx, cy, size * 0.4f, fillPaint);
                using (var starPaint = new SKPaint
                       {
                           IsAntialias = true, Style = SKPaintStyle.Stroke,
                           Color = new SKColor(255, 255, 100), StrokeWidth = size * 0.18f
                       })
                {
                    canvas.DrawLine(cx, cy - size * 0.3f, cx, cy + size * 0.3f, starPaint);
                    canvas.DrawLine(cx - size * 0.3f, cy, cx + size * 0.3f, cy, starPaint);
                }
                break;

            case PowerUpType.Skull:
                // Totenkopf (Kreis + Augenhöhlen + Kiefer)
                canvas.DrawCircle(cx, cy - size * 0.1f, size * 0.4f, fillPaint);
                fillPaint.Color = SKColors.Black;
                canvas.DrawCircle(cx - size * 0.15f, cy - size * 0.15f, size * 0.12f, fillPaint);
                canvas.DrawCircle(cx + size * 0.15f, cy - size * 0.15f, size * 0.12f, fillPaint);
                canvas.DrawRect(cx - size * 0.2f, cy + size * 0.15f, size * 0.4f, size * 0.08f, fillPaint);
                fillPaint.Color = SKColors.White;
                break;

            default:
                using (var font = new SKFont { Size = size * 1.4f })
                using (var textPaint = new SKPaint { IsAntialias = true, Color = SKColors.White })
                {
                    canvas.DrawText("?", cx, cy + size * 0.25f, SKTextAlign.Center, font, textPaint);
                }
                break;
        }
    }

    /// <summary>
    /// Farbe pro PowerUp-Typ (identisch zu GameRenderer.GetPowerUpColor).
    /// </summary>
    private static SKColor GetPowerUpColor(PowerUpType type) => type switch
    {
        PowerUpType.BombUp => new SKColor(80, 80, 240),
        PowerUpType.Fire => new SKColor(240, 90, 40),
        PowerUpType.Speed => new SKColor(60, 220, 80),
        PowerUpType.Wallpass => new SKColor(150, 100, 50),
        PowerUpType.Detonator => new SKColor(240, 40, 40),
        PowerUpType.Bombpass => new SKColor(50, 50, 150),
        PowerUpType.Flamepass => new SKColor(240, 190, 40),
        PowerUpType.Mystery => new SKColor(180, 80, 240),
        PowerUpType.Kick => new SKColor(255, 165, 0),
        PowerUpType.LineBomb => new SKColor(0, 180, 255),
        PowerUpType.PowerBomb => new SKColor(255, 50, 50),
        PowerUpType.Skull => new SKColor(100, 0, 100),
        _ => SKColors.White
    };

    // ═══════════════════════════════════════════════════════════════════════
    // BOSS-ICONS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen Boss (vereinfacht, ohne Animationen) für CollectionView.
    /// 5 Boss-Typen mit einzigartigen Formen und Farben aus GameRenderer.Bosses.cs.
    /// </summary>
    public static void DrawBoss(SKCanvas canvas, float cx, float cy, float size, BossType type)
    {
        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };

        switch (type)
        {
            case BossType.StoneGolem: DrawStoneGolemIcon(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case BossType.IceDragon: DrawIceDragonIcon(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case BossType.FireDemon: DrawFireDemonIcon(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case BossType.ShadowMaster: DrawShadowMasterIcon(canvas, cx, cy, size, fillPaint, strokePaint); break;
            case BossType.FinalBoss: DrawFinalBossIcon(canvas, cx, cy, size, fillPaint, strokePaint); break;
        }
    }

    /// <summary>StoneGolem: Grauer Felsblock mit roten Augen, eckig, Risse</summary>
    private static void DrawStoneGolemIcon(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float w = s * 0.45f, h = s * 0.5f;

        // Massiger Fels-Körper
        fill.Color = new SKColor(120, 120, 130);
        canvas.DrawRoundRect(cx - w, cy - h * 0.8f, w * 2, h * 1.6f, 5, 5, fill);

        // Schulter-Blöcke (breiter als Körper)
        fill.Color = new SKColor(100, 100, 110);
        canvas.DrawRoundRect(cx - w * 1.2f, cy - h * 0.5f, w * 0.5f, h * 0.6f, 3, 3, fill);
        canvas.DrawRoundRect(cx + w * 0.7f, cy - h * 0.5f, w * 0.5f, h * 0.6f, 3, 3, fill);

        // Risse im Stein
        stroke.Color = new SKColor(80, 80, 85);
        stroke.StrokeWidth = 1f;
        canvas.DrawLine(cx - w * 0.3f, cy - h * 0.4f, cx - w * 0.1f, cy + h * 0.2f, stroke);
        canvas.DrawLine(cx + w * 0.2f, cy - h * 0.2f, cx + w * 0.4f, cy + h * 0.3f, stroke);

        // Rote leuchtende Augen
        fill.Color = new SKColor(255, 50, 20);
        float eyeY = cy - h * 0.2f;
        float eyeSp = w * 0.4f;
        canvas.DrawOval(cx - eyeSp, eyeY, s * 0.08f, s * 0.05f, fill);
        canvas.DrawOval(cx + eyeSp, eyeY, s * 0.08f, s * 0.05f, fill);
        // Glühender Kern
        fill.Color = new SKColor(255, 200, 100);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.03f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.03f, fill);
    }

    /// <summary>IceDragon: Hellblauer Drache mit Flügel-Andeutung, Eiskristall-Aura</summary>
    private static void DrawIceDragonIcon(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.3f;

        // Flügel-Andeutungen (Dreiecke hinter Körper)
        fill.Color = new SKColor(100, 180, 255, 100);
        using var wingL = new SKPath();
        wingL.MoveTo(cx - r * 0.6f, cy - r * 0.2f);
        wingL.LineTo(cx - r * 1.8f, cy - r * 1.2f);
        wingL.LineTo(cx - r * 0.3f, cy + r * 0.3f);
        wingL.Close();
        canvas.DrawPath(wingL, fill);
        using var wingR = new SKPath();
        wingR.MoveTo(cx + r * 0.6f, cy - r * 0.2f);
        wingR.LineTo(cx + r * 1.8f, cy - r * 1.2f);
        wingR.LineTo(cx + r * 0.3f, cy + r * 0.3f);
        wingR.Close();
        canvas.DrawPath(wingR, fill);

        // Ovaler Drachen-Körper
        fill.Color = new SKColor(140, 210, 255);
        canvas.DrawOval(cx, cy, r, r * 1.1f, fill);

        // Bauch-Highlight
        fill.Color = new SKColor(200, 235, 255, 100);
        canvas.DrawOval(cx, cy + r * 0.2f, r * 0.5f, r * 0.5f, fill);

        // Schnauze (schmaler unten)
        fill.Color = new SKColor(120, 190, 240);
        using var snout = new SKPath();
        snout.MoveTo(cx - r * 0.3f, cy + r * 0.5f);
        snout.LineTo(cx, cy + r * 1.2f);
        snout.LineTo(cx + r * 0.3f, cy + r * 0.5f);
        snout.Close();
        canvas.DrawPath(snout, fill);

        // Leuchtende Eisaugen
        fill.Color = new SKColor(200, 240, 255);
        float eyeY = cy - r * 0.2f;
        float eyeSp = r * 0.4f;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.07f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.07f, fill);
        fill.Color = new SKColor(0, 120, 200);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.035f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.035f, fill);
    }

    /// <summary>FireDemon: Roter Dämon mit Flammen-Aura, Hörner, glühende Augen</summary>
    private static void DrawFireDemonIcon(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.32f;

        // Flammen-Aura hinter dem Körper
        fill.Color = new SKColor(255, 100, 20, 60);
        for (int i = 0; i < 3; i++)
        {
            float flameH = r * (1.6f + i * 0.2f);
            float flameW = r * (0.9f + i * 0.15f);
            float offsetX = (i - 1) * r * 0.3f;
            using var flame = new SKPath();
            flame.MoveTo(cx + offsetX - flameW * 0.5f, cy + r * 0.4f);
            flame.QuadTo(cx + offsetX, cy - flameH, cx + offsetX + flameW * 0.5f, cy + r * 0.4f);
            flame.Close();
            canvas.DrawPath(flame, fill);
        }

        // Hörner
        fill.Color = new SKColor(180, 30, 0);
        using var hL = new SKPath();
        hL.MoveTo(cx - r * 0.4f, cy - r * 0.5f);
        hL.LineTo(cx - r * 0.7f, cy - r * 1.2f);
        hL.LineTo(cx - r * 0.15f, cy - r * 0.4f);
        hL.Close();
        canvas.DrawPath(hL, fill);
        using var hR = new SKPath();
        hR.MoveTo(cx + r * 0.4f, cy - r * 0.5f);
        hR.LineTo(cx + r * 0.7f, cy - r * 1.2f);
        hR.LineTo(cx + r * 0.15f, cy - r * 0.4f);
        hR.Close();
        canvas.DrawPath(hR, fill);

        // Körper
        fill.Color = new SKColor(200, 30, 0);
        canvas.DrawOval(cx, cy, r, r * 1.1f, fill);

        // Glühende Augen
        fill.Color = new SKColor(255, 200, 50);
        float eyeY = cy - r * 0.2f;
        float eyeSp = r * 0.4f;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.08f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.08f, fill);
        fill.Color = new SKColor(255, 50, 0);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.04f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.04f, fill);

        // Breites Maul mit Fangzähnen
        stroke.Color = new SKColor(120, 0, 0);
        stroke.StrokeWidth = 1.2f;
        float my = cy + r * 0.35f;
        canvas.DrawLine(cx - r * 0.4f, my, cx + r * 0.4f, my, stroke);
        fill.Color = SKColors.White;
        canvas.DrawRect(cx - r * 0.25f, my - 1, s * 0.05f, s * 0.07f, fill);
        canvas.DrawRect(cx + r * 0.15f, my - 1, s * 0.05f, s * 0.07f, fill);
    }

    /// <summary>ShadowMaster: Lila Kapuzen-Figur, mysteriöse leuchtende Augen, Schatten-Aura</summary>
    private static void DrawShadowMasterIcon(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float r = s * 0.35f;

        // Schatten-Aura
        fill.Color = new SKColor(100, 0, 180, 40);
        canvas.DrawCircle(cx, cy, r * 1.4f, fill);

        // Kapuzen-Robe (Tropfenform nach unten)
        fill.Color = new SKColor(60, 20, 80);
        using var robe = new SKPath();
        robe.MoveTo(cx, cy - r * 1.1f);
        robe.CubicTo(cx + r * 1.3f, cy - r * 0.3f, cx + r, cy + r * 0.8f, cx + r * 0.4f, cy + r);
        robe.LineTo(cx - r * 0.4f, cy + r);
        robe.CubicTo(cx - r, cy + r * 0.8f, cx - r * 1.3f, cy - r * 0.3f, cx, cy - r * 1.1f);
        robe.Close();
        canvas.DrawPath(robe, fill);

        // Kapuzen-Öffnung (dunkler)
        fill.Color = new SKColor(30, 5, 50);
        canvas.DrawOval(cx, cy - r * 0.15f, r * 0.6f, r * 0.5f, fill);

        // Leuchtende lila Augen in der Kapuze
        fill.Color = new SKColor(200, 100, 255);
        float eyeY = cy - r * 0.25f;
        float eyeSp = r * 0.3f;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.06f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.06f, fill);
        // Heller Kern
        fill.Color = new SKColor(240, 200, 255);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.025f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.025f, fill);
    }

    /// <summary>FinalBoss: Schwarzer Panzer mit Goldkrone, alle Elemente vereint</summary>
    private static void DrawFinalBossIcon(SKCanvas canvas, float cx, float cy, float s,
        SKPaint fill, SKPaint stroke)
    {
        float w = s * 0.42f, h = s * 0.48f;

        // Goldkrone oben
        fill.Color = new SKColor(255, 215, 0);
        using var crown = new SKPath();
        float crownY = cy - h * 0.9f;
        crown.MoveTo(cx - w * 0.6f, crownY + s * 0.12f);
        crown.LineTo(cx - w * 0.6f, crownY);
        crown.LineTo(cx - w * 0.3f, crownY + s * 0.06f);
        crown.LineTo(cx, crownY - s * 0.04f);
        crown.LineTo(cx + w * 0.3f, crownY + s * 0.06f);
        crown.LineTo(cx + w * 0.6f, crownY);
        crown.LineTo(cx + w * 0.6f, crownY + s * 0.12f);
        crown.Close();
        canvas.DrawPath(crown, fill);
        // Kronjuwelen
        fill.Color = new SKColor(255, 50, 50);
        canvas.DrawCircle(cx, crownY + s * 0.03f, s * 0.03f, fill);
        fill.Color = new SKColor(50, 100, 255);
        canvas.DrawCircle(cx - w * 0.3f, crownY + s * 0.06f, s * 0.02f, fill);
        canvas.DrawCircle(cx + w * 0.3f, crownY + s * 0.06f, s * 0.02f, fill);

        // Massiver schwarzer Körper
        fill.Color = new SKColor(30, 30, 40);
        canvas.DrawRoundRect(cx - w, cy - h * 0.6f, w * 2, h * 1.4f, 6, 6, fill);

        // Goldene Rüstungs-Details
        stroke.Color = new SKColor(255, 200, 50);
        stroke.StrokeWidth = 1.5f;
        canvas.DrawLine(cx - w * 0.8f, cy - h * 0.2f, cx + w * 0.8f, cy - h * 0.2f, stroke);
        canvas.DrawLine(cx - w * 0.7f, cy + h * 0.2f, cx + w * 0.7f, cy + h * 0.2f, stroke);

        // Bedrohliche mehrfarbige Augen (Feuer+Eis → Orange+Cyan)
        fill.Color = new SKColor(255, 150, 30);
        float eyeY = cy - h * 0.35f;
        float eyeSp = w * 0.45f;
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.075f, fill);
        fill.Color = new SKColor(0, 200, 255);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.075f, fill);
        // Dunkle Pupillen
        fill.Color = new SKColor(20, 10, 10);
        canvas.DrawCircle(cx - eyeSp, eyeY, s * 0.035f, fill);
        canvas.DrawCircle(cx + eyeSp, eyeY, s * 0.035f, fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOMBEN-KARTEN-ICONS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine Bomben-Karte (farbiger Bomben-Kreis + Typ-Symbol + Zündschnur).
    /// Farben aus GameRenderer.Items.cs.
    /// </summary>
    public static void DrawBombCard(SKCanvas canvas, float cx, float cy, float size, BombType type)
    {
        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };

        var bodyColor = GetBombBodyColor(type);
        float r = size * 0.3f;

        // Bomben-Körper (farbiger Kreis)
        fillPaint.Color = bodyColor;
        canvas.DrawCircle(cx, cy + r * 0.1f, r, fillPaint);

        // Highlight (heller Fleck oben-links)
        fillPaint.Color = new SKColor(255, 255, 255, 60);
        canvas.DrawCircle(cx - r * 0.25f, cy - r * 0.2f, r * 0.3f, fillPaint);

        // Zündschnur
        strokePaint.Color = new SKColor(180, 140, 80);
        strokePaint.StrokeWidth = size * 0.04f;
        float fuseBaseY = cy + r * 0.1f - r;
        using var fusePath = new SKPath();
        fusePath.MoveTo(cx, fuseBaseY);
        fusePath.QuadTo(cx + r * 0.3f, fuseBaseY - r * 0.3f, cx + r * 0.15f, fuseBaseY - r * 0.5f);
        canvas.DrawPath(fusePath, strokePaint);

        // Funke an der Zündschnur-Spitze
        fillPaint.Color = new SKColor(255, 200, 50);
        canvas.DrawCircle(cx + r * 0.15f, fuseBaseY - r * 0.5f, size * 0.04f, fillPaint);
        fillPaint.Color = new SKColor(255, 255, 200);
        canvas.DrawCircle(cx + r * 0.15f, fuseBaseY - r * 0.5f, size * 0.02f, fillPaint);

        // Typ-Symbol im Bomben-Kreis
        DrawBombTypeSymbol(canvas, type, cx, cy + r * 0.1f, r * 0.55f, fillPaint, strokePaint);
    }

    /// <summary>Zeichnet das Typ-Symbol innerhalb des Bomben-Kreises</summary>
    private static void DrawBombTypeSymbol(SKCanvas canvas, BombType type,
        float cx, float cy, float size, SKPaint fill, SKPaint stroke)
    {
        fill.Color = new SKColor(255, 255, 255, 220);
        stroke.Color = new SKColor(255, 255, 255, 220);
        stroke.StrokeWidth = size * 0.15f;

        switch (type)
        {
            case BombType.Normal:
                // Stern-Symbol
                canvas.DrawCircle(cx, cy, size * 0.3f, fill);
                break;

            case BombType.Ice:
                // Schneeflocke (6 Linien)
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * MathF.PI / 3f;
                    float dx = MathF.Cos(angle) * size * 0.6f;
                    float dy = MathF.Sin(angle) * size * 0.6f;
                    canvas.DrawLine(cx - dx, cy - dy, cx + dx, cy + dy, stroke);
                }
                break;

            case BombType.Fire:
                // Flamme (Dreieck)
                using (var flame = new SKPath())
                {
                    flame.MoveTo(cx, cy - size * 0.6f);
                    flame.LineTo(cx + size * 0.35f, cy + size * 0.4f);
                    flame.LineTo(cx - size * 0.35f, cy + size * 0.4f);
                    flame.Close();
                    canvas.DrawPath(flame, fill);
                }
                break;

            case BombType.Sticky:
                // Tropfen
                using (var drop = new SKPath())
                {
                    drop.MoveTo(cx, cy - size * 0.5f);
                    drop.QuadTo(cx + size * 0.4f, cy, cx, cy + size * 0.4f);
                    drop.QuadTo(cx - size * 0.4f, cy, cx, cy - size * 0.5f);
                    drop.Close();
                    canvas.DrawPath(drop, fill);
                }
                break;

            case BombType.Smoke:
                // Wolke (3 Kreise)
                canvas.DrawCircle(cx - size * 0.2f, cy, size * 0.25f, fill);
                canvas.DrawCircle(cx + size * 0.2f, cy, size * 0.25f, fill);
                canvas.DrawCircle(cx, cy - size * 0.15f, size * 0.3f, fill);
                break;

            case BombType.Lightning:
                // Blitz-Zickzack
                using (var bolt = new SKPath())
                {
                    bolt.MoveTo(cx + size * 0.1f, cy - size * 0.55f);
                    bolt.LineTo(cx - size * 0.15f, cy);
                    bolt.LineTo(cx + size * 0.1f, cy);
                    bolt.LineTo(cx - size * 0.1f, cy + size * 0.55f);
                    canvas.DrawPath(bolt, stroke);
                }
                break;

            case BombType.Gravity:
                // Magnet-Symbol (U-Form)
                stroke.StrokeWidth = size * 0.12f;
                using (var magnet = new SKPath())
                {
                    magnet.MoveTo(cx - size * 0.35f, cy - size * 0.3f);
                    magnet.LineTo(cx - size * 0.35f, cy + size * 0.1f);
                    magnet.ArcTo(new SKRect(cx - size * 0.35f, cy - size * 0.1f, cx + size * 0.35f, cy + size * 0.5f),
                        180, -180, false);
                    magnet.LineTo(cx + size * 0.35f, cy - size * 0.3f);
                    canvas.DrawPath(magnet, stroke);
                }
                break;

            case BombType.Poison:
                // Totenkopf (klein)
                canvas.DrawCircle(cx, cy - size * 0.1f, size * 0.3f, fill);
                fill.Color = GetBombBodyColor(type);
                canvas.DrawCircle(cx - size * 0.12f, cy - size * 0.15f, size * 0.09f, fill);
                canvas.DrawCircle(cx + size * 0.12f, cy - size * 0.15f, size * 0.09f, fill);
                fill.Color = new SKColor(255, 255, 255, 220);
                break;

            case BombType.TimeWarp:
                // Uhr-Symbol (Kreis + Zeiger)
                stroke.StrokeWidth = size * 0.1f;
                canvas.DrawCircle(cx, cy, size * 0.35f, stroke);
                canvas.DrawLine(cx, cy, cx, cy - size * 0.25f, stroke);
                canvas.DrawLine(cx, cy, cx + size * 0.18f, cy + size * 0.05f, stroke);
                break;

            case BombType.Mirror:
                // Spiegel-Pfeile (links-rechts)
                using (var arrL = new SKPath())
                {
                    arrL.MoveTo(cx - size * 0.1f, cy);
                    arrL.LineTo(cx - size * 0.5f, cy);
                    arrL.LineTo(cx - size * 0.3f, cy - size * 0.2f);
                    arrL.MoveTo(cx - size * 0.5f, cy);
                    arrL.LineTo(cx - size * 0.3f, cy + size * 0.2f);
                    canvas.DrawPath(arrL, stroke);
                }
                using (var arrR = new SKPath())
                {
                    arrR.MoveTo(cx + size * 0.1f, cy);
                    arrR.LineTo(cx + size * 0.5f, cy);
                    arrR.LineTo(cx + size * 0.3f, cy - size * 0.2f);
                    arrR.MoveTo(cx + size * 0.5f, cy);
                    arrR.LineTo(cx + size * 0.3f, cy + size * 0.2f);
                    canvas.DrawPath(arrR, stroke);
                }
                break;

            case BombType.Vortex:
                // Spirale
                stroke.StrokeWidth = size * 0.1f;
                using (var spiral = new SKPath())
                {
                    for (int i = 0; i < 20; i++)
                    {
                        float t = i / 20f * MathF.PI * 3f;
                        float sr = size * 0.05f + size * 0.45f * (i / 20f);
                        float sx = cx + MathF.Cos(t) * sr;
                        float sy = cy + MathF.Sin(t) * sr;
                        if (i == 0) spiral.MoveTo(sx, sy);
                        else spiral.LineTo(sx, sy);
                    }
                    canvas.DrawPath(spiral, stroke);
                }
                break;

            case BombType.Phantom:
                // Geist-Symbol (transparenter Kreis mit Augen)
                fill.Color = new SKColor(255, 255, 255, 150);
                canvas.DrawCircle(cx, cy - size * 0.1f, size * 0.3f, fill);
                canvas.DrawRect(cx - size * 0.3f, cy, size * 0.6f, size * 0.25f, fill);
                fill.Color = GetBombBodyColor(type);
                canvas.DrawCircle(cx - size * 0.12f, cy - size * 0.12f, size * 0.07f, fill);
                canvas.DrawCircle(cx + size * 0.12f, cy - size * 0.12f, size * 0.07f, fill);
                fill.Color = new SKColor(255, 255, 255, 220);
                break;

            case BombType.Nova:
                // Stern (8 Strahlen)
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * MathF.PI / 4f;
                    float dx = MathF.Cos(angle) * size * 0.5f;
                    float dy = MathF.Sin(angle) * size * 0.5f;
                    stroke.StrokeWidth = (i % 2 == 0) ? size * 0.12f : size * 0.08f;
                    canvas.DrawLine(cx, cy, cx + dx, cy + dy, stroke);
                }
                canvas.DrawCircle(cx, cy, size * 0.15f, fill);
                break;

            case BombType.BlackHole:
                // Schwarzes Loch (konzentrische Kreise, dunkler Kern)
                fill.Color = new SKColor(100, 0, 200, 100);
                canvas.DrawCircle(cx, cy, size * 0.5f, fill);
                fill.Color = new SKColor(60, 0, 120, 150);
                canvas.DrawCircle(cx, cy, size * 0.3f, fill);
                fill.Color = new SKColor(20, 0, 40);
                canvas.DrawCircle(cx, cy, size * 0.15f, fill);
                break;
        }
    }

    /// <summary>Farb-Mapping für Bomben-Körper (aus GameRenderer.Items.cs)</summary>
    private static SKColor GetBombBodyColor(BombType type) => type switch
    {
        BombType.Ice => new SKColor(100, 200, 255),
        BombType.Fire => new SKColor(200, 30, 0),
        BombType.Sticky => new SKColor(50, 180, 50),
        BombType.Smoke => new SKColor(140, 140, 140),
        BombType.Lightning => new SKColor(255, 255, 100),
        BombType.Gravity => new SKColor(150, 80, 220),
        BombType.Poison => new SKColor(0, 180, 0),
        BombType.TimeWarp => new SKColor(80, 130, 255),
        BombType.Mirror => new SKColor(200, 200, 230),
        BombType.Vortex => new SKColor(130, 0, 200),
        BombType.Phantom => new SKColor(180, 200, 240),
        BombType.Nova => new SKColor(255, 200, 0),
        BombType.BlackHole => new SKColor(40, 0, 60),
        _ => new SKColor(60, 60, 60) // Normal
    };
}
