using BomberBlast.Models;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

// v2.0.37 (Plan Task 2.1): Aus GameRenderer.Grid.cs extrahiert.
// Enthaelt Spezial-Bomben-Zelleffekte (Eis/Lava/Smoke/Poison/Gravity/TimeWarp/BlackHole).
// Felder + Render-Orchestrierung bleiben in Grid.cs.
public sealed partial class GameRenderer
{
    /// <summary>
    /// Spezial-Bomben-Zelleffekte: Eingefrorene Zellen (Eis-Bombe) und Lava-Zellen (Feuer-Bombe).
    /// Iteriert über die Spezialeffekt-Dirty-Liste der GameEngine statt über alle ~150 Zellen
    /// (Early-Out bei 0 aktiven Zellen, der Normalfall). Jede Zelle in der Liste hat garantiert
    /// mindestens einen aktiven Effekt (siehe GameEngine.UpdateSpecialBombEffects). Fällt auf einen
    /// Voll-Grid-Scan zurück, falls keine Dirty-Liste übergeben wurde (Defensiv-Fallback).
    /// </summary>
    private void RenderSpecialBombCellEffects(SKCanvas canvas, GameGrid grid, List<Cell>? specialEffectCells)
    {
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        if (specialEffectCells != null)
        {
            // Schneller Pfad: nur Zellen mit aktivem Spezial-Effekt (Dirty-Liste).
            for (int i = 0; i < specialEffectCells.Count; i++)
            {
                var cell = specialEffectCells[i];
                RenderSpecialBombCell(canvas, cell, cell.X, cell.Y, isNeon);
            }
            return;
        }

        // Fallback: Voll-Grid-Scan (nur falls keine Dirty-Liste vorhanden).
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                RenderSpecialBombCell(canvas, grid[x, y], x, y, isNeon);
    }

    /// <summary>
    /// Spezial-Bomben-Effekte einer einzelnen Zelle rendern (Eis/Lava/Smoke/Poison/Gravity/TimeWarp/BlackHole).
    /// </summary>
    private void RenderSpecialBombCell(SKCanvas canvas, Cell cell, int x, int y, bool isNeon)
    {
        int cs = GameGrid.CELL_SIZE;
        float px = x * cs;
        float py = y * cs;

        // --- Eingefrorene Zelle (Eis-Bombe) ---
        if (cell.IsFrozen)
        {
            // Frost-Intensität basierend auf verbleibender Zeit (ausblenden am Ende)
            float frostIntensity = Math.Min(1f, cell.FreezeTimer / 0.5f); // Letzten 0.5s ausblenden

            // Halbtransparenter hellblauer Overlay
            byte iceAlpha = (byte)(90 * frostIntensity);
            _fillPaint.Color = isNeon
                ? new SKColor(0, 180, 255, iceAlpha)
                : new SKColor(160, 220, 255, iceAlpha);
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Eis-Kristall-Linien (2-3 dünne weiße Diagonalen)
            byte lineAlpha = (byte)(120 * frostIntensity);
            _strokePaint.Color = new SKColor(220, 240, 255, lineAlpha);
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
            // Diagonale 1: oben-links nach mitte
            canvas.DrawLine(px + cs * 0.15f, py + cs * 0.2f, px + cs * 0.5f, py + cs * 0.55f, _strokePaint);
            // Diagonale 2: oben-rechts nach mitte-unten
            canvas.DrawLine(px + cs * 0.8f, py + cs * 0.15f, px + cs * 0.45f, py + cs * 0.6f, _strokePaint);
            // Diagonale 3: mitte nach unten-rechts
            canvas.DrawLine(px + cs * 0.35f, py + cs * 0.45f, px + cs * 0.75f, py + cs * 0.85f, _strokePaint);
            _strokePaint.MaskFilter = null;

            // Shimmer-Effekt (pulsierender weißer Punkt)
            float shimmerPulse = (MathF.Sin(_globalTimer * 4f + x * 1.3f + y * 0.9f) + 1f) * 0.5f;
            byte shimmerAlpha = (byte)(60 * shimmerPulse * frostIntensity);
            if (shimmerAlpha > 10)
            {
                _fillPaint.Color = new SKColor(255, 255, 255, shimmerAlpha);
                _fillPaint.MaskFilter = _smallGlow;
                canvas.DrawCircle(px + cs * 0.4f + MathF.Sin(_globalTimer + x) * 3f,
                    py + cs * 0.35f + MathF.Cos(_globalTimer * 0.7f + y) * 2f,
                    2.5f, _fillPaint);
                _fillPaint.MaskFilter = null;
            }
        }

        // --- Lava-Zelle (Feuer-Bombe) ---
        if (cell.IsLavaActive)
        {
            // Lava-Intensität basierend auf verbleibender Zeit
            float lavaIntensity = Math.Min(1f, cell.LavaTimer / 0.5f);

            // Pulsierender Glow (Intensität schwankt mit sin(timer))
            float lavaPulse = 0.7f + MathF.Sin(_globalTimer * 3.5f + x * 0.8f + y * 1.2f) * 0.3f;

            // Rot-orange Overlay
            byte lavaAlpha = (byte)(120 * lavaIntensity * lavaPulse);
            _fillPaint.Color = isNeon
                ? new SKColor(255, 60, 0, lavaAlpha)
                : new SKColor(255, 100, 20, lavaAlpha);
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Innerer Glow (heller, pulsierend)
            byte innerAlpha = (byte)(60 * lavaIntensity * lavaPulse);
            _fillPaint.Color = new SKColor(255, 200, 50, innerAlpha);
            _fillPaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px + cs * 0.15f, py + cs * 0.15f, cs * 0.7f, cs * 0.7f, _fillPaint);
            _fillPaint.MaskFilter = null;

            // Lava-Blasen (2-3 kleine orange Kreise die periodisch erscheinen/verschwinden)
            for (int b = 0; b < 3; b++)
            {
                // Jede Blase hat eigenen Phasen-Offset
                float bubblePhase = (_globalTimer * 1.5f + b * 1.1f + x * 0.7f + y * 0.5f) % 2f;
                if (bubblePhase < 1.2f) // Sichtbar für 1.2s von 2s Zyklus
                {
                    float bubbleLife = bubblePhase / 1.2f; // 0→1
                    // Blase wächst, steigt auf, platzt
                    float bubbleSize = MathF.Sin(bubbleLife * MathF.PI) * 2.5f;
                    float bubbleX = px + cs * (0.25f + b * 0.25f) + MathF.Sin(_globalTimer * 0.8f + b) * 2f;
                    float bubbleY = py + cs * 0.7f - bubbleLife * cs * 0.3f;

                    if (bubbleSize > 0.5f)
                    {
                        byte bubbleAlpha = (byte)(150 * MathF.Sin(bubbleLife * MathF.PI) * lavaIntensity);
                        _fillPaint.Color = new SKColor(255, 160, 30, bubbleAlpha);
                        _fillPaint.MaskFilter = null;
                        canvas.DrawCircle(bubbleX, bubbleY, bubbleSize, _fillPaint);

                        // Heller Kern
                        if (bubbleSize > 1.5f)
                        {
                            _fillPaint.Color = new SKColor(255, 220, 80, (byte)(bubbleAlpha * 0.6f));
                            canvas.DrawCircle(bubbleX, bubbleY, bubbleSize * 0.5f, _fillPaint);
                        }
                    }
                }
            }
        }

        // --- Rauchwolke (Smoke-Bombe) ---
        if (cell.IsSmokeCloud)
        {
            float smokeIntensity = Math.Min(1f, cell.SmokeTimer / 0.5f);
            byte smokeAlpha = (byte)(100 * smokeIntensity);

            // Grauer halbtransparenter Nebel
            _fillPaint.Color = new SKColor(140, 140, 140, smokeAlpha);
            _fillPaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
            _fillPaint.MaskFilter = null;

            // Wirbelnde Rauchschwaden (2-3 helle Kreise)
            for (int s = 0; s < 3; s++)
            {
                float angle = _globalTimer * 0.5f + s * 2.094f + x * 0.7f;
                float sx = px + cs * 0.5f + MathF.Cos(angle) * cs * 0.2f;
                float sy = py + cs * 0.5f + MathF.Sin(angle) * cs * 0.15f;
                byte sAlpha = (byte)(50 * smokeIntensity + MathF.Sin(_globalTimer * 2f + s) * 20);
                _fillPaint.Color = new SKColor(200, 200, 200, sAlpha);
                _fillPaint.MaskFilter = null;
                canvas.DrawCircle(sx, sy, cs * 0.15f, _fillPaint);
            }
        }

        // --- Gift-Zelle (Poison-Bombe) ---
        if (cell.IsPoisoned)
        {
            float poisonIntensity = Math.Min(1f, cell.PoisonTimer / 0.5f);
            float poisonPulse = 0.7f + MathF.Sin(_globalTimer * 2.5f + x * 1.1f + y * 0.9f) * 0.3f;
            byte poisonAlpha = (byte)(80 * poisonIntensity * poisonPulse);

            // Grüner halbtransparenter Overlay
            _fillPaint.Color = new SKColor(0, 180, 0, poisonAlpha);
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Gift-Blasen
            for (int b = 0; b < 2; b++)
            {
                float bPhase = (_globalTimer * 1.2f + b * 0.8f + x * 0.5f) % 1.5f;
                float bx = px + cs * (0.3f + b * 0.4f);
                float by = py + cs * 0.8f - bPhase * cs * 0.4f;
                float bSize = MathF.Sin(bPhase / 1.5f * MathF.PI) * 2f;
                if (bSize > 0.3f)
                {
                    _fillPaint.Color = new SKColor(0, 220, 0, (byte)(100 * poisonIntensity));
                    canvas.DrawCircle(bx, by, bSize, _fillPaint);
                }
            }
        }

        // --- Gravitationsfeld (Gravity-Bombe) ---
        if (cell.IsGravityWell)
        {
            float gravIntensity = Math.Min(1f, cell.GravityTimer / 0.3f);
            byte gravAlpha = (byte)(50 * gravIntensity);

            // Violetter konzentrischer Ring
            _strokePaint.Color = new SKColor(150, 80, 220, gravAlpha);
            _strokePaint.StrokeWidth = 1f;
            _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
            float gravPulse = 0.5f + MathF.Sin(_globalTimer * 4f + x + y) * 0.3f;
            canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, cs * 0.4f * gravPulse, _strokePaint);
            _strokePaint.MaskFilter = null;
        }

        // --- Zeitverzerrung (TimeWarp-Bombe) ---
        if (cell.IsTimeWarped)
        {
            float twIntensity = Math.Min(1f, cell.TimeWarpTimer / 0.5f);
            byte twAlpha = (byte)(50 * twIntensity);

            // Blauer Schimmer
            _fillPaint.Color = new SKColor(80, 130, 255, twAlpha);
            _fillPaint.MaskFilter = _smallGlow;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);
            _fillPaint.MaskFilter = null;

            // Uhrzeiger-Kreuz (langsam rotierend)
            float twAngle = _globalTimer * 0.3f; // Langsame Rotation = Zeitverlangsamung
            _strokePaint.Color = new SKColor(150, 180, 255, (byte)(80 * twIntensity));
            _strokePaint.StrokeWidth = 0.8f;
            _strokePaint.MaskFilter = null;
            float cx = px + cs * 0.5f;
            float cy = py + cs * 0.5f;
            float handLen = cs * 0.3f;
            canvas.DrawLine(cx, cy,
                cx + MathF.Cos(twAngle) * handLen,
                cy + MathF.Sin(twAngle) * handLen, _strokePaint);
            canvas.DrawLine(cx, cy,
                cx + MathF.Cos(twAngle + MathF.PI * 0.5f) * handLen * 0.6f,
                cy + MathF.Sin(twAngle + MathF.PI * 0.5f) * handLen * 0.6f, _strokePaint);
        }

        // --- Schwarzes Loch (BlackHole-Bombe) ---
        if (cell.IsBlackHole)
        {
            float bhIntensity = Math.Min(1f, cell.BlackHoleTimer / 0.5f);

            // Dunkler Overlay
            byte bhAlpha = (byte)(80 * bhIntensity);
            _fillPaint.Color = new SKColor(20, 0, 40, bhAlpha);
            _fillPaint.MaskFilter = null;
            canvas.DrawRect(px, py, cs, cs, _fillPaint);

            // Violetter Sog-Ring (nach innen pulsierend)
            float sogPhase = (_globalTimer * 2f) % 1f;
            float sogRadius = cs * 0.4f * (1f - sogPhase * 0.5f);
            byte sogAlpha = (byte)(60 * bhIntensity * (1f - sogPhase));
            _strokePaint.Color = new SKColor(100, 0, 200, sogAlpha);
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawCircle(px + cs * 0.5f, py + cs * 0.5f, sogRadius, _strokePaint);
            _strokePaint.MaskFilter = null;
        }
    }
}
