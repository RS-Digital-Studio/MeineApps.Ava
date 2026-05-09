using BomberBlast.Models.Cosmetics;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Prozeduraler Profilrahmen-Renderer (Phase 29c — AAA-Audit Cosmetic-Integration).
///
/// <para>Rendert alle 33 <see cref="FrameStyle"/>-Werte rein per SkiaSharp — keine externen Bitmaps.
/// Wird im Profile-Hub, Liga-Leaderboard, GameOver-Screen genutzt.</para>
///
/// <para>Pattern: Statische Helper-Methoden mit gepoolten <see cref="SKPaint"/>-Objekten.
/// Ein Aufruf <see cref="DrawFrame"/> rendert den gewünschten Frame um eine Avatar-Box.</para>
/// </summary>
public static class FrameRenderer
{
    private static readonly SKPaint _strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
    };

    private static readonly SKPaint _fillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    private static readonly SKMaskFilter _softGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);

    /// <summary>
    /// Zeichnet den Frame um <paramref name="bounds"/> herum.
    /// </summary>
    /// <param name="canvas">Ziel-Canvas.</param>
    /// <param name="bounds">Avatar-Box (Frame wird drumherum gerendert).</param>
    /// <param name="def">Frame-Definition (Style + Farben).</param>
    /// <param name="time">Globaler Zeit-Akkumulator für Animationen.</param>
    public static void DrawFrame(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time = 0f)
    {
        switch (def.Style)
        {
            // === Common ===
            case FrameStyle.Simple: DrawSimple(canvas, bounds, def); break;
            case FrameStyle.Rounded: DrawRounded(canvas, bounds, def); break;
            case FrameStyle.Square: DrawSquare(canvas, bounds, def); break;
            case FrameStyle.Dotted: DrawDotted(canvas, bounds, def); break;
            case FrameStyle.Thin: DrawThin(canvas, bounds, def); break;

            // === Rare ===
            case FrameStyle.FireFrame: DrawFlamePattern(canvas, bounds, def, time); break;
            case FrameStyle.IceFrame: DrawIcePattern(canvas, bounds, def, time); break;
            case FrameStyle.ElectricFrame: DrawElectricPattern(canvas, bounds, def, time); break;
            case FrameStyle.NatureFrame: DrawVinePattern(canvas, bounds, def, time); break;
            case FrameStyle.WaterFrame: DrawWavePattern(canvas, bounds, def, time); break;

            // === Epic ===
            case FrameStyle.ShadowFrame: DrawShadowPattern(canvas, bounds, def, time); break;
            case FrameStyle.CrystalFrame: DrawCrystalPattern(canvas, bounds, def); break;
            case FrameStyle.PlasmaFrame: DrawPlasmaPattern(canvas, bounds, def, time); break;
            case FrameStyle.StellarFrame: DrawStellarPattern(canvas, bounds, def, time); break;
            case FrameStyle.ArcaneFrame: DrawArcanePattern(canvas, bounds, def); break;

            // === Legendary ===
            case FrameStyle.DragonFrame: DrawDragonScales(canvas, bounds, def); break;
            case FrameStyle.PhoenixFrame: DrawPhoenixFeathers(canvas, bounds, def, time); break;
            case FrameStyle.CrownFrame: DrawCrownGems(canvas, bounds, def, time); break;

            // === Phase 29 — Welt-thematische ===
            case FrameStyle.PumpkinFrame: DrawPumpkinSpikes(canvas, bounds, def); break;
            case FrameStyle.SnowflakeFrame: DrawSnowflakeIcicles(canvas, bounds, def, time); break;
            case FrameStyle.CherryFrame: DrawCherryBlossomBorder(canvas, bounds, def, time); break;
            case FrameStyle.SteampunkFrame: DrawSteampunkGears(canvas, bounds, def, time); break;
            case FrameStyle.NeonFrame: DrawNeonGlitch(canvas, bounds, def, time); break;
            case FrameStyle.BoneFrame: DrawBoneBorder(canvas, bounds, def); break;
            case FrameStyle.OceanFrame: DrawOceanWaves(canvas, bounds, def, time); break;
            case FrameStyle.SamuraiFrame: DrawSamuraiSwords(canvas, bounds, def); break;
            case FrameStyle.MechFrame: DrawMechRivets(canvas, bounds, def, time); break;
            case FrameStyle.BeachFrame: DrawBeachShells(canvas, bounds, def); break;

            // === Phase 29 — Karriere-Status ===
            case FrameStyle.DiamondFrame: DrawDiamondCascade(canvas, bounds, def, time); break;
            case FrameStyle.MasterFrame: DrawMasterStars(canvas, bounds, def, time); break;
            case FrameStyle.AscensionFrame: DrawAscensionRunes(canvas, bounds, def, time); break;
            case FrameStyle.BPFrame: DrawBPGold(canvas, bounds, def, time); break;
            case FrameStyle.SeasonFrame: DrawSeasonStripes(canvas, bounds, def, time); break;

            default: DrawSimple(canvas, bounds, def); break;
        }
    }

    // === Common ===========================================================

    private static void DrawSimple(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
    }

    private static void DrawRounded(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 12f, 12f, _strokePaint);
    }

    private static void DrawSquare(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Eckmarkierungen
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.SecondaryColor;
        const float c = 8f;
        canvas.DrawLine(bounds.Left, bounds.Top + c, bounds.Left + c, bounds.Top, _strokePaint);
        canvas.DrawLine(bounds.Right - c, bounds.Top, bounds.Right, bounds.Top + c, _strokePaint);
        canvas.DrawLine(bounds.Left, bounds.Bottom - c, bounds.Left + c, bounds.Bottom, _strokePaint);
        canvas.DrawLine(bounds.Right - c, bounds.Bottom, bounds.Right, bounds.Bottom - c, _strokePaint);
    }

    private static void DrawDotted(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _fillPaint.Color = def.PrimaryColor;
        const float spacing = 8f;
        for (float x = bounds.Left; x <= bounds.Right; x += spacing)
        {
            canvas.DrawCircle(x, bounds.Top, 1.5f, _fillPaint);
            canvas.DrawCircle(x, bounds.Bottom, 1.5f, _fillPaint);
        }
        for (float y = bounds.Top; y <= bounds.Bottom; y += spacing)
        {
            canvas.DrawCircle(bounds.Left, y, 1.5f, _fillPaint);
            canvas.DrawCircle(bounds.Right, y, 1.5f, _fillPaint);
        }
    }

    private static void DrawThin(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
    }

    // === Rare =============================================================

    private static void DrawFlamePattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Flammen-Spitzen oben
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        for (int i = 0; i < 8; i++)
        {
            var x = bounds.Left + (bounds.Width / 8) * i + (bounds.Width / 16);
            var flicker = MathF.Sin(time * 8f + i) * 3f;
            using var p = new SKPath();
            p.MoveTo(x, bounds.Top + 2);
            p.LineTo(x - 4, bounds.Top - 6 + flicker);
            p.LineTo(x + 4, bounds.Top - 6 + flicker);
            p.Close();
            canvas.DrawPath(p, _fillPaint);
        }
        _strokePaint.MaskFilter = null;
        _fillPaint.MaskFilter = null;
    }

    private static void DrawIcePattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Eis-Kristalle in den Ecken
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.Color = def.SecondaryColor;
        DrawSnowflakeAt(canvas, bounds.Left, bounds.Top, 6f);
        DrawSnowflakeAt(canvas, bounds.Right, bounds.Top, 6f);
        DrawSnowflakeAt(canvas, bounds.Left, bounds.Bottom, 6f);
        DrawSnowflakeAt(canvas, bounds.Right, bounds.Bottom, 6f);
    }

    private static void DrawSnowflakeAt(SKCanvas canvas, float cx, float cy, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            var ang = i * MathF.PI / 3f;
            canvas.DrawLine(cx, cy, cx + MathF.Cos(ang) * radius, cy + MathF.Sin(ang) * radius, _strokePaint);
        }
    }

    private static void DrawElectricPattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        _strokePaint.MaskFilter = null;
        // Zickzack-Blitze auf den Seiten
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.Color = def.SecondaryColor;
        var phase = (time * 3f) % 1f;
        for (int side = 0; side < 4; side++)
        {
            DrawZigzagLine(canvas, bounds, side, phase);
        }
    }

    private static void DrawZigzagLine(SKCanvas canvas, SKRect b, int side, float phase)
    {
        // Sehr einfach: kleine Spikes auf jeder Seite
        const int spikes = 4;
        for (int i = 0; i < spikes; i++)
        {
            var t = (i + phase) / spikes;
            float x = 0, y = 0, dx = 0, dy = 0;
            switch (side)
            {
                case 0: x = b.Left + b.Width * t; y = b.Top; dx = 0; dy = -3; break;
                case 1: x = b.Right; y = b.Top + b.Height * t; dx = 3; dy = 0; break;
                case 2: x = b.Left + b.Width * t; y = b.Bottom; dx = 0; dy = 3; break;
                case 3: x = b.Left; y = b.Top + b.Height * t; dx = -3; dy = 0; break;
            }
            canvas.DrawLine(x, y, x + dx, y + dy, _strokePaint);
        }
    }

    private static void DrawVinePattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 8f, 8f, _strokePaint);
        // Blätter in den Ecken
        _fillPaint.Color = def.SecondaryColor;
        var sway = MathF.Sin(time * 1.5f) * 2f;
        DrawLeaf(canvas, bounds.Left + 4, bounds.Top + 4, 6f, sway);
        DrawLeaf(canvas, bounds.Right - 4, bounds.Top + 4, 6f, -sway);
        DrawLeaf(canvas, bounds.Left + 4, bounds.Bottom - 4, 6f, -sway);
        DrawLeaf(canvas, bounds.Right - 4, bounds.Bottom - 4, 6f, sway);
    }

    private static void DrawLeaf(SKCanvas canvas, float cx, float cy, float r, float sway)
    {
        canvas.Save();
        canvas.RotateDegrees(sway * 5, cx, cy);
        canvas.DrawOval(cx, cy, r, r * 0.6f, _fillPaint);
        canvas.Restore();
    }

    private static void DrawWavePattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Wellen unten
        _strokePaint.Color = def.SecondaryColor;
        using var p = new SKPath();
        p.MoveTo(bounds.Left, bounds.Bottom);
        for (float x = bounds.Left; x <= bounds.Right; x += 8f)
        {
            var w = MathF.Sin((x + time * 30f) * 0.15f) * 2f;
            p.LineTo(x, bounds.Bottom + w);
        }
        canvas.DrawPath(p, _strokePaint);
    }

    // === Epic =============================================================

    private static void DrawShadowPattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 4f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 6f, 6f, _strokePaint);
        // Schatten-Wisps
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        for (int i = 0; i < 6; i++)
        {
            var ang = i * MathF.PI / 3f + time;
            var x = bounds.MidX + MathF.Cos(ang) * (bounds.Width / 2f - 4);
            var y = bounds.MidY + MathF.Sin(ang) * (bounds.Height / 2f - 4);
            canvas.DrawCircle(x, y, 3f, _fillPaint);
        }
        _strokePaint.MaskFilter = null;
        _fillPaint.MaskFilter = null;
    }

    private static void DrawCrystalPattern(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Diagonale Facetten in Ecken
        _strokePaint.Color = def.SecondaryColor;
        _strokePaint.StrokeWidth = 1f;
        const float c = 12f;
        canvas.DrawLine(bounds.Left, bounds.Top + c, bounds.Left + c, bounds.Top, _strokePaint);
        canvas.DrawLine(bounds.Left + c / 2, bounds.Top, bounds.Left + c, bounds.Top + c / 2, _strokePaint);
        canvas.DrawLine(bounds.Right - c, bounds.Top, bounds.Right, bounds.Top + c, _strokePaint);
        canvas.DrawLine(bounds.Right, bounds.Top + c / 2, bounds.Right - c / 2, bounds.Top, _strokePaint);
    }

    private static void DrawPlasmaPattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.MaskFilter = _softGlow;
        var pulse = 0.5f + MathF.Sin(time * 4f) * 0.3f;
        _strokePaint.Color = def.PrimaryColor.WithAlpha((byte)(255 * pulse));
        canvas.DrawRoundRect(bounds, 10f, 10f, _strokePaint);
        _strokePaint.Color = def.SecondaryColor.WithAlpha((byte)(255 * (1f - pulse)));
        canvas.DrawRoundRect(bounds, 14f, 14f, _strokePaint);
        _strokePaint.MaskFilter = null;
    }

    private static void DrawStellarPattern(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Sterne in den Ecken
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        var twinkle = 0.6f + MathF.Sin(time * 5f) * 0.4f;
        DrawStar(canvas, bounds.Left, bounds.Top, 5f * twinkle);
        DrawStar(canvas, bounds.Right, bounds.Top, 5f * twinkle);
        DrawStar(canvas, bounds.Left, bounds.Bottom, 5f * twinkle);
        DrawStar(canvas, bounds.Right, bounds.Bottom, 5f * twinkle);
        _fillPaint.MaskFilter = null;
    }

    private static void DrawStar(SKCanvas canvas, float cx, float cy, float r)
    {
        using var p = new SKPath();
        for (int i = 0; i < 10; i++)
        {
            var ang = i * MathF.PI / 5f - MathF.PI / 2f;
            var rad = i % 2 == 0 ? r : r * 0.45f;
            var x = cx + MathF.Cos(ang) * rad;
            var y = cy + MathF.Sin(ang) * rad;
            if (i == 0) p.MoveTo(x, y); else p.LineTo(x, y);
        }
        p.Close();
        canvas.DrawPath(p, _fillPaint);
    }

    private static void DrawArcanePattern(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Runen-Symbole (geometrische Glyphen) auf jeder Seite
        _strokePaint.Color = def.SecondaryColor;
        _strokePaint.StrokeWidth = 1.5f;
        DrawRune(canvas, bounds.MidX, bounds.Top - 6, 4f, 0);
        DrawRune(canvas, bounds.MidX, bounds.Bottom + 6, 4f, 1);
        DrawRune(canvas, bounds.Left - 6, bounds.MidY, 4f, 2);
        DrawRune(canvas, bounds.Right + 6, bounds.MidY, 4f, 3);
    }

    private static void DrawRune(SKCanvas canvas, float cx, float cy, float r, int variant)
    {
        switch (variant)
        {
            case 0: canvas.DrawLine(cx - r, cy, cx + r, cy, _strokePaint);
                    canvas.DrawLine(cx, cy - r, cx, cy + r, _strokePaint); break;
            case 1: canvas.DrawCircle(cx, cy, r * 0.6f, _strokePaint); break;
            case 2: canvas.DrawLine(cx - r, cy - r, cx + r, cy + r, _strokePaint);
                    canvas.DrawLine(cx + r, cy - r, cx - r, cy + r, _strokePaint); break;
            case 3: using (var p = new SKPath())
                    {
                        p.MoveTo(cx, cy - r); p.LineTo(cx + r, cy); p.LineTo(cx, cy + r); p.LineTo(cx - r, cy); p.Close();
                        canvas.DrawPath(p, _strokePaint);
                    }
                    break;
        }
    }

    // === Legendary ========================================================

    private static void DrawDragonScales(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 4f, 4f, _strokePaint);
        // Schuppen-Pattern auf der Oberseite
        _fillPaint.Color = def.SecondaryColor;
        const float scaleR = 4f;
        for (float x = bounds.Left; x <= bounds.Right; x += scaleR * 1.5f)
        {
            canvas.DrawCircle(x, bounds.Top, scaleR, _fillPaint);
            canvas.DrawCircle(x, bounds.Bottom, scaleR, _fillPaint);
        }
        // Hörner in den Ecken
        using var horn = new SKPath();
        horn.MoveTo(bounds.Left, bounds.Top);
        horn.LineTo(bounds.Left - 6, bounds.Top - 8);
        horn.LineTo(bounds.Left + 4, bounds.Top - 4);
        horn.Close();
        canvas.DrawPath(horn, _fillPaint);
        using var horn2 = new SKPath();
        horn2.MoveTo(bounds.Right, bounds.Top);
        horn2.LineTo(bounds.Right + 6, bounds.Top - 8);
        horn2.LineTo(bounds.Right - 4, bounds.Top - 4);
        horn2.Close();
        canvas.DrawPath(horn2, _fillPaint);
    }

    private static void DrawPhoenixFeathers(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 8f, 8f, _strokePaint);
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        // Federn als Tropfen-Form um den Frame
        for (int i = 0; i < 8; i++)
        {
            var ang = i * MathF.PI / 4f + time * 0.3f;
            var x = bounds.MidX + MathF.Cos(ang) * (bounds.Width / 2f + 6);
            var y = bounds.MidY + MathF.Sin(ang) * (bounds.Height / 2f + 6);
            canvas.DrawOval(x, y, 4f, 8f, _fillPaint);
        }
        _strokePaint.MaskFilter = null;
        _fillPaint.MaskFilter = null;
    }

    private static void DrawCrownGems(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 4f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 6f, 6f, _strokePaint);
        // Krone oben (3 Spitzen mit Edelsteinen)
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        var pulse = 0.7f + MathF.Sin(time * 3f) * 0.3f;
        var cx = bounds.MidX;
        DrawGem(canvas, cx - 12, bounds.Top - 4, 3f * pulse);
        DrawGem(canvas, cx, bounds.Top - 8, 4f * pulse);
        DrawGem(canvas, cx + 12, bounds.Top - 4, 3f * pulse);
        _strokePaint.MaskFilter = null;
        _fillPaint.MaskFilter = null;
    }

    private static void DrawGem(SKCanvas canvas, float cx, float cy, float r)
    {
        using var p = new SKPath();
        p.MoveTo(cx, cy - r);
        p.LineTo(cx + r, cy);
        p.LineTo(cx, cy + r);
        p.LineTo(cx - r, cy);
        p.Close();
        canvas.DrawPath(p, _fillPaint);
    }

    // === Phase 29 — Welt-thematische ======================================

    private static void DrawPumpkinSpikes(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        _fillPaint.Color = def.SecondaryColor;
        // Stacheln entlang der Oberseite
        for (int i = 0; i < 10; i++)
        {
            var x = bounds.Left + (bounds.Width / 10) * i + 4;
            using var p = new SKPath();
            p.MoveTo(x, bounds.Top);
            p.LineTo(x - 3, bounds.Top - 6);
            p.LineTo(x + 3, bounds.Top - 6);
            p.Close();
            canvas.DrawPath(p, _fillPaint);
        }
    }

    private static void DrawSnowflakeIcicles(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Eiszapfen unten
        _fillPaint.Color = def.SecondaryColor;
        for (int i = 0; i < 8; i++)
        {
            var x = bounds.Left + (bounds.Width / 8) * i + 4;
            var len = 3f + MathF.Sin(time * 2f + i) * 1f + (i % 3) * 2f;
            using var p = new SKPath();
            p.MoveTo(x - 2, bounds.Bottom);
            p.LineTo(x + 2, bounds.Bottom);
            p.LineTo(x, bounds.Bottom + len);
            p.Close();
            canvas.DrawPath(p, _fillPaint);
        }
    }

    private static void DrawCherryBlossomBorder(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 6f, 6f, _strokePaint);
        _fillPaint.Color = def.SecondaryColor;
        // 5-Blüten in den Ecken
        for (int corner = 0; corner < 4; corner++)
        {
            var cx = (corner & 1) == 0 ? bounds.Left : bounds.Right;
            var cy = (corner & 2) == 0 ? bounds.Top : bounds.Bottom;
            for (int p = 0; p < 5; p++)
            {
                var ang = p * MathF.PI * 2 / 5 + time * 0.5f;
                canvas.DrawCircle(cx + MathF.Cos(ang) * 3, cy + MathF.Sin(ang) * 3, 2f, _fillPaint);
            }
        }
    }

    private static void DrawSteampunkGears(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Zahnräder in den Ecken
        _strokePaint.Color = def.SecondaryColor;
        DrawGear(canvas, bounds.Left, bounds.Top, 5f, time);
        DrawGear(canvas, bounds.Right, bounds.Top, 5f, -time);
        DrawGear(canvas, bounds.Left, bounds.Bottom, 5f, -time);
        DrawGear(canvas, bounds.Right, bounds.Bottom, 5f, time);
    }

    private static void DrawGear(SKCanvas canvas, float cx, float cy, float r, float rotation)
    {
        canvas.Save();
        canvas.RotateDegrees(rotation * 30f, cx, cy);
        for (int t = 0; t < 8; t++)
        {
            var ang = t * MathF.PI / 4f;
            var x1 = cx + MathF.Cos(ang) * r;
            var y1 = cy + MathF.Sin(ang) * r;
            var x2 = cx + MathF.Cos(ang) * (r + 2);
            var y2 = cy + MathF.Sin(ang) * (r + 2);
            canvas.DrawLine(x1, y1, x2, y2, _strokePaint);
        }
        canvas.DrawCircle(cx, cy, r, _strokePaint);
        canvas.Restore();
    }

    private static void DrawNeonGlitch(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = _softGlow;
        var glitchOffset = MathF.Sin(time * 20f) * 1.5f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds.Left + glitchOffset, bounds.Top, bounds.Width, bounds.Height, _strokePaint);
        _strokePaint.Color = def.SecondaryColor;
        canvas.DrawRect(bounds.Left - glitchOffset, bounds.Top, bounds.Width, bounds.Height, _strokePaint);
        _strokePaint.MaskFilter = null;
    }

    private static void DrawBoneBorder(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Knochen-Form in der Ecke (kleine Symbole)
        _fillPaint.Color = def.SecondaryColor;
        DrawBone(canvas, bounds.Left + 6, bounds.Top - 4);
        DrawBone(canvas, bounds.Right - 6, bounds.Top - 4);
    }

    private static void DrawBone(SKCanvas canvas, float cx, float cy)
    {
        canvas.DrawCircle(cx - 3, cy, 2f, _fillPaint);
        canvas.DrawRect(cx - 2, cy - 1, 4, 2, _fillPaint);
        canvas.DrawCircle(cx + 3, cy, 2f, _fillPaint);
    }

    private static void DrawOceanWaves(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 8f, 8f, _strokePaint);
        // Wellen-Pattern in der Mitte (oben + unten)
        _strokePaint.Color = def.SecondaryColor;
        _strokePaint.StrokeWidth = 1.5f;
        using var top = new SKPath();
        using var bot = new SKPath();
        top.MoveTo(bounds.Left, bounds.Top);
        bot.MoveTo(bounds.Left, bounds.Bottom);
        for (float x = bounds.Left; x <= bounds.Right; x += 6f)
        {
            var w = MathF.Sin((x + time * 20f) * 0.2f) * 2f;
            top.LineTo(x, bounds.Top + w);
            bot.LineTo(x, bounds.Bottom - w);
        }
        canvas.DrawPath(top, _strokePaint);
        canvas.DrawPath(bot, _strokePaint);
    }

    private static void DrawSamuraiSwords(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Schwerter X auf der Oberseite
        _strokePaint.Color = def.SecondaryColor;
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawLine(bounds.MidX - 8, bounds.Top - 6, bounds.MidX + 8, bounds.Top + 2, _strokePaint);
        canvas.DrawLine(bounds.MidX + 8, bounds.Top - 6, bounds.MidX - 8, bounds.Top + 2, _strokePaint);
    }

    private static void DrawMechRivets(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // LED-Lichter pulsierend
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        var pulse = 0.6f + MathF.Sin(time * 3f) * 0.4f;
        var alpha = (byte)(255 * pulse);
        _fillPaint.Color = def.SecondaryColor.WithAlpha(alpha);
        canvas.DrawCircle(bounds.Left, bounds.Top, 2f, _fillPaint);
        canvas.DrawCircle(bounds.Right, bounds.Top, 2f, _fillPaint);
        canvas.DrawCircle(bounds.Left, bounds.Bottom, 2f, _fillPaint);
        canvas.DrawCircle(bounds.Right, bounds.Bottom, 2f, _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    private static void DrawBeachShells(SKCanvas canvas, SKRect bounds, FrameDefinition def)
    {
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 8f, 8f, _strokePaint);
        // Muscheln in den Ecken (Halbkreise)
        _fillPaint.Color = def.SecondaryColor;
        canvas.DrawArc(new SKRect(bounds.Left - 4, bounds.Top - 4, bounds.Left + 4, bounds.Top + 4), 0, 180, true, _fillPaint);
        canvas.DrawArc(new SKRect(bounds.Right - 4, bounds.Top - 4, bounds.Right + 4, bounds.Top + 4), 0, 180, true, _fillPaint);
    }

    // === Phase 29 — Karriere-Status ========================================

    private static void DrawDiamondCascade(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 4f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 6f, 6f, _strokePaint);
        // Diamanten-Kaskade
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        for (int i = 0; i < 6; i++)
        {
            var angle = i * MathF.PI / 3f + time * 0.5f;
            var x = bounds.MidX + MathF.Cos(angle) * (bounds.Width / 2f + 4);
            var y = bounds.MidY + MathF.Sin(angle) * (bounds.Height / 2f + 4);
            DrawGem(canvas, x, y, 3.5f);
        }
        _strokePaint.MaskFilter = null;
        _fillPaint.MaskFilter = null;
    }

    private static void DrawMasterStars(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 4f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        var twinkle = 0.7f + MathF.Sin(time * 4f) * 0.3f;
        DrawStar(canvas, bounds.MidX, bounds.Top - 6, 5f * twinkle);
        DrawStar(canvas, bounds.Left - 6, bounds.MidY, 4f * twinkle);
        DrawStar(canvas, bounds.Right + 6, bounds.MidY, 4f * twinkle);
        DrawStar(canvas, bounds.MidX, bounds.Bottom + 6, 5f * twinkle);
        _strokePaint.MaskFilter = null;
        _fillPaint.MaskFilter = null;
    }

    private static void DrawAscensionRunes(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 8f, 8f, _strokePaint);
        // Aufsteigende Runen-Symbole entlang der Seiten
        _strokePaint.Color = def.SecondaryColor;
        _strokePaint.StrokeWidth = 1.5f;
        var phase = (time % 1f);
        for (int i = 0; i < 4; i++)
        {
            var t = (i + phase) / 4;
            DrawRune(canvas, bounds.Left - 6, bounds.Top + bounds.Height * t, 3f, i % 4);
            DrawRune(canvas, bounds.Right + 6, bounds.Top + bounds.Height * t, 3f, i % 4);
        }
        _strokePaint.MaskFilter = null;
    }

    private static void DrawBPGold(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 4f;
        _strokePaint.MaskFilter = _softGlow;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRoundRect(bounds, 8f, 8f, _strokePaint);
        // Gold-Krone groß auf der Oberseite
        _fillPaint.Color = def.SecondaryColor;
        _fillPaint.MaskFilter = _softGlow;
        var pulse = 0.8f + MathF.Sin(time * 2.5f) * 0.2f;
        using var crown = new SKPath();
        var cx = bounds.MidX;
        var cy = bounds.Top - 4;
        crown.MoveTo(cx - 16, cy + 4);
        crown.LineTo(cx - 12, cy - 6 * pulse);
        crown.LineTo(cx - 6, cy);
        crown.LineTo(cx, cy - 10 * pulse);
        crown.LineTo(cx + 6, cy);
        crown.LineTo(cx + 12, cy - 6 * pulse);
        crown.LineTo(cx + 16, cy + 4);
        crown.Close();
        canvas.DrawPath(crown, _fillPaint);
        _strokePaint.MaskFilter = null;
        _fillPaint.MaskFilter = null;
    }

    private static void DrawSeasonStripes(SKCanvas canvas, SKRect bounds, FrameDefinition def, float time)
    {
        _strokePaint.StrokeWidth = 3f;
        _strokePaint.Color = def.PrimaryColor;
        canvas.DrawRect(bounds, _strokePaint);
        // Farbige Streifen oben + unten
        _strokePaint.StrokeWidth = 2f;
        for (int i = 0; i < 5; i++)
        {
            var alpha = (byte)(255 * (0.4f + MathF.Sin(time * 2f + i) * 0.3f));
            _strokePaint.Color = (i % 2 == 0 ? def.PrimaryColor : def.SecondaryColor).WithAlpha(alpha);
            var y1 = bounds.Top - 2 - i;
            canvas.DrawLine(bounds.Left, y1, bounds.Right, y1, _strokePaint);
            var y2 = bounds.Bottom + 2 + i;
            canvas.DrawLine(bounds.Left, y2, bounds.Right, y2, _strokePaint);
        }
    }
}
