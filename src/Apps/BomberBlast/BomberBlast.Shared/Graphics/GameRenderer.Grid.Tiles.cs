using BomberBlast.Models;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

// v2.0.37 (Plan Task 2.1): Aus GameRenderer.Grid.cs extrahiert.
// Enthaelt Floor-Tile + Wall-Tile + welt-spezifische Mechanik-Tiles (Ice/Conveyor/Teleporter/LavaCrack/PlatformGap).
public sealed partial class GameRenderer
{
    private void RenderFloorTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        // Schachbrett-Basis (subtile Farbvariation zwischen Zellen)
        bool alt = (gx + gy) % 2 == 0;
        _fillPaint.Color = alt ? _palette.FloorBase : _palette.FloorAlt;
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Welt-spezifische Boden-Details (nur Classic/Retro, bei Neon subtilere Variante)
        if (_worldPalette != null)
        {
            switch (_currentWorldIndex)
            {
                case 0: // Forest: Grashalme + leichte Farbvariation
                    if (!isNeon)
                        ProceduralTextures.DrawGrassBlades(canvas, _fillPaint, px, py, cs, gx, gy, _globalTimer, 50);
                    else
                    {
                        // Neon: Subtile grüne Leuchtpunkte
                        float gp = ProceduralTextures.CellRandom(gx, gy, 10);
                        if (gp < 0.3f)
                        {
                            _fillPaint.Color = new SKColor(0, 200, 80, 15);
                            canvas.DrawCircle(px + gp * cs + 5, py + cs * 0.7f, 1.5f, _fillPaint);
                        }
                    }
                    break;

                case 1: // Industrial: Metallplatten-Rillen + Nieten
                    if (!isNeon)
                    {
                        // Horizontale Rillen
                        _strokePaint.Color = new SKColor(140, 140, 150, 35);
                        _strokePaint.StrokeWidth = 0.5f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 2, py + cs * 0.33f, px + cs - 2, py + cs * 0.33f, _strokePaint);
                        canvas.DrawLine(px + 2, py + cs * 0.66f, px + cs - 2, py + cs * 0.66f, _strokePaint);
                        // Nieten nur auf manchen Zellen
                        if (ProceduralTextures.CellRandom(gx, gy, 11) < 0.35f)
                            ProceduralTextures.DrawMetalRivets(canvas, _fillPaint, px, py, cs, new SKColor(180, 180, 190), 50);
                    }
                    else
                    {
                        // Neon: Blaue Gitter-Highlights
                        if ((gx + gy) % 3 == 0)
                        {
                            _strokePaint.Color = new SKColor(0, 140, 255, 20);
                            _strokePaint.StrokeWidth = 0.5f;
                            _strokePaint.MaskFilter = null;
                            canvas.DrawLine(px + 3, py + cs * 0.5f, px + cs - 3, py + cs * 0.5f, _strokePaint);
                        }
                    }
                    break;

                case 2: // Cavern: Risse + dunkle Flecken
                    if (!isNeon)
                    {
                        if (ProceduralTextures.CellRandom(gx, gy, 12) < 0.4f)
                            ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(120, 100, 140), 30);
                        // Dunkle Feuchtigkeitsflecken
                        if (ProceduralTextures.CellRandom(gx, gy, 13) < 0.2f)
                        {
                            _fillPaint.Color = new SKColor(0, 0, 0, 15);
                            float fx = px + ProceduralTextures.CellRandom(gx, gy, 14) * cs;
                            float fy = py + ProceduralTextures.CellRandom(gx, gy, 15) * cs;
                            canvas.DrawOval(fx, fy, cs * 0.2f, cs * 0.15f, _fillPaint);
                        }
                    }
                    else
                    {
                        // Neon: Lila Kristallschimmer
                        if (ProceduralTextures.CellRandom(gx, gy, 16) < 0.25f)
                        {
                            float shimmer = MathF.Sin(_globalTimer * 1.5f + gx * 1.1f + gy * 0.7f) * 0.5f + 0.5f;
                            _fillPaint.Color = new SKColor(180, 0, 255, (byte)(12 * shimmer));
                            canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, cs * 0.12f, _fillPaint);
                        }
                    }
                    break;

                case 3: // Sky: Noise-basierte Wolkenmuster
                {
                    float noise = ProceduralTextures.Noise2D(gx * 0.4f, gy * 0.4f + _globalTimer * 0.05f);
                    byte cloudAlpha = (byte)(noise * (isNeon ? 12 : 20));
                    if (cloudAlpha > 3)
                    {
                        _fillPaint.Color = isNeon
                            ? new SKColor(0, 220, 255, cloudAlpha)
                            : new SKColor(255, 255, 255, cloudAlpha);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.5f, cs * 0.4f, cs * 0.25f, _fillPaint);
                    }
                    break;
                }

                case 4: // Inferno: Glutrisse + Ascheflecken
                    if (!isNeon)
                    {
                        if (ProceduralTextures.CellRandom(gx, gy, 17) < 0.35f)
                            ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px, py, cs, gx, gy, _globalTimer, 40);
                        if (ProceduralTextures.CellRandom(gx, gy, 18) < 0.3f)
                            ProceduralTextures.DrawSandGrain(canvas, _fillPaint, px, py, cs, gx, gy, new SKColor(60, 50, 45), 20);
                    }
                    else
                    {
                        // Neon: Pulsierende rote Risslinien
                        if (ProceduralTextures.CellRandom(gx, gy, 19) < 0.3f)
                        {
                            float pulse = MathF.Sin(_globalTimer * 2.5f + gx + gy * 0.8f) * 0.5f + 0.5f;
                            _strokePaint.Color = new SKColor(255, 40, 0, (byte)(25 * pulse));
                            _strokePaint.StrokeWidth = 0.8f;
                            _strokePaint.MaskFilter = null;
                            float sx = px + ProceduralTextures.CellRandom(gx, gy, 20) * cs;
                            float sy = py + ProceduralTextures.CellRandom(gx, gy, 21) * cs * 0.3f;
                            canvas.DrawLine(sx, sy, sx + cs * 0.4f, sy + cs * 0.5f, _strokePaint);
                        }
                    }
                    break;

                case 5: // Ruins: Erodierter Sandstein + Risslinien
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawSandGrain(canvas, _fillPaint, px, py, cs, gx, gy, new SKColor(190, 170, 130), 20);
                        if (ProceduralTextures.CellRandom(gx, gy, 22) < 0.3f)
                            ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(150, 130, 100), 25);
                    }
                    else
                    {
                        // Neon: Goldene Sandkörner
                        if (ProceduralTextures.CellRandom(gx, gy, 23) < 0.3f)
                        {
                            _fillPaint.Color = new SKColor(255, 200, 0, 10);
                            float dx = px + ProceduralTextures.CellRandom(gx, gy, 24) * cs;
                            float dy = py + ProceduralTextures.CellRandom(gx, gy, 25) * cs;
                            canvas.DrawCircle(dx, dy, 0.8f, _fillPaint);
                        }
                    }
                    break;

                case 6: // Ocean: Kaustik-Muster + Wellenlinien
                {
                    float noise = ProceduralTextures.Noise2D(gx * 0.5f + _globalTimer * 0.15f, gy * 0.5f + _globalTimer * 0.1f);
                    byte caustAlpha = (byte)(noise * (isNeon ? 15 : 25));
                    if (caustAlpha > 3)
                    {
                        _fillPaint.Color = isNeon
                            ? new SKColor(0, 200, 220, caustAlpha)
                            : new SKColor(180, 230, 255, caustAlpha);
                        canvas.DrawCircle(px + cs * 0.5f + noise * 3f, py + cs * 0.5f, cs * 0.15f, _fillPaint);
                    }
                    // Wellenlinien
                    if (!isNeon && ProceduralTextures.CellRandom(gx, gy, 26) < 0.25f)
                    {
                        _strokePaint.Color = new SKColor(100, 180, 220, 18);
                        _strokePaint.StrokeWidth = 0.5f;
                        _strokePaint.MaskFilter = null;
                        float wy = py + cs * 0.4f + MathF.Sin(_globalTimer * 0.8f + gx * 0.6f) * 2f;
                        canvas.DrawLine(px + 2, wy, px + cs - 2, wy + 1.5f, _strokePaint);
                    }
                    break;
                }

                case 7: // Volcano: Glänzender Obsidian mit Rissen
                    if (!isNeon)
                    {
                        // Glanz-Highlight
                        float gloss = ProceduralTextures.CellRandom(gx, gy, 27);
                        if (gloss < 0.25f)
                        {
                            _fillPaint.Color = new SKColor(255, 255, 255, 12);
                            canvas.DrawOval(px + cs * gloss + cs * 0.3f, py + cs * 0.35f, cs * 0.15f, cs * 0.08f, _fillPaint);
                        }
                        if (ProceduralTextures.CellRandom(gx, gy, 28) < 0.3f)
                            ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(40, 25, 20), 25);
                    }
                    else
                    {
                        // Neon: Orange Adern
                        if (ProceduralTextures.CellRandom(gx, gy, 29) < 0.2f)
                        {
                            float pulse = MathF.Sin(_globalTimer * 1.8f + gx * 0.9f + gy) * 0.4f + 0.6f;
                            _strokePaint.Color = new SKColor(255, 120, 0, (byte)(18 * pulse));
                            _strokePaint.StrokeWidth = 0.6f;
                            _strokePaint.MaskFilter = null;
                            canvas.DrawLine(px + 3, py + cs * 0.6f, px + cs - 3, py + cs * 0.4f, _strokePaint);
                        }
                    }
                    break;

                case 8: // SkyFortress: Marmor-Adern
                    if (!isNeon)
                    {
                        if (ProceduralTextures.CellRandom(gx, gy, 30) < 0.4f)
                            ProceduralTextures.DrawMarbleVeins(canvas, _strokePaint, px, py, cs, gx, gy, new SKColor(180, 170, 150), 20);
                    }
                    else
                    {
                        // Neon: Goldene Lichtpunkte
                        float sparkle = MathF.Sin(_globalTimer * 3f + gx * 2.1f + gy * 1.3f) * 0.5f + 0.5f;
                        if (sparkle > 0.7f && ProceduralTextures.CellRandom(gx, gy, 31) < 0.2f)
                        {
                            _fillPaint.Color = new SKColor(255, 230, 100, (byte)(15 * sparkle));
                            canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, 1f, _fillPaint);
                        }
                    }
                    break;

                case 9: // ShadowRealm: Nebelschleier + subtile Augen
                {
                    float mist = ProceduralTextures.Noise2D(gx * 0.3f + _globalTimer * 0.03f, gy * 0.3f);
                    byte mistAlpha = (byte)(mist * (isNeon ? 15 : 20));
                    if (mistAlpha > 3)
                    {
                        _fillPaint.Color = isNeon
                            ? new SKColor(100, 20, 180, mistAlpha)
                            : new SKColor(20, 10, 35, mistAlpha);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.5f, cs * 0.35f, cs * 0.2f, _fillPaint);
                    }
                    // Subtile Augen (sehr selten)
                    if (ProceduralTextures.CellRandom(gx, gy, 32) < 0.03f)
                    {
                        float blink = (_globalTimer * 0.2f + gx * 1.7f) % 6f;
                        if (blink < 5.5f) // Offen
                        {
                            byte eyeAlpha = (byte)(isNeon ? 20 : 15);
                            _fillPaint.Color = isNeon
                                ? new SKColor(200, 60, 255, eyeAlpha)
                                : new SKColor(160, 80, 200, eyeAlpha);
                            canvas.DrawOval(px + cs * 0.35f, py + cs * 0.5f, 1.5f, 1f, _fillPaint);
                            canvas.DrawOval(px + cs * 0.65f, py + cs * 0.5f, 1.5f, 1f, _fillPaint);
                        }
                    }
                    break;
                }
            }
        }

        // Gitter-Linien (subtil)
        _strokePaint.Color = _palette.FloorLine;
        _strokePaint.StrokeWidth = isNeon ? 0.5f : 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(px, py, px + cs, py, _strokePaint);
        canvas.DrawLine(px, py, px, py + cs, _strokePaint);
    }

    // ───────────────────────────────────────────────────────────────────────
    // WAND-RENDERING (statischer Teil → Wand-Cache, animierter Teil → Per-Frame)
    //
    // Wände (CellType.Wall) werden ausschließlich beim Level-Setup gesetzt
    // (Border + festes Schachbrett-/Layout-Muster) und nie zur Laufzeit verändert
    // — also genauso statisch wie der Boden. Der statische Anteil wird einmalig in
    // das Boden-Cache-Bitmap gebacken (RebuildFloorCache). Pro Frame fällt damit
    // nur noch der eine Bitmap-Blit an statt ~35 Wand-Renderings.
    //
    // Welt-spezifische ANIMIERTE Wand-Effekte (zeitabhängiges Pulsieren) dürfen
    // NICHT eingefroren werden — sie laufen weiter als dünner Per-Frame-Overlay
    // (RenderWallTileAnimatedOverlay). Betroffen sind nur Welt 4 (Inferno,
    // pulsierende Glut-Risse) und Welt 9 (ShadowRealm, pulsierende dunkle Masse).
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Statischer Wand-Anteil (Basis-Block + zeit-unabhängige Welt-Details).
    /// Wird in den Boden-Cache gebacken. Enthält bewusst KEINE animierten Effekte.
    /// </summary>
    private void RenderWallTileStatic(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        if (isNeon)
        {
            // Dunkler Stahlblock
            _fillPaint.Color = _palette.WallBase;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Neon-Kanten-Glow (Welt-Akzentfarbe)
            _strokePaint.Color = _palette.WallEdge;
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            // 3D Steinblock
            _fillPaint.Color = _palette.WallBase;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Highlight (oben + links)
            _fillPaint.Color = _palette.WallHighlight;
            canvas.DrawRect(px, py, cs, 3, _fillPaint);
            canvas.DrawRect(px, py, 3, cs, _fillPaint);

            // Schatten (unten + rechts)
            _fillPaint.Color = _palette.WallShadow;
            canvas.DrawRect(px, py + cs - 3, cs, 3, _fillPaint);
            canvas.DrawRect(px + cs - 3, py, 3, cs, _fillPaint);
        }

        // Welt-spezifische Wand-Details (nur statische — Welt 4 + 9 laufen im Overlay)
        if (_worldPalette != null)
        {
            switch (_currentWorldIndex)
            {
                case 0: // Forest: Bemooste Steine
                    if (!isNeon)
                        ProceduralTextures.DrawMossPatches(canvas, _fillPaint, px, py, cs, gx, gy, 35);
                    else if (ProceduralTextures.CellRandom(gx, gy, 50) < 0.3f)
                    {
                        _fillPaint.Color = new SKColor(0, 180, 60, 15);
                        canvas.DrawOval(px + cs * 0.3f, py + cs - 3, cs * 0.2f, 2f, _fillPaint);
                    }
                    break;

                case 1: // Industrial: Stahlplatten mit Nieten + Naht
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawMetalRivets(canvas, _fillPaint, px, py, cs, new SKColor(140, 145, 155), 60);
                        // Horizontale Naht
                        _strokePaint.Color = new SKColor(50, 55, 65, 50);
                        _strokePaint.StrokeWidth = 0.8f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 4, py + cs * 0.5f, px + cs - 4, py + cs * 0.5f, _strokePaint);
                    }
                    break;

                case 2: // Cavern: Kristall-Einschlüsse
                    if (ProceduralTextures.CellRandom(gx, gy, 51) < 0.35f)
                    {
                        float cx = px + ProceduralTextures.CellRandom(gx, gy, 52) * cs * 0.6f + cs * 0.2f;
                        float cy = py + ProceduralTextures.CellRandom(gx, gy, 53) * cs * 0.6f + cs * 0.2f;
                        float crystalSize = 2f + ProceduralTextures.CellRandom(gx, gy, 54) * 2f;
                        byte crystalAlpha = isNeon ? (byte)40 : (byte)50;
                        _fillPaint.Color = new SKColor(160, 100, 220, crystalAlpha);
                        // Raute (Kristall-Form) - gepoolter Pfad
                        _tilePath.Rewind();
                        _tilePath.MoveTo(cx, cy - crystalSize);
                        _tilePath.LineTo(cx + crystalSize * 0.6f, cy);
                        _tilePath.LineTo(cx, cy + crystalSize);
                        _tilePath.LineTo(cx - crystalSize * 0.6f, cy);
                        _tilePath.Close();
                        canvas.DrawPath(_tilePath, _fillPaint);
                        if (isNeon)
                        {
                            _fillPaint.Color = new SKColor(200, 100, 255, 20);
                            _fillPaint.MaskFilter = _smallGlow;
                            canvas.DrawPath(_tilePath, _fillPaint);
                            _fillPaint.MaskFilter = null;
                        }
                    }
                    break;

                case 3: // Sky: Weiche Wolkensäulen
                    if (!isNeon)
                    {
                        _fillPaint.Color = new SKColor(255, 255, 255, 15);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.3f, cs * 0.35f, cs * 0.2f, _fillPaint);
                        canvas.DrawOval(px + cs * 0.4f, py + cs * 0.6f, cs * 0.3f, cs * 0.15f, _fillPaint);
                    }
                    else
                    {
                        _fillPaint.Color = new SKColor(0, 220, 255, 8);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.4f, cs * 0.3f, cs * 0.15f, _fillPaint);
                    }
                    break;

                // case 4 (Inferno, pulsierende Glut-Risse) → RenderWallTileAnimatedOverlay

                case 5: // Ruins: Sandstein mit Risslinien + abgebrochene Ecken
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawCracks(canvas, _strokePaint, px, py, cs, gx, gy,
                            new SKColor(120, 100, 70), 35);
                        // Abgebrochene Ecke (einzelne Zellen)
                        if (ProceduralTextures.CellRandom(gx, gy, 55) < 0.2f)
                        {
                            _fillPaint.Color = _palette.FloorBase.WithAlpha(180);
                            _tilePath.Rewind();
                            _tilePath.MoveTo(px + cs, py);
                            _tilePath.LineTo(px + cs - 4, py);
                            _tilePath.LineTo(px + cs, py + 4);
                            _tilePath.Close();
                            canvas.DrawPath(_tilePath, _fillPaint);
                        }
                    }
                    break;

                case 6: // Ocean: Korallen-bewachsen
                    if (!isNeon)
                        ProceduralTextures.DrawCoralGrowth(canvas, _fillPaint, px, py, cs, gx, gy,
                            new SKColor(200, 100, 120), 40);
                    else if (ProceduralTextures.CellRandom(gx, gy, 56) < 0.3f)
                    {
                        _fillPaint.Color = new SKColor(0, 200, 220, 15);
                        float cx = px + cs * 0.5f;
                        float cy = py + cs * 0.5f;
                        canvas.DrawCircle(cx, cy, 2.5f, _fillPaint);
                    }
                    break;

                case 7: // Volcano: Basalt-Säulen (vertikale Linien)
                {
                    byte lineAlpha = isNeon ? (byte)25 : (byte)30;
                    _strokePaint.Color = isNeon
                        ? new SKColor(255, 80, 0, lineAlpha)
                        : new SKColor(60, 45, 40, lineAlpha);
                    _strokePaint.StrokeWidth = 0.6f;
                    _strokePaint.MaskFilter = null;
                    int lines = 3 + (int)(ProceduralTextures.CellRandom(gx, gy, 57) * 2);
                    for (int i = 0; i < lines; i++)
                    {
                        float lx = px + cs * (0.15f + i * 0.7f / lines);
                        canvas.DrawLine(lx, py + 3, lx, py + cs - 3, _strokePaint);
                    }
                    break;
                }

                case 8: // SkyFortress: Marmor-Säulen mit Glanz
                    if (!isNeon)
                    {
                        ProceduralTextures.DrawMarbleVeins(canvas, _strokePaint, px, py, cs, gx, gy,
                            new SKColor(200, 185, 150), 20);
                        // Glanz-Highlight
                        _fillPaint.Color = new SKColor(255, 255, 240, 15);
                        canvas.DrawRect(px + 3, py + 2, 2, cs - 4, _fillPaint);
                    }
                    else
                    {
                        // Neon: Goldene Adern
                        if (ProceduralTextures.CellRandom(gx, gy, 58) < 0.4f)
                        {
                            _strokePaint.Color = new SKColor(255, 230, 100, 20);
                            _strokePaint.StrokeWidth = 0.5f;
                            _strokePaint.MaskFilter = _smallGlow;
                            float vy = py + ProceduralTextures.CellRandom(gx, gy, 59) * cs;
                            canvas.DrawLine(px + 3, vy, px + cs - 3, vy + 2, _strokePaint);
                            _strokePaint.MaskFilter = null;
                        }
                    }
                    break;

                // case 9 (ShadowRealm, pulsierende dunkle Masse) → RenderWallTileAnimatedOverlay
            }
        }
    }

    /// <summary>
    /// Animierter Wand-Overlay (zeitabhängiges Pulsieren). Wird pro Frame über den
    /// gecachten statischen Wand-Block gezeichnet — nur für Welten mit animiertem
    /// Wand-Detail (Welt 4 Inferno, Welt 9 ShadowRealm). Alle anderen Welten haben
    /// rein statische Wände und zeichnen hier nichts.
    /// </summary>
    private void RenderWallTileAnimatedOverlay(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        if (_worldPalette == null)
            return;

        switch (_currentWorldIndex)
        {
            case 4: // Inferno: Glut-durchzogener Obsidian (pulsierend)
                ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px, py, cs, gx, gy, _globalTimer,
                    isNeon ? (byte)50 : (byte)45);
                break;

            case 9: // ShadowRealm: Pulsierende dunkle Masse
            {
                _fillPaint.MaskFilter = null;
                float pulse = MathF.Sin(_globalTimer * 1.5f + gx * 0.9f + gy * 1.1f) * 0.4f + 0.6f;
                byte darkAlpha = (byte)(isNeon ? 20 * pulse : 15 * pulse);
                _fillPaint.Color = isNeon
                    ? new SKColor(150, 30, 220, darkAlpha)
                    : new SKColor(40, 15, 60, darkAlpha);
                canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-MECHANIK-TILES (Ice / Conveyor / Teleporter / LavaCrack / PlatformGap)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Eis-Boden: Hellblauer reflektiver Glanz mit Schimmer-Animation</summary>
    private void RenderIceTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Basis-Eis-Farbe (Schachbrett-Variation)
        bool alt = (gx + gy) % 2 == 0;
        if (isNeon)
        {
            _fillPaint.Color = alt ? new SKColor(40, 60, 80) : new SKColor(35, 55, 75);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Neon-Glow-Linien (Riss-Muster)
            _strokePaint.Color = new SKColor(100, 200, 255, 80);
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 5, py + cs * 0.6f, _strokePaint);
            canvas.DrawLine(px + cs * 0.4f, py + 2, px + cs * 0.7f, py + cs - 3, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            _fillPaint.Color = alt ? new SKColor(180, 210, 235) : new SKColor(170, 200, 225);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Riss-Linien
            _strokePaint.Color = new SKColor(200, 230, 250, 120);
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 5, py + cs * 0.6f, _strokePaint);
            canvas.DrawLine(px + cs * 0.4f, py + 2, px + cs * 0.7f, py + cs - 3, _strokePaint);
        }

        // Wandernder Glanz-Highlight (Lichtreflexion)
        float shimmerX = (MathF.Sin(_globalTimer * 1.5f + gx * 0.5f) * 0.5f + 0.5f) * cs;
        float shimmerY = (MathF.Cos(_globalTimer * 1.2f + gy * 0.7f) * 0.5f + 0.5f) * cs;
        byte shimmerAlpha = isNeon ? (byte)60 : (byte)90;
        _fillPaint.Color = new SKColor(255, 255, 255, shimmerAlpha);
        canvas.DrawCircle(px + shimmerX, py + shimmerY, cs * 0.15f, _fillPaint);

        // Grid-Linie
        _strokePaint.Color = isNeon ? new SKColor(80, 160, 220, 40) : new SKColor(150, 190, 215);
        _strokePaint.StrokeWidth = 0.5f;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(px, py, px + cs, py, _strokePaint);
        canvas.DrawLine(px, py, px, py + cs, _strokePaint);
    }

    /// <summary>Förderband: Animierte Pfeile in Förderrichtung</summary>
    private void RenderConveyorTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Basis (metallisch-grauer Boden)
        if (isNeon)
        {
            _fillPaint.Color = new SKColor(45, 45, 55);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
        }
        else
        {
            _fillPaint.Color = new SKColor(160, 165, 175);
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
        }

        // Seitenleisten (metallische Ränder)
        bool horizontal = cell.ConveyorDirection is Models.Entities.Direction.Left or Models.Entities.Direction.Right;
        _fillPaint.Color = isNeon ? new SKColor(60, 60, 75) : new SKColor(130, 135, 145);
        if (horizontal)
        {
            canvas.DrawRect(px, py, cs, 3, _fillPaint);
            canvas.DrawRect(px, py + cs - 3, cs, 3, _fillPaint);
        }
        else
        {
            canvas.DrawRect(px, py, 3, cs, _fillPaint);
            canvas.DrawRect(px + cs - 3, py, 3, cs, _fillPaint);
        }

        // Animierte Pfeil-Chevrons (3 Stück, wandern in Förderrichtung)
        float animOffset = (_globalTimer * 40f) % cs; // Pixel-Offset Animation

        var arrowColor = isNeon ? new SKColor(255, 200, 0, 180) : new SKColor(220, 180, 40, 200);
        _strokePaint.Color = arrowColor;
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;

        for (int i = 0; i < 3; i++)
        {
            float offset = (i * cs / 3f + animOffset) % cs - cs / 2f;
            float chevronSize = cs * 0.2f;

            switch (cell.ConveyorDirection)
            {
                case Models.Entities.Direction.Right:
                    canvas.DrawLine(cx + offset - chevronSize, cy - chevronSize, cx + offset, cy, _strokePaint);
                    canvas.DrawLine(cx + offset, cy, cx + offset - chevronSize, cy + chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Left:
                    canvas.DrawLine(cx - offset + chevronSize, cy - chevronSize, cx - offset, cy, _strokePaint);
                    canvas.DrawLine(cx - offset, cy, cx - offset + chevronSize, cy + chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Down:
                    canvas.DrawLine(cx - chevronSize, cy + offset - chevronSize, cx, cy + offset, _strokePaint);
                    canvas.DrawLine(cx, cy + offset, cx + chevronSize, cy + offset - chevronSize, _strokePaint);
                    break;
                case Models.Entities.Direction.Up:
                    canvas.DrawLine(cx - chevronSize, cy - offset + chevronSize, cx, cy - offset, _strokePaint);
                    canvas.DrawLine(cx, cy - offset, cx + chevronSize, cy - offset + chevronSize, _strokePaint);
                    break;
            }
        }
        _strokePaint.MaskFilter = null;
    }

    /// <summary>Teleporter: Leuchtender pulsierender Ring mit Farb-ID</summary>
    private void RenderTeleporterTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Boden (Basis)
        bool alt = (cell.X + cell.Y) % 2 == 0;
        _fillPaint.Color = alt ? _palette.FloorBase : _palette.FloorAlt;
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Portal-Farbe basierend auf ColorId
        SKColor portalColor = cell.TeleporterColorId switch
        {
            0 => new SKColor(50, 150, 255),  // Blau
            1 => new SKColor(50, 255, 120),  // Grün
            2 => new SKColor(255, 150, 50),  // Orange
            _ => new SKColor(200, 100, 255)  // Lila
        };

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;
        float pulse = MathF.Sin(_globalTimer * 4f + cell.X * 0.5f) * 0.15f + 0.85f;
        float cooldownFade = cell.TeleporterCooldown > 0 ? 0.3f : 1f;

        // Äußerer Glow
        _glowPaint.Color = portalColor.WithAlpha((byte)(80 * pulse * cooldownFade));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(cx, cy, cs * 0.45f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Rotierender Ring
        float rotation = _globalTimer * 90f; // 90° pro Sekunde
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(rotation);

        // Ring zeichnen (4 Arcs)
        _strokePaint.Color = portalColor.WithAlpha((byte)(220 * cooldownFade));
        _strokePaint.StrokeWidth = 2.5f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;

        float r = cs * 0.35f * pulse;
        var arcRect = new SKRect(-r, -r, r, r);

        // 3 Arcs für rotierenden Portal-Ring (wiederverwendeter _fusePath)
        _fusePath.Rewind();
        _fusePath.AddArc(arcRect, 0, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _fusePath.Rewind();
        _fusePath.AddArc(arcRect, 120, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _fusePath.Rewind();
        _fusePath.AddArc(arcRect, 240, 80);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        canvas.Restore();

        // Innerer Punkt (Kern des Portals)
        _fillPaint.Color = portalColor.WithAlpha((byte)(180 * pulse * cooldownFade));
        _fillPaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawCircle(cx, cy, cs * 0.1f, _fillPaint);
        _fillPaint.MaskFilter = null;
    }

    /// <summary>Lava-Riss: Pulsierender roter Riss, gefährlich wenn aktiv</summary>
    private void RenderLavaCrackTile(SKCanvas canvas, float px, float py, int cs, Cell cell, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Boden (dunkler als normal, vulkanisch)
        _fillPaint.Color = isNeon ? new SKColor(45, 20, 20) : new SKColor(100, 65, 55);
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        float cx = px + cs / 2f;
        float cy = py + cs / 2f;

        bool isActive = cell.IsLavaCrackActive;
        float timerMod = cell.LavaCrackTimer % 4f;

        // Riss-Muster (immer sichtbar, auch wenn inaktiv)
        byte crackAlpha = isActive ? (byte)255 : (byte)100;
        var crackColor = isActive
            ? (isNeon ? new SKColor(255, 60, 0, crackAlpha) : new SKColor(255, 80, 20, crackAlpha))
            : (isNeon ? new SKColor(200, 80, 40, crackAlpha) : new SKColor(180, 90, 50, crackAlpha));

        _strokePaint.Color = crackColor;
        _strokePaint.StrokeWidth = isActive ? 2.5f : 1.5f;
        _strokePaint.MaskFilter = isActive && isNeon ? _smallGlow : null;

        // Zickzack-Riss
        _fusePath.Rewind();
        _fusePath.MoveTo(px + cs * 0.2f, py + 2);
        _fusePath.LineTo(px + cs * 0.45f, py + cs * 0.35f);
        _fusePath.LineTo(px + cs * 0.3f, py + cs * 0.5f);
        _fusePath.LineTo(px + cs * 0.6f, py + cs * 0.65f);
        _fusePath.LineTo(px + cs * 0.5f, py + cs - 2);
        canvas.DrawPath(_fusePath, _strokePaint);

        // Zweiter kleinerer Riss
        _fusePath.Rewind();
        _fusePath.MoveTo(px + cs * 0.7f, py + 4);
        _fusePath.LineTo(px + cs * 0.55f, py + cs * 0.4f);
        _fusePath.LineTo(px + cs * 0.8f, py + cs * 0.7f);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Aktiver Zustand: Glühende Lava-Füllung
        if (isActive)
        {
            float intensity = (timerMod - 2.5f) / 1.5f; // 0→1 während aktiver Phase
            byte lavaAlpha = (byte)(120 + 80 * MathF.Sin(_globalTimer * 8f));

            // Roter/orangener Glow über die ganze Zelle
            _glowPaint.Color = isNeon
                ? new SKColor(255, 40, 0, lavaAlpha)
                : new SKColor(255, 100, 20, lavaAlpha);
            _glowPaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Gefahren-Indikator: Pulsierendes X in der Mitte
            _strokePaint.Color = new SKColor(255, 255, 200, (byte)(200 * intensity));
            _strokePaint.StrokeWidth = 2f;
            float xSize = cs * 0.15f;
            canvas.DrawLine(cx - xSize, cy - xSize, cx + xSize, cy + xSize, _strokePaint);
            canvas.DrawLine(cx + xSize, cy - xSize, cx - xSize, cy + xSize, _strokePaint);
        }
        else
        {
            // Inaktiv: Schwacher Warn-Glow wenn fast aktiv (timerMod > 2.0)
            if (timerMod > 2.0f)
            {
                float warnIntensity = (timerMod - 2.0f) / 0.5f;
                byte warnAlpha = (byte)(40 * warnIntensity);
                _fillPaint.Color = isNeon
                    ? new SKColor(255, 60, 0, warnAlpha)
                    : new SKColor(255, 100, 20, warnAlpha);
                canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);
            }
        }
    }

    /// <summary>Plattform-Lücke (Welt 9: Himmelsfestung) - dunkle Lücke mit Tiefeneffekt</summary>
    private void RenderPlatformGapTile(SKCanvas canvas, float px, float py, int cs, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        // Dunkler Abgrund
        _fillPaint.Color = isNeon ? new SKColor(10, 5, 20) : new SKColor(20, 15, 25);
        canvas.DrawRect(px, py, cs, cs, _fillPaint);

        // Innerer noch dunklerer Kern (Tiefeneffekt)
        _fillPaint.Color = isNeon ? new SKColor(5, 0, 15) : new SKColor(10, 8, 15);
        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _fillPaint);

        // Subtile Kanten (heller Rand fuer Kontrast zum Boden)
        _strokePaint.Color = isNeon ? new SKColor(80, 60, 140, 100) : new SKColor(100, 80, 120, 80);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _strokePaint);

        // Warnendes Pulsieren (subtil)
        float pulse = MathF.Sin(_globalTimer * 2f) * 0.3f + 0.5f;
        byte warningAlpha = (byte)(30 * pulse);
        _fillPaint.Color = isNeon
            ? new SKColor(200, 50, 50, warningAlpha)
            : new SKColor(180, 40, 40, warningAlpha);
        canvas.DrawRect(px + 4, py + 4, cs - 8, cs - 8, _fillPaint);
    }
}
