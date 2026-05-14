using BomberBlast.Models;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

// v2.0.37 (Plan Task 2.1): Aus GameRenderer.Grid.cs extrahiert.
// Enthaelt zerstoerbare Bloecke (Block-Tile mit welt-spezifischen Texturen + Destroy-Animation in 3 Phasen).
public sealed partial class GameRenderer
{
    private void RenderBlockTile(SKCanvas canvas, float px, float py, int cs, int gx, int gy, bool isNeon)
    {
        _fillPaint.MaskFilter = null;

        if (isNeon)
        {
            // Dunkler Block mit sichtbarem Rand
            _fillPaint.Color = _palette.BlockBase;
            canvas.DrawRect(px + 1, py + 1, cs - 2, cs - 2, _fillPaint);

            // Heller Rand oben/links für 3D-Effekt
            _fillPaint.Color = _palette.BlockHighlight;
            canvas.DrawRect(px + 1, py + 1, cs - 2, 2, _fillPaint);
            canvas.DrawRect(px + 1, py + 1, 2, cs - 2, _fillPaint);

            // Dunkler Rand unten/rechts
            _fillPaint.Color = _palette.BlockShadow;
            canvas.DrawRect(px + 1, py + cs - 3, cs - 2, 2, _fillPaint);
            canvas.DrawRect(px + cs - 3, py + 1, 2, cs - 2, _fillPaint);

            // Glow-Riss-Muster
            _strokePaint.Color = _palette.BlockMortar;
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;

            // Horizontaler Riss
            canvas.DrawLine(px + 4, py + cs / 2f, px + cs - 4, py + cs / 2f, _strokePaint);
            // Vertikaler Riss (versetzt pro Spalte)
            float vx = (gx % 2 == 0) ? px + cs / 2f : px + cs / 3f;
            canvas.DrawLine(vx, py + 4, vx, py + cs / 2f, _strokePaint);

            // Zusätzlicher diagonaler Riss
            float vx2 = (gx % 2 == 0) ? px + cs * 0.65f : px + cs * 0.6f;
            canvas.DrawLine(vx2, py + cs / 2f + 2, vx2 - cs * 0.15f, py + cs - 4, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
        else
        {
            // 3D Ziegel mit Mörtellinien
            _fillPaint.Color = _palette.BlockBase;
            canvas.DrawRect(px + 2, py + 2, cs - 4, cs - 4, _fillPaint);

            // Highlight (oben + links)
            _fillPaint.Color = _palette.BlockHighlight;
            canvas.DrawRect(px + 2, py + 2, cs - 4, 2, _fillPaint);
            canvas.DrawRect(px + 2, py + 2, 2, cs - 4, _fillPaint);

            // Schatten (unten + rechts)
            _fillPaint.Color = _palette.BlockShadow;
            canvas.DrawRect(px + 2, py + cs - 4, cs - 4, 2, _fillPaint);
            canvas.DrawRect(px + cs - 4, py + 2, 2, cs - 4, _fillPaint);

            // Mörtel-Kreuzlinien
            _strokePaint.Color = _palette.BlockMortar;
            _strokePaint.StrokeWidth = 1f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(px + 2, py + cs / 2f, px + cs - 2, py + cs / 2f, _strokePaint);
            float vx = (gx % 2 == 0) ? px + cs / 2f : px + cs / 3f;
            canvas.DrawLine(vx, py + 2, vx, py + cs / 2f, _strokePaint);
        }

        // Welt-spezifische Block-Details (intensiviert)
        if (_worldPalette != null)
        {
            switch (_currentWorldIndex)
            {
                case 0: // Forest: Holzkiste mit kräftiger Maserung + Metallbändern + Nägeln
                    if (!isNeon)
                    {
                        // Kräftigere Holzmaserung
                        ProceduralTextures.DrawWoodGrain(canvas, _strokePaint, px + 3, py + 3, cs - 6,
                            gx, gy, new SKColor(90, 60, 30), 25);
                        // Horizontales Metallband (Kisten-Look)
                        _fillPaint.Color = new SKColor(100, 95, 85, 100);
                        canvas.DrawRect(px + 2, py + cs / 2f - 1.5f, cs - 4, 3, _fillPaint);
                        // Nagelköpfe (4 Ecken + 2 auf dem Band)
                        _fillPaint.Color = new SKColor(140, 130, 115, 140);
                        canvas.DrawCircle(px + 5, py + 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + 5, py + cs - 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 1.5f, _fillPaint);
                        // Nägel auf dem Band
                        _fillPaint.Color = new SKColor(160, 150, 130, 160);
                        canvas.DrawCircle(px + cs * 0.25f, py + cs / 2f, 1.2f, _fillPaint);
                        canvas.DrawCircle(px + cs * 0.75f, py + cs / 2f, 1.2f, _fillPaint);
                        // Holzfarb-Gradient (dunkler unten)
                        _fillPaint.Color = new SKColor(40, 25, 10, 30);
                        canvas.DrawRect(px + 3, py + cs * 0.7f, cs - 6, cs * 0.27f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Grüne Holz-Silhouette + Nagelglow
                        _strokePaint.Color = new SKColor(0, 180, 60, 30);
                        _strokePaint.StrokeWidth = 0.7f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 4, py + cs * 0.3f, px + cs - 4, py + cs * 0.35f, _strokePaint);
                        canvas.DrawLine(px + 4, py + cs * 0.6f, px + cs - 4, py + cs * 0.65f, _strokePaint);
                        _fillPaint.Color = new SKColor(0, 200, 80, 25);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(px + 5, py + 5, 1.2f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 1.2f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 1: // Industrial: Metall-Container mit Warnstreifen + Bolzen + Rost
                    if (!isNeon)
                    {
                        // Diagonale Warnstreifen (gelb/schwarz)
                        _strokePaint.Color = new SKColor(200, 180, 40, 50);
                        _strokePaint.StrokeWidth = 2f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 3, py + cs - 3, px + cs * 0.35f, py + 3, _strokePaint);
                        canvas.DrawLine(px + cs * 0.35f, py + cs - 3, px + cs * 0.65f, py + 3, _strokePaint);
                        canvas.DrawLine(px + cs * 0.65f, py + cs - 3, px + cs - 3, py + 3, _strokePaint);
                        // Container-Rillen
                        _strokePaint.Color = new SKColor(80, 85, 95, 60);
                        _strokePaint.StrokeWidth = 0.8f;
                        canvas.DrawLine(px + 3, py + cs * 0.3f, px + cs - 3, py + cs * 0.3f, _strokePaint);
                        canvas.DrawLine(px + 3, py + cs * 0.7f, px + cs - 3, py + cs * 0.7f, _strokePaint);
                        // Bolzen an den Ecken
                        ProceduralTextures.DrawMetalRivets(canvas, _fillPaint, px + 3, py + 3, cs - 6,
                            new SKColor(140, 140, 155), 100);
                        // Rost-Flecken (kräftiger)
                        if (ProceduralTextures.CellRandom(gx, gy, 61) < 0.45f)
                        {
                            float rx = px + ProceduralTextures.CellRandom(gx, gy, 62) * cs * 0.4f + cs * 0.25f;
                            float ry = py + ProceduralTextures.CellRandom(gx, gy, 63) * cs * 0.4f + cs * 0.25f;
                            _fillPaint.Color = new SKColor(160, 80, 30, 55);
                            canvas.DrawOval(rx, ry, cs * 0.12f, cs * 0.1f, _fillPaint);
                        }
                    }
                    else
                    {
                        // Neon: Gelbe Warnstreifen-Glow
                        _strokePaint.Color = new SKColor(255, 200, 0, 25);
                        _strokePaint.StrokeWidth = 1.5f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawLine(px + 4, py + cs - 4, px + cs - 4, py + 4, _strokePaint);
                        _strokePaint.MaskFilter = null;
                        // Nieten-Glow
                        _fillPaint.Color = new SKColor(200, 200, 220, 20);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(px + 5, py + 5, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 1.5f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 2: // Cavern: Kristall-Gestein mit farbigen Facetten + Reflexen
                {
                    float rng = ProceduralTextures.CellRandom(gx, gy, 64);
                    // Kristall-Einschluss (immer, nicht nur 40%)
                    byte crystalAlpha = isNeon ? (byte)50 : (byte)70;
                    // Kristall-Farbe variiert pro Zelle (Amethyst, Smaragd, Saphir)
                    int colorIdx = (int)(ProceduralTextures.CellRandom(gx, gy, 74) * 3);
                    var crystalColor = colorIdx switch
                    {
                        0 => new SKColor(180, 100, 240, crystalAlpha),
                        1 => new SKColor(100, 220, 140, crystalAlpha),
                        _ => new SKColor(100, 140, 240, crystalAlpha)
                    };
                    _fillPaint.Color = crystalColor;
                    {
                        _tilePath.Rewind();
                        float fx = px + rng * cs * 0.25f + cs * 0.2f;
                        _tilePath.MoveTo(fx, py + 4);
                        _tilePath.LineTo(fx + cs * 0.35f, py + cs * 0.45f);
                        _tilePath.LineTo(fx + cs * 0.15f, py + cs * 0.55f);
                        _tilePath.LineTo(fx - cs * 0.1f, py + cs * 0.4f);
                        _tilePath.Close();
                        canvas.DrawPath(_tilePath, _fillPaint);
                    }
                    // Glanz-Highlight auf dem Kristall
                    _fillPaint.Color = new SKColor(255, 255, 255, 40);
                    float gx2 = px + rng * cs * 0.25f + cs * 0.25f;
                    canvas.DrawCircle(gx2, py + cs * 0.25f, 2f, _fillPaint);
                    if (isNeon)
                    {
                        // Neon: Kristall-Glow
                        _fillPaint.Color = crystalColor.WithAlpha(25);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(gx2 + cs * 0.1f, py + cs * 0.35f, cs * 0.15f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    // Zweite kleine Kristall-Facette (andere Ecke)
                    if (ProceduralTextures.CellRandom(gx, gy, 75) < 0.5f)
                    {
                        _fillPaint.Color = crystalColor.WithAlpha((byte)(crystalAlpha * 0.6f));
                        _tilePath.Rewind();
                        float fx2 = px + cs * 0.55f;
                        _tilePath.MoveTo(fx2, py + cs * 0.55f);
                        _tilePath.LineTo(fx2 + cs * 0.2f, py + cs * 0.7f);
                        _tilePath.LineTo(fx2 + cs * 0.05f, py + cs - 4);
                        _tilePath.Close();
                        canvas.DrawPath(_tilePath, _fillPaint);
                    }
                    break;
                }

                case 3: // Sky: Wolken-Blöcke (weiche Kanten, volumetrischer Wolken-Look)
                    if (!isNeon)
                    {
                        // Mehrere überlappende Wolkenformen für 3D-Volumen
                        _fillPaint.Color = new SKColor(255, 255, 255, 35);
                        canvas.DrawOval(px + cs * 0.35f, py + cs * 0.3f, cs * 0.3f, cs * 0.2f, _fillPaint);
                        canvas.DrawOval(px + cs * 0.6f, py + cs * 0.35f, cs * 0.25f, cs * 0.18f, _fillPaint);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.55f, cs * 0.35f, cs * 0.22f, _fillPaint);
                        // Heller Rand oben (Sonnenlicht)
                        _fillPaint.Color = new SKColor(255, 255, 240, 45);
                        canvas.DrawRect(px + 3, py + 2, cs - 6, 3, _fillPaint);
                        // Weicher dunkler Schatten unten
                        _fillPaint.Color = new SKColor(100, 120, 160, 25);
                        canvas.DrawRect(px + 3, py + cs - 5, cs - 6, 3, _fillPaint);
                    }
                    else
                    {
                        // Neon: Cyan-Wolken-Glow
                        _fillPaint.Color = new SKColor(0, 220, 255, 18);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.4f, cs * 0.3f, cs * 0.2f, _fillPaint);
                        _strokePaint.Color = new SKColor(0, 200, 255, 20);
                        _strokePaint.StrokeWidth = 0.6f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawRoundRect(px + 3, py + 3, cs - 6, cs - 6, 5, 5, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 4: // Inferno: Verkohltes Holz mit Glutrissen + Asche
                    if (!isNeon)
                    {
                        // Kohleschicht (dunkler Overlay)
                        _fillPaint.Color = new SKColor(15, 10, 5, 50);
                        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _fillPaint);
                        // Mehrere Brandspuren (Verkohlungen)
                        _fillPaint.Color = new SKColor(25, 18, 10, 60);
                        float bx = px + ProceduralTextures.CellRandom(gx, gy, 66) * cs * 0.4f + cs * 0.15f;
                        float by = py + ProceduralTextures.CellRandom(gx, gy, 67) * cs * 0.3f + cs * 0.2f;
                        canvas.DrawOval(bx, by, cs * 0.18f, cs * 0.14f, _fillPaint);
                        // Glühende Risse (kräftiger)
                        ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            _globalTimer, 30);
                        // Zusätzlicher Glutriss quer
                        float pulse = MathF.Sin(_globalTimer * 2f + gx * 0.9f + gy * 1.3f) * 0.3f + 0.7f;
                        _strokePaint.Color = new SKColor(255, 100, 20, (byte)(40 * pulse));
                        _strokePaint.StrokeWidth = 0.8f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawLine(px + 4, py + cs * 0.6f, px + cs - 4, py + cs * 0.55f, _strokePaint);
                        // Asche-Flecken (graue Punkte)
                        _fillPaint.Color = new SKColor(80, 75, 70, 40);
                        canvas.DrawCircle(px + cs * 0.3f, py + cs * 0.8f, 1.5f, _fillPaint);
                        canvas.DrawCircle(px + cs * 0.7f, py + cs * 0.75f, 1f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Pulsierende rote Glut-Kanten (intensiver)
                        float pulse = MathF.Sin(_globalTimer * 2f + gx * 0.7f + gy * 1.1f) * 0.4f + 0.6f;
                        _strokePaint.Color = new SKColor(255, 60, 0, (byte)(45 * pulse));
                        _strokePaint.StrokeWidth = 1f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _strokePaint);
                        // Innere Glut-Linie
                        _strokePaint.Color = new SKColor(255, 120, 0, (byte)(25 * pulse));
                        canvas.DrawLine(px + 5, py + cs * 0.5f, px + cs - 5, py + cs * 0.45f, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 5: // Ruins: Ziegelwand mit sichtbaren Ziegeln + Efeu-Ranken + Risse
                    if (!isNeon)
                    {
                        // Kräftigere Ziegellinien
                        ProceduralTextures.DrawBrickPattern(canvas, _strokePaint, px + 3, py + 3, cs - 6,
                            new SKColor(150, 110, 80), 30);
                        // Abgebrochene Ecke (zufällig, 30% der Blöcke)
                        if (ProceduralTextures.CellRandom(gx, gy, 76) < 0.3f)
                        {
                            // Dunkles Dreieck in einer Ecke (abgebröckelt)
                            _fillPaint.Color = new SKColor(60, 50, 40, 50);
                            _tilePath.Rewind();
                            _tilePath.MoveTo(px + cs - 3, py + 3);
                            _tilePath.LineTo(px + cs - 3, py + cs * 0.25f);
                            _tilePath.LineTo(px + cs * 0.75f, py + 3);
                            _tilePath.Close();
                            canvas.DrawPath(_tilePath, _fillPaint);
                        }
                        // Efeu/Moos an Kanten (grüne Flecken)
                        if (ProceduralTextures.CellRandom(gx, gy, 77) < 0.4f)
                        {
                            ProceduralTextures.DrawMossPatches(canvas, _fillPaint, px + 2, py + cs * 0.6f, cs - 4,
                                gx, gy, 50);
                        }
                        // Risslinien
                        ProceduralTextures.DrawCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            new SKColor(100, 80, 60), 40);
                    }
                    else
                    {
                        // Neon: Goldene Ziegel-Umrisse (kräftiger)
                        _strokePaint.Color = new SKColor(255, 200, 80, 25);
                        _strokePaint.StrokeWidth = 0.7f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawLine(px + 4, py + cs * 0.35f, px + cs - 4, py + cs * 0.35f, _strokePaint);
                        canvas.DrawLine(px + 4, py + cs * 0.65f, px + cs - 4, py + cs * 0.65f, _strokePaint);
                        float brickOff = (gx % 2 == 0) ? cs * 0.45f : cs * 0.3f;
                        canvas.DrawLine(px + brickOff, py + 4, px + brickOff, py + cs * 0.35f, _strokePaint);
                        float brickOff2 = (gx % 2 == 0) ? cs * 0.65f : cs * 0.5f;
                        canvas.DrawLine(px + brickOff2, py + cs * 0.35f, px + brickOff2, py + cs * 0.65f, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 6: // Ocean: Versunkene Truhe mit Metallbändern + Algen + Seepocken
                    if (!isNeon)
                    {
                        // Holzmaserung
                        ProceduralTextures.DrawWoodGrain(canvas, _strokePaint, px + 3, py + 3, cs - 6,
                            gx, gy, new SKColor(80, 70, 50), 20);
                        // Metallband (horizontal)
                        _fillPaint.Color = new SKColor(90, 100, 80, 90);
                        canvas.DrawRect(px + 2, py + cs * 0.4f - 1, cs - 4, 2.5f, _fillPaint);
                        // Metallband Nieten
                        _fillPaint.Color = new SKColor(120, 130, 110, 100);
                        canvas.DrawCircle(px + 5, py + cs * 0.4f, 1f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs * 0.4f, 1f, _fillPaint);
                        // Algen-Bewuchs (kräftiger, mehrere)
                        _fillPaint.Color = new SKColor(30, 140, 50, 65);
                        float ax = px + ProceduralTextures.CellRandom(gx, gy, 69) * cs * 0.3f + 4;
                        canvas.DrawOval(ax, py + cs - 4, cs * 0.1f, 3.5f, _fillPaint);
                        canvas.DrawOval(ax + cs * 0.25f, py + cs - 3, cs * 0.08f, 3f, _fillPaint);
                        _fillPaint.Color = new SKColor(40, 160, 60, 50);
                        canvas.DrawOval(ax + cs * 0.4f, py + cs - 5, cs * 0.07f, 4f, _fillPaint);
                        // Seepocken (kleine weiße Kreise)
                        if (ProceduralTextures.CellRandom(gx, gy, 78) < 0.4f)
                        {
                            _fillPaint.Color = new SKColor(200, 200, 190, 50);
                            canvas.DrawCircle(px + cs * 0.7f, py + cs * 0.25f, 1.5f, _fillPaint);
                            canvas.DrawCircle(px + cs * 0.8f, py + cs * 0.3f, 1f, _fillPaint);
                        }
                    }
                    else
                    {
                        // Neon: Cyan Wasser-Reflexion + Algen-Glow
                        _fillPaint.Color = new SKColor(0, 180, 220, 18);
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.55f, cs * 0.25f, cs * 0.12f, _fillPaint);
                        _fillPaint.Color = new SKColor(0, 255, 100, 15);
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawOval(px + cs * 0.3f, py + cs - 5, cs * 0.08f, 3f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 7: // Volcano: Lava-Gestein mit tiefen Rissen + Magma-Glow + Obsidian-Glanz
                    if (!isNeon)
                    {
                        // Tiefe Risse
                        ProceduralTextures.DrawCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            new SKColor(40, 25, 15), 35);
                        // Magma-Glow in den Rissen (kräftiger, immer sichtbar)
                        float pulse = MathF.Sin(_globalTimer * 1.5f + gx * 0.8f + gy) * 0.3f + 0.7f;
                        _strokePaint.Color = new SKColor(255, 120, 20, (byte)(55 * pulse));
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = null;
                        float cx1 = px + cs * 0.25f;
                        float cy1 = py + cs * 0.35f;
                        canvas.DrawLine(cx1, cy1, cx1 + cs * 0.45f, cy1 + cs * 0.35f, _strokePaint);
                        // Zweiter Riss diagonal
                        _strokePaint.Color = new SKColor(255, 80, 10, (byte)(40 * pulse));
                        float cx2 = px + cs * 0.6f;
                        canvas.DrawLine(cx2, py + 4, cx2 - cs * 0.2f, py + cs * 0.4f, _strokePaint);
                        // Obsidian-Glanz (heller Fleck)
                        _fillPaint.Color = new SKColor(255, 255, 255, 20);
                        canvas.DrawCircle(px + cs * 0.35f, py + cs * 0.25f, 2f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Leuchtende Lava-Adern (intensiver)
                        ProceduralTextures.DrawEmberCracks(canvas, _strokePaint, px + 3, py + 3, cs - 6, gx, gy,
                            _globalTimer, 35);
                        // Extra Magma-Glow
                        float pulse = MathF.Sin(_globalTimer * 1.5f + gx * 0.8f + gy) * 0.3f + 0.7f;
                        _fillPaint.Color = new SKColor(255, 80, 0, (byte)(15 * pulse));
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawOval(px + cs * 0.5f, py + cs * 0.5f, cs * 0.2f, cs * 0.15f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                    }
                    break;

                case 8: // SkyFortress: Goldverzierte Steinblöcke mit Edelstein + Relief
                    if (!isNeon)
                    {
                        // Goldener Rahmen (kräftiger)
                        _strokePaint.Color = new SKColor(200, 170, 60, 80);
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = null;
                        canvas.DrawRoundRect(px + 4, py + 4, cs - 8, cs - 8, 2, 2, _strokePaint);
                        // Goldene Eckpunkte (Nieten)
                        _fillPaint.Color = new SKColor(230, 200, 80, 120);
                        canvas.DrawCircle(px + 5, py + 5, 2f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + 5, 2f, _fillPaint);
                        canvas.DrawCircle(px + 5, py + cs - 5, 2f, _fillPaint);
                        canvas.DrawCircle(px + cs - 5, py + cs - 5, 2f, _fillPaint);
                        // Edelstein in der Mitte (blau oder rot, zufällig)
                        int gemColor = (int)(ProceduralTextures.CellRandom(gx, gy, 79) * 3);
                        var gemClr = gemColor switch
                        {
                            0 => new SKColor(80, 120, 255, 90),  // Saphir
                            1 => new SKColor(220, 60, 60, 90),   // Rubin
                            _ => new SKColor(80, 200, 120, 90)   // Smaragd
                        };
                        _fillPaint.Color = gemClr;
                        {
                            _tilePath.Rewind();
                            float gcx = px + cs * 0.5f, gcy = py + cs * 0.5f;
                            _tilePath.MoveTo(gcx, gcy - 3);
                            _tilePath.LineTo(gcx + 3, gcy);
                            _tilePath.LineTo(gcx, gcy + 3);
                            _tilePath.LineTo(gcx - 3, gcy);
                            _tilePath.Close();
                            canvas.DrawPath(_tilePath, _fillPaint);
                        }
                        // Glanz auf dem Edelstein
                        _fillPaint.Color = new SKColor(255, 255, 255, 50);
                        canvas.DrawCircle(px + cs * 0.48f, py + cs * 0.47f, 1.2f, _fillPaint);
                    }
                    else
                    {
                        // Neon: Goldener Glüh-Rahmen (kräftiger)
                        float sparkle = MathF.Sin(_globalTimer * 2.5f + gx * 1.3f + gy * 0.9f) * 0.3f + 0.7f;
                        _strokePaint.Color = new SKColor(255, 220, 80, (byte)(35 * sparkle));
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = _smallGlow;
                        canvas.DrawRoundRect(px + 4, py + 4, cs - 8, cs - 8, 2, 2, _strokePaint);
                        // Edelstein-Glow in der Mitte
                        _fillPaint.Color = new SKColor(100, 180, 255, (byte)(20 * sparkle));
                        _fillPaint.MaskFilter = _smallGlow;
                        canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, 3f, _fillPaint);
                        _fillPaint.MaskFilter = null;
                        _strokePaint.MaskFilter = null;
                    }
                    break;

                case 9: // ShadowRealm: Schattensubstanz mit lila Glow-Rissen + pulsierendem Auge
                {
                    float pulse = MathF.Sin(_globalTimer * 1.8f + gx * 1.1f + gy * 0.9f) * 0.4f + 0.6f;
                    if (isNeon)
                    {
                        // Pulsierende lila Glow-Risse (intensiver)
                        _strokePaint.Color = new SKColor(180, 40, 255, (byte)(45 * pulse));
                        _strokePaint.StrokeWidth = 1.2f;
                        _strokePaint.MaskFilter = _smallGlow;
                        float crx = px + ProceduralTextures.CellRandom(gx, gy, 72) * cs * 0.3f + cs * 0.2f;
                        canvas.DrawLine(crx, py + 4, crx + cs * 0.3f, py + cs - 4, _strokePaint);
                        // Zweiter Riss
                        float crx2 = px + cs * 0.6f;
                        canvas.DrawLine(crx2, py + cs * 0.3f, crx2 - cs * 0.15f, py + cs * 0.7f, _strokePaint);
                        _strokePaint.MaskFilter = null;
                    }
                    else
                    {
                        // Dunkler pulsierender Kern (intensiver)
                        _fillPaint.Color = new SKColor(30, 10, 50, (byte)(35 * pulse));
                        canvas.DrawRect(px + 3, py + 3, cs - 6, cs - 6, _fillPaint);
                        // Lila Risse (kräftiger, immer sichtbar)
                        _strokePaint.Color = new SKColor(150, 60, 220, (byte)(50 * pulse));
                        _strokePaint.StrokeWidth = 0.8f;
                        _strokePaint.MaskFilter = null;
                        float sx = px + cs * 0.2f;
                        float sy = py + cs * 0.3f;
                        canvas.DrawLine(sx, sy, sx + cs * 0.5f, sy + cs * 0.4f, _strokePaint);
                        canvas.DrawLine(sx + cs * 0.35f, py + 4, sx + cs * 0.2f, sy + cs * 0.15f, _strokePaint);
                        // Pulsierendes Auge (auf 25% der Blöcke)
                        if (ProceduralTextures.CellRandom(gx, gy, 80) < 0.25f)
                        {
                            float eyeX = px + cs * 0.5f, eyeY = py + cs * 0.5f;
                            _fillPaint.Color = new SKColor(160, 40, 200, (byte)(50 * pulse));
                            canvas.DrawOval(eyeX, eyeY, cs * 0.12f, cs * 0.08f, _fillPaint);
                            // Pupille
                            _fillPaint.Color = new SKColor(255, 255, 100, (byte)(40 * pulse));
                            canvas.DrawCircle(eyeX, eyeY, 1.5f, _fillPaint);
                        }
                    }
                    break;
                }
            }
        }
    }

    private void RenderBlockDestruction(SKCanvas canvas, float px, float py, int cs, float progress, bool isNeon)
    {
        _fillPaint.MaskFilter = null;
        float cx = px + cs * 0.5f;
        float cy = py + cs * 0.5f;

        //  (0-0.3): Risse erscheinen, Block vibriert
        //  (0.3-0.7): Block zerbricht, Fragmente fliegen
        //  (0.7-1.0): Fragmente verblassen

        if (progress < 0.3f)
        {
            // : Block mit zunehmenden Rissen + Vibration
            float p1 = progress / 0.3f; // 0→1
            float vibrate = MathF.Sin(p1 * 40f) * p1 * 2f;
            byte alpha = 255;

            _fillPaint.Color = _palette.BlockBase.WithAlpha(alpha);
            canvas.DrawRect(px + vibrate, py, cs, cs, _fillPaint);

            // Risse werden stärker
            _strokePaint.Color = new SKColor(40, 30, 20, (byte)(180 * p1));
            _strokePaint.StrokeWidth = 1f + p1;
            // Diagonaler Hauptriss
            canvas.DrawLine(px + cs * 0.2f + vibrate, py + cs * 0.1f,
                px + cs * 0.8f + vibrate, py + cs * 0.9f, _strokePaint);
            // Querriss
            if (p1 > 0.4f)
            {
                canvas.DrawLine(px + cs * 0.1f + vibrate, py + cs * 0.6f,
                    px + cs * 0.7f + vibrate, py + cs * 0.3f, _strokePaint);
            }
            // Dritter Riss
            if (p1 > 0.7f)
            {
                canvas.DrawLine(px + cs * 0.5f + vibrate, py,
                    px + cs * 0.3f + vibrate, py + cs, _strokePaint);
            }
            _strokePaint.StrokeWidth = 1f;
        }
        else if (progress < 0.7f)
        {
            // : 4 Fragmente fliegen auseinander + rotieren
            float p2 = (progress - 0.3f) / 0.4f; // 0→1
            byte alpha = (byte)(255 * (1f - p2 * 0.6f));
            float spread = p2 * cs * 0.5f;
            float halfCs = cs * 0.5f;
            float fragSize = halfCs * (1f - p2 * 0.3f);

            // 4 Fragmente (oben-links, oben-rechts, unten-links, unten-rechts).
            // Multiplikatoren sind static readonly (BlockFragSpreadMulX/Y, BlockFragRotMul).
            for (int i = 0; i < 4; i++)
            {
                canvas.Save();
                float fx = cx + BlockFragSpreadMulX[i] * spread;
                float fy = cy + BlockFragSpreadMulY[i] * spread;
                canvas.Translate(fx, fy);
                canvas.RotateDegrees(BlockFragRotMul[i] * p2);

                _fillPaint.Color = _palette.BlockBase.WithAlpha(alpha);
                canvas.DrawRect(-fragSize * 0.5f, -fragSize * 0.5f, fragSize, fragSize, _fillPaint);

                // Highlight-Kante auf Fragment
                byte hAlpha = (byte)(100 * (1f - p2));
                _fillPaint.Color = _palette.BlockHighlight.WithAlpha(hAlpha);
                canvas.DrawRect(-fragSize * 0.5f, -fragSize * 0.5f, fragSize, 1.5f, _fillPaint);

                canvas.Restore();
            }

            // Staubwolke in der Mitte
            byte dustAlpha = (byte)(60 * (1f - p2));
            _fillPaint.Color = new SKColor(180, 170, 150, dustAlpha);
            float dustR = cs * 0.3f + p2 * cs * 0.4f;
            canvas.DrawOval(cx, cy, dustR, dustR * 0.6f, _fillPaint);
        }
        else
        {
            // : Kleine Trümmer verblassen + fallen
            float p3 = (progress - 0.7f) / 0.3f; // 0→1
            byte alpha = (byte)(100 * (1f - p3));
            if (alpha < 5) return;

            float gravity = p3 * cs * 0.4f;
            float spread = cs * 0.5f + p3 * cs * 0.3f;

            // 6 kleine Trümmer-Punkte
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + 15f;
                float rad = angle * MathF.PI / 180f;
                float tx = cx + MathF.Cos(rad) * spread * (0.5f + i * 0.1f);
                float ty = cy + MathF.Sin(rad) * spread * 0.4f + gravity;
                float size = 2f - p3 * 1.5f;
                if (size < 0.5f) continue;

                _fillPaint.Color = _palette.BlockBase.WithAlpha(alpha);
                canvas.DrawRect(tx - size, ty - size, size * 2, size * 2, _fillPaint);
            }

            // Verblassende Staubwolke
            byte dustAlpha = (byte)(40 * (1f - p3));
            _fillPaint.Color = new SKColor(180, 170, 150, dustAlpha);
            canvas.DrawOval(cx, cy + gravity * 0.3f, cs * 0.5f, cs * 0.2f, _fillPaint);
        }

        if (isNeon)
        {
            // Neon: Energie-Burst bei Zerstörung
            float burstAlpha = progress < 0.5f ? (1f - progress * 2f) : 0f;
            if (burstAlpha > 0.05f)
            {
                _glowPaint.Color = _palette.BlockMortar.WithAlpha((byte)(120 * burstAlpha));
                _glowPaint.MaskFilter = _mediumGlow;
                float burstR = cs * 0.4f + progress * cs * 0.6f;
                canvas.DrawCircle(cx, cy, burstR, _glowPaint);
                _glowPaint.MaskFilter = null;
            }
        }
    }
}
