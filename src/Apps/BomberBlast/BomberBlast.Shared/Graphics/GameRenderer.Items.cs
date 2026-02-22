using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

public partial class GameRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // EXIT
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderExit(SKCanvas canvas, GameGrid grid, Cell? exitCell)
    {
        // Gecachte Exit-Zelle nutzen statt Grid-Iteration (150 Zellen pro Frame gespart)
        if (exitCell == null || exitCell.Type != CellType.Exit)
            return;

        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float pulse = MathF.Sin(_globalTimer * 3) * 0.15f + 0.85f;

        float px = exitCell.X * GameGrid.CELL_SIZE;
        float py = exitCell.Y * GameGrid.CELL_SIZE;
        int cs = GameGrid.CELL_SIZE;
        float cx = px + cs / 2f;
        float cy = py + cs / 2f;
        float portalRadius = cs * 0.4f;

        // Äußerer Glow-Ring (pulsierend, goldfarben)
        _glowPaint.Color = _palette.ExitGlow.WithAlpha((byte)(50 * pulse));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(cx, cy, portalRadius + 4f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // 4 drehende Spiralarme
        canvas.Save();
        canvas.Translate(cx, cy);
        float rotSpeed = _globalTimer * 90f; // 90°/s
        canvas.RotateDegrees(rotSpeed);

        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        for (int arm = 0; arm < 4; arm++)
        {
            float armAngle = arm * 90f;
            canvas.Save();
            canvas.RotateDegrees(armAngle);

            // Spiralarm als QuadTo-Kurve
            byte armAlpha = (byte)(140 * pulse);
            _strokePaint.Color = _palette.ExitGlow.WithAlpha(armAlpha);
            _strokePaint.StrokeWidth = 2f;
            _fusePath.Reset();
            _fusePath.MoveTo(0, 0);
            _fusePath.QuadTo(portalRadius * 0.3f, -portalRadius * 0.5f,
                             portalRadius * 0.8f, -portalRadius * 0.15f);
            canvas.DrawPath(_fusePath, _strokePaint);

            canvas.Restore();
        }
        _strokePaint.MaskFilter = null;

        canvas.Restore(); // Rotation zurücksetzen

        // Portal-Ring (äußerer Kreis)
        _strokePaint.Color = _palette.ExitGlow.WithAlpha((byte)(180 * pulse));
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawCircle(cx, cy, portalRadius, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Innerer farbiger Kern (heller Punkt)
        _fillPaint.Color = _palette.ExitInner.WithAlpha((byte)(200 * pulse));
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(cx, cy, portalRadius * 0.35f, _fillPaint);

        // Heller Kern-Punkt (Weiß, pulsierend)
        float corePulse = MathF.Sin(_globalTimer * 5f) * 0.3f + 0.7f;
        _fillPaint.Color = SKColors.White.WithAlpha((byte)(200 * corePulse));
        canvas.DrawCircle(cx, cy, portalRadius * 0.15f, _fillPaint);

        // 5 Partikel die zum Portal gezogen werden
        for (int i = 0; i < 5; i++)
        {
            float phase = (_globalTimer * 0.7f + i * 0.628f) % 2f; // Zyklus 2s
            float dist = portalRadius * (1.5f - phase * 0.6f); // Von außen nach innen
            float angle = _globalTimer * 1.5f + i * 1.257f;
            float sparkX = cx + MathF.Cos(angle) * dist;
            float sparkY = cy + MathF.Sin(angle) * dist;
            byte sparkAlpha = (byte)(100 * (1f - phase / 2f)); // Verblasst nach innen
            float sparkSize = 1.5f * (1f - phase / 3f);
            _fillPaint.Color = _palette.ExitGlow.WithAlpha(sparkAlpha);
            canvas.DrawCircle(sparkX, sparkY, sparkSize, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOMB
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderBomb(SKCanvas canvas, Bomb bomb)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Squash/Stretch: Platzierungs-Bounce in den ersten 0.3s
        float age = Bomb.DEFAULT_FUSE_TIME - bomb.FuseTimer;
        float birthScale = 1f;
        if (age < 0.3f)
        {
            float t = age / 0.3f; // 0→1
            // Bounce: schnell groß, dann einpendeln (overshoot + settle)
            birthScale = 1f + MathF.Sin(t * MathF.PI) * 0.25f * (1f - t);
        }

        // Slide-Indikator: Leichtes Strecken in Gleitrichtung
        float stretchX = 1f, stretchY = 1f;
        if (bomb.IsSliding)
        {
            float stretch = 0.15f;
            if (bomb.SlideDirection is Direction.Left or Direction.Right)
            { stretchX = 1f + stretch; stretchY = 1f - stretch * 0.5f; }
            else
            { stretchY = 1f + stretch; stretchX = 1f - stretch * 0.5f; }
        }

        // Pulsation beschleunigt sich je näher die Explosion (4→12 Hz)
        float fuseProgress = 1f - (bomb.FuseTimer / Bomb.DEFAULT_FUSE_TIME);
        float pulseSpeed = 4f + fuseProgress * 8f;
        float pulseAmount = 0.06f + fuseProgress * 0.04f;
        float pulse = MathF.Sin(_globalTimer * pulseSpeed) * pulseAmount + (1f - pulseAmount);
        float drawSize = cs * pulse * birthScale;

        // === Typ-spezifische Farben bestimmen ===
        SKColor bombBody, bombGlow, bombFuse;
        switch (bomb.Type)
        {
            case BombType.Ice:
                bombBody = new SKColor(100, 200, 255);
                bombGlow = new SKColor(0, 200, 255);
                bombFuse = new SKColor(120, 210, 255);
                break;
            case BombType.Fire:
                bombBody = new SKColor(200, 30, 0);
                bombGlow = new SKColor(255, 100, 0);
                bombFuse = new SKColor(255, 80, 20);
                break;
            case BombType.Sticky:
                bombBody = new SKColor(50, 180, 50);
                bombGlow = new SKColor(100, 220, 50);
                bombFuse = new SKColor(80, 200, 60);
                break;
            case BombType.Smoke:
                bombBody = new SKColor(140, 140, 140);
                bombGlow = new SKColor(180, 180, 180);
                bombFuse = new SKColor(160, 160, 160);
                break;
            case BombType.Lightning:
                bombBody = new SKColor(255, 255, 100);
                bombGlow = new SKColor(255, 255, 200);
                bombFuse = new SKColor(255, 255, 150);
                break;
            case BombType.Gravity:
                bombBody = new SKColor(150, 80, 220);
                bombGlow = new SKColor(180, 100, 255);
                bombFuse = new SKColor(160, 100, 230);
                break;
            case BombType.Poison:
                bombBody = new SKColor(0, 180, 0);
                bombGlow = new SKColor(50, 220, 50);
                bombFuse = new SKColor(30, 200, 30);
                break;
            case BombType.TimeWarp:
                bombBody = new SKColor(80, 130, 255);
                bombGlow = new SKColor(100, 150, 255);
                bombFuse = new SKColor(120, 170, 255);
                break;
            case BombType.Mirror:
                bombBody = new SKColor(200, 200, 230);
                bombGlow = new SKColor(220, 220, 255);
                bombFuse = new SKColor(210, 210, 240);
                break;
            case BombType.Vortex:
                bombBody = new SKColor(130, 0, 200);
                bombGlow = new SKColor(170, 50, 255);
                bombFuse = new SKColor(148, 0, 211);
                break;
            case BombType.Phantom:
                bombBody = new SKColor(180, 200, 240);
                bombGlow = new SKColor(200, 220, 255);
                bombFuse = new SKColor(190, 210, 250);
                break;
            case BombType.Nova:
                bombBody = new SKColor(255, 200, 0);
                bombGlow = new SKColor(255, 215, 0);
                bombFuse = new SKColor(255, 230, 80);
                break;
            case BombType.BlackHole:
                bombBody = new SKColor(40, 0, 60);
                bombGlow = new SKColor(100, 0, 200);
                bombFuse = new SKColor(80, 20, 120);
                break;
            default: // Normal - Bomben-Skin verwenden falls nicht Standard
                var bSkin = _customizationService.BombSkin;
                if (bSkin.Id != "bomb_default")
                {
                    bombBody = bSkin.BodyColor;
                    bombGlow = bSkin.GlowColor;
                    bombFuse = bSkin.FuseColor;
                }
                else
                {
                    bombBody = _palette.BombBody;
                    bombGlow = _palette.BombGlowColor;
                    bombFuse = _palette.BombFuse;
                }
                break;
        }

        // Spezial-Bomben: Partikel-Effekte um die Bombe herum
        if (bomb.Type != BombType.Normal)
            RenderBombTypeParticles(canvas, bomb, cs, drawSize);

        // Glow beschleunigt und intensiviert sich
        float glowSpeed = 3f + fuseProgress * 6f;
        float glowPulse = MathF.Sin(_globalTimer * glowSpeed) * 0.3f + 0.5f;
        byte glowAlpha = (byte)(80 + fuseProgress * 80);
        _glowPaint.Color = bombGlow.WithAlpha((byte)(glowAlpha * glowPulse));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(bomb.X, bomb.Y, drawSize * 0.5f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Bomb body (glossy sphere mit Squash/Stretch)
        float radius = drawSize * 0.38f;
        _fillPaint.Color = bombBody;
        _fillPaint.MaskFilter = null;
        if (bomb.IsSliding)
        {
            canvas.DrawOval(bomb.X, bomb.Y, radius * stretchX, radius * stretchY, _fillPaint);
        }
        else
        {
            canvas.DrawCircle(bomb.X, bomb.Y, radius, _fillPaint);
        }

        // Gloss highlight (top-left) - Bomben-Skin Highlight wenn nicht Standard
        var hlSkin = _customizationService.BombSkin;
        var hlColor = (bomb.Type == BombType.Normal && hlSkin.Id != "bomb_default")
            ? hlSkin.HighlightColor
            : _palette.BombHighlight.WithAlpha(120);
        _fillPaint.Color = hlColor;
        canvas.DrawCircle(bomb.X - radius * 0.3f, bomb.Y - radius * 0.3f, radius * 0.25f, _fillPaint);

        // === Zündschnur: Wird kürzer je näher an Explosion ===
        float fuseLength = 1f - fuseProgress * 0.7f; // 1.0 → 0.3 (Schnur wird kürzer)
        float fuseEndX = bomb.X + 8f * fuseLength;
        float fuseEndY = bomb.Y - radius - 4f * fuseLength - 4f;
        float fuseMidX = bomb.X + 5f * fuseLength;
        float fuseMidY = bomb.Y - radius - 8f * fuseLength;

        // Schnur-Farbe intensiviert sich
        SKColor fuseColor = bomb.IsAboutToExplode ? SKColors.Red : bombFuse;
        _strokePaint.Color = fuseColor;
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        _fusePath.Reset();
        _fusePath.MoveTo(bomb.X, bomb.Y - radius);
        _fusePath.QuadTo(fuseMidX, fuseMidY, fuseEndX, fuseEndY);
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Kleine Funken-Punkte entlang der Schnur (2-3 wandernde Punkte)
        for (int i = 0; i < 3; i++)
        {
            float t = ((_globalTimer * 3f + i * 0.33f) % 1f) * fuseLength;
            // Quadratische Bezier-Position auf der Schnur
            float oneMinusT = 1f - t;
            float sparkOnFuseX = oneMinusT * oneMinusT * bomb.X + 2 * oneMinusT * t * fuseMidX + t * t * fuseEndX;
            float sparkOnFuseY = oneMinusT * oneMinusT * (bomb.Y - radius) + 2 * oneMinusT * t * fuseMidY + t * t * fuseEndY;
            byte sparkAlpha = (byte)(100 + MathF.Sin(_globalTimer * 8f + i * 2f) * 60);
            _fillPaint.Color = new SKColor(255, 220, 100, sparkAlpha);
            _fillPaint.MaskFilter = null;
            canvas.DrawCircle(sparkOnFuseX, sparkOnFuseY, 1.2f, _fillPaint);
        }

        // Funken-Farben nach Typ bestimmen
        SKColor sparkGlow, sparkCore;
        switch (bomb.Type)
        {
            case BombType.Ice:
                sparkGlow = new SKColor(100, 220, 255, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(200, 240, 255);
                break;
            case BombType.Fire:
                sparkGlow = new SKColor(255, 120, 0, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(255, 200, 50);
                break;
            case BombType.Sticky:
                sparkGlow = new SKColor(120, 255, 60, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(180, 255, 100);
                break;
            case BombType.Smoke:
                sparkGlow = new SKColor(180, 180, 180, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(220, 220, 220);
                break;
            case BombType.Lightning:
                sparkGlow = new SKColor(255, 255, 150, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(255, 255, 255);
                break;
            case BombType.Gravity:
                sparkGlow = new SKColor(180, 100, 255, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(220, 180, 255);
                break;
            case BombType.Poison:
                sparkGlow = new SKColor(50, 220, 50, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(100, 255, 100);
                break;
            case BombType.TimeWarp:
                sparkGlow = new SKColor(100, 150, 255, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(180, 200, 255);
                break;
            case BombType.Mirror:
                sparkGlow = new SKColor(220, 220, 255, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(255, 255, 255);
                break;
            case BombType.Vortex:
                sparkGlow = new SKColor(170, 50, 255, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(220, 150, 255);
                break;
            case BombType.Phantom:
                sparkGlow = new SKColor(200, 220, 255, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(230, 240, 255);
                break;
            case BombType.Nova:
                sparkGlow = new SKColor(255, 215, 0, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(255, 255, 200);
                break;
            case BombType.BlackHole:
                sparkGlow = new SKColor(100, 0, 200, (byte)(80 + fuseProgress * 80));
                sparkCore = new SKColor(200, 100, 255);
                break;
            default:
                var bsSkin = _customizationService.BombSkin;
                if (bsSkin.Id != "bomb_default")
                {
                    sparkGlow = bsSkin.SparkColor.WithAlpha((byte)(80 + fuseProgress * 80));
                    sparkCore = bsSkin.SparkColor;
                }
                else
                {
                    sparkGlow = new SKColor(255, 180, 50, (byte)(80 + fuseProgress * 80));
                    sparkCore = isNeon ? new SKColor(255, 200, 100) : SKColors.Yellow;
                }
                break;
        }

        // Stern-Effekt am Schnur-Ende (4-zackiger Stern statt einfacher Punkt)
        float starSize = 4f + fuseProgress * 2f;
        float starFlicker = MathF.Sin(_globalTimer * 15f) * 0.3f + 0.7f;

        // Glow-Halo um den Stern
        _glowPaint.Color = sparkGlow;
        _glowPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(fuseEndX, fuseEndY, starSize + 2f, _glowPaint);
        _glowPaint.MaskFilter = null;

        // 4-zackiger Stern
        _fillPaint.Color = sparkCore;
        _fillPaint.MaskFilter = isNeon ? _smallGlow : null;
        float sRot = _globalTimer * 200f; // Schnelle Rotation
        canvas.Save();
        canvas.Translate(fuseEndX, fuseEndY);
        canvas.RotateDegrees(sRot);
        _fusePath.Reset();
        float sOuter = starSize * starFlicker;
        float sInner = sOuter * 0.35f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4f;
            float r = (i % 2 == 0) ? sOuter : sInner;
            float sx = MathF.Cos(a) * r;
            float sy = MathF.Sin(a) * r;
            if (i == 0) _fusePath.MoveTo(sx, sy);
            else _fusePath.LineTo(sx, sy);
        }
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
        canvas.Restore();
        _fillPaint.MaskFilter = null;

        // 2-3 aufsteigende Rauch-Partikel vom Funken-Ende
        for (int i = 0; i < 3; i++)
        {
            float smokePhase = (_globalTimer * 1.5f + i * 0.4f) % 1.2f;
            float smokeY = fuseEndY - smokePhase * 8f; // Steigt auf
            float smokeX = fuseEndX + MathF.Sin(_globalTimer * 2f + i) * 2f; // Leichtes Wackeln
            byte smokeAlpha = (byte)(60 * (1f - smokePhase / 1.2f));
            float smokeSize = 1.5f + smokePhase * 2f; // Wird größer beim Aufsteigen
            _fillPaint.Color = new SKColor(180, 180, 180, smokeAlpha);
            _fillPaint.MaskFilter = null;
            canvas.DrawCircle(smokeX, smokeY, smokeSize, _fillPaint);
        }
    }

    /// <summary>
    /// Typ-spezifische Partikel-Effekte um Spezial-Bomben
    /// </summary>
    private void RenderBombTypeParticles(SKCanvas canvas, Bomb bomb, float cs, float drawSize)
    {
        float radius = drawSize * 0.38f;

        switch (bomb.Type)
        {
            case BombType.Ice:
            {
                // Frost-Partikel: Kleine blaue Punkte die langsam aufsteigen
                for (int i = 0; i < 5; i++)
                {
                    float angle = _globalTimer * 1.5f + i * 1.257f; // 2*PI/5 Abstand
                    float dist = radius * 0.8f + MathF.Sin(_globalTimer * 2f + i) * 4f;
                    float px = bomb.X + MathF.Cos(angle) * dist;
                    // Aufsteigende Bewegung
                    float py = bomb.Y + MathF.Sin(angle) * dist - MathF.Abs(MathF.Sin(_globalTimer * 1.2f + i * 0.7f)) * 6f;
                    byte alpha = (byte)(80 + MathF.Sin(_globalTimer * 3f + i) * 40);
                    _fillPaint.Color = new SKColor(180, 230, 255, alpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawCircle(px, py, 1.5f, _fillPaint);
                }

                // Kleiner Eisschimmer-Ring
                _strokePaint.Color = new SKColor(150, 220, 255, 50);
                _strokePaint.StrokeWidth = 1f;
                _strokePaint.MaskFilter = _smallGlow;
                canvas.DrawCircle(bomb.X, bomb.Y, radius * 1.2f, _strokePaint);
                _strokePaint.MaskFilter = null;
                break;
            }
            case BombType.Fire:
            {
                // Flammen-Partikel: Kleine rot-orange Punkte die aufsteigen
                for (int i = 0; i < 6; i++)
                {
                    float angle = _globalTimer * 2.5f + i * 1.047f; // 2*PI/6 Abstand
                    float dist = radius * 0.7f + MathF.Sin(_globalTimer * 3f + i * 0.8f) * 5f;
                    float px = bomb.X + MathF.Cos(angle) * dist;
                    // Aufsteigend mit Flacker-Effekt
                    float py = bomb.Y + MathF.Sin(angle) * dist * 0.6f - MathF.Abs(MathF.Sin(_globalTimer * 2f + i)) * 8f;
                    byte alpha = (byte)(100 + MathF.Sin(_globalTimer * 4f + i * 1.3f) * 50);
                    // Abwechselnd rot und orange
                    var color = i % 2 == 0
                        ? new SKColor(255, 80, 20, alpha)
                        : new SKColor(255, 160, 30, alpha);
                    _fillPaint.Color = color;
                    _fillPaint.MaskFilter = null;
                    canvas.DrawCircle(px, py, 1.8f, _fillPaint);
                }

                // Flammender Glow-Ring
                _strokePaint.Color = new SKColor(255, 80, 0, 40);
                _strokePaint.StrokeWidth = 1.5f;
                _strokePaint.MaskFilter = _smallGlow;
                float fireRingPulse = 1f + MathF.Sin(_globalTimer * 6f) * 0.08f;
                canvas.DrawCircle(bomb.X, bomb.Y, radius * 1.3f * fireRingPulse, _strokePaint);
                _strokePaint.MaskFilter = null;
                break;
            }
            case BombType.Sticky:
            {
                // Schleim-Tropfen an der Unterseite der Bombe
                for (int i = 0; i < 4; i++)
                {
                    float offsetX = (i - 1.5f) * radius * 0.4f;
                    // Tropfen hängen herunter und tropfen periodisch
                    float dripPhase = (_globalTimer * 0.8f + i * 0.6f) % 2f;
                    float dripY = radius * 0.6f;
                    float dripSize = 2.5f;
                    if (dripPhase < 1.2f)
                    {
                        // Tropfen wächst und hängt herunter
                        dripY += dripPhase * 3f;
                        dripSize = 2f + dripPhase * 0.8f;
                    }
                    else
                    {
                        // Tropfen fällt und wird kleiner
                        float fallProgress = (dripPhase - 1.2f) / 0.8f;
                        dripY += 3.6f + fallProgress * 6f;
                        dripSize = 2.8f * (1f - fallProgress);
                    }

                    if (dripSize > 0.3f)
                    {
                        byte alpha = (byte)(140 + MathF.Sin(_globalTimer + i) * 30);
                        _fillPaint.Color = new SKColor(60, 200, 60, alpha);
                        _fillPaint.MaskFilter = null;
                        canvas.DrawCircle(bomb.X + offsetX, bomb.Y + dripY, dripSize, _fillPaint);
                    }
                }

                // Schleim-Fäden (dünne grüne Linien von Bombe nach unten)
                _strokePaint.Color = new SKColor(80, 210, 80, 60);
                _strokePaint.StrokeWidth = 0.8f;
                _strokePaint.MaskFilter = null;
                for (int i = 0; i < 3; i++)
                {
                    float offsetX = (i - 1) * radius * 0.35f;
                    canvas.DrawLine(bomb.X + offsetX, bomb.Y + radius * 0.4f,
                        bomb.X + offsetX + MathF.Sin(_globalTimer + i) * 2f, bomb.Y + radius * 0.9f, _strokePaint);
                }
                break;
            }
            case BombType.Smoke:
            {
                // Aufsteigende graue Rauchschwaden
                for (int i = 0; i < 5; i++)
                {
                    float angle = _globalTimer * 0.8f + i * 1.257f;
                    float dist = radius * 1.0f + MathF.Sin(_globalTimer * 1.5f + i) * 5f;
                    float px = bomb.X + MathF.Cos(angle) * dist * 0.6f;
                    float py = bomb.Y - MathF.Abs(MathF.Sin(_globalTimer * 0.7f + i * 0.5f)) * 10f - 3f;
                    byte alpha = (byte)(60 + MathF.Sin(_globalTimer * 2f + i) * 30);
                    _fillPaint.Color = new SKColor(160, 160, 160, alpha);
                    _fillPaint.MaskFilter = _smallGlow;
                    canvas.DrawCircle(px, py, 2.5f + MathF.Sin(_globalTimer + i) * 0.5f, _fillPaint);
                    _fillPaint.MaskFilter = null;
                }
                break;
            }
            case BombType.Lightning:
            {
                // Elektrische Blitze um die Bombe (zuckende Linien)
                _strokePaint.StrokeWidth = 1.2f;
                _strokePaint.MaskFilter = _smallGlow;
                for (int i = 0; i < 4; i++)
                {
                    float angle = _globalTimer * 4f + i * 1.571f; // PI/2
                    byte alpha = (byte)(120 + MathF.Sin(_globalTimer * 8f + i * 2f) * 80);
                    _strokePaint.Color = new SKColor(255, 255, 100, alpha);
                    float startX = bomb.X + MathF.Cos(angle) * radius * 0.5f;
                    float startY = bomb.Y + MathF.Sin(angle) * radius * 0.5f;
                    float endX = bomb.X + MathF.Cos(angle) * radius * 1.4f;
                    float endY = bomb.Y + MathF.Sin(angle) * radius * 1.4f;
                    float midX = (startX + endX) / 2f + MathF.Sin(_globalTimer * 12f + i) * 3f;
                    float midY = (startY + endY) / 2f + MathF.Cos(_globalTimer * 10f + i) * 3f;
                    canvas.DrawLine(startX, startY, midX, midY, _strokePaint);
                    canvas.DrawLine(midX, midY, endX, endY, _strokePaint);
                }
                _strokePaint.MaskFilter = null;
                break;
            }
            case BombType.Gravity:
            {
                // Rotierende violette Orbital-Punkte
                for (int i = 0; i < 6; i++)
                {
                    float angle = _globalTimer * 3f + i * 1.047f;
                    float dist = radius * 1.1f + MathF.Sin(_globalTimer * 2f + i) * 2f;
                    float px = bomb.X + MathF.Cos(angle) * dist;
                    float py = bomb.Y + MathF.Sin(angle) * dist;
                    byte alpha = (byte)(100 + MathF.Sin(_globalTimer * 3f + i) * 40);
                    _fillPaint.Color = new SKColor(180, 100, 255, alpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawCircle(px, py, 1.5f, _fillPaint);
                }
                // Gravitationsfeld-Ring (pulsierend)
                _strokePaint.Color = new SKColor(150, 80, 220, 40);
                _strokePaint.StrokeWidth = 1f;
                _strokePaint.MaskFilter = _smallGlow;
                float gPulse = 1f + MathF.Sin(_globalTimer * 4f) * 0.1f;
                canvas.DrawCircle(bomb.X, bomb.Y, radius * 1.5f * gPulse, _strokePaint);
                _strokePaint.MaskFilter = null;
                break;
            }
            case BombType.Poison:
            {
                // Gift-Blasen die aufsteigen und platzen
                for (int i = 0; i < 4; i++)
                {
                    float phase = (_globalTimer * 0.6f + i * 0.7f) % 1.5f;
                    float offsetX = (i - 1.5f) * radius * 0.3f;
                    float py = bomb.Y - phase * 8f;
                    float size = 1.5f + MathF.Sin(phase * MathF.PI) * 1f;
                    byte alpha = (byte)(120 - phase * 50);
                    _fillPaint.Color = new SKColor(0, 200, 0, alpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawCircle(bomb.X + offsetX, py, size, _fillPaint);
                }
                break;
            }
            case BombType.TimeWarp:
            {
                // Uhrzeiger-ähnliche rotierende Linien
                _strokePaint.StrokeWidth = 1f;
                _strokePaint.MaskFilter = _smallGlow;
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * MathF.PI / 6f;
                    byte alpha = (byte)(40 + (i == (int)(_globalTimer * 2f) % 12 ? 120 : 0));
                    _strokePaint.Color = new SKColor(100, 150, 255, alpha);
                    float startDist = radius * 0.9f;
                    float endDist = radius * 1.2f;
                    canvas.DrawLine(
                        bomb.X + MathF.Cos(angle) * startDist,
                        bomb.Y + MathF.Sin(angle) * startDist,
                        bomb.X + MathF.Cos(angle) * endDist,
                        bomb.Y + MathF.Sin(angle) * endDist,
                        _strokePaint);
                }
                _strokePaint.MaskFilter = null;
                break;
            }
            case BombType.Mirror:
            {
                // Spiegel-Reflexions-Effekt (schimmernde Punkte)
                for (int i = 0; i < 4; i++)
                {
                    float angle = _globalTimer * 1.5f + i * 1.571f;
                    float px = bomb.X + MathF.Cos(angle) * radius * 1.1f;
                    float py = bomb.Y + MathF.Sin(angle) * radius * 1.1f;
                    byte alpha = (byte)(100 + MathF.Sin(_globalTimer * 5f + i) * 80);
                    _fillPaint.Color = new SKColor(255, 255, 255, alpha);
                    _fillPaint.MaskFilter = _smallGlow;
                    canvas.DrawCircle(px, py, 1.8f, _fillPaint);
                    _fillPaint.MaskFilter = null;
                }
                break;
            }
            case BombType.Vortex:
            {
                // Spiralförmige Partikel die sich einwärts drehen
                for (int i = 0; i < 8; i++)
                {
                    float t = (float)i / 8f;
                    float spiralAngle = _globalTimer * 5f + t * MathF.PI * 4f;
                    float dist = radius * (0.5f + t * 0.8f);
                    float px = bomb.X + MathF.Cos(spiralAngle) * dist;
                    float py = bomb.Y + MathF.Sin(spiralAngle) * dist;
                    byte alpha = (byte)(60 + t * 120);
                    _fillPaint.Color = new SKColor(148, 0, 211, alpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawCircle(px, py, 1.2f + t * 0.5f, _fillPaint);
                }
                break;
            }
            case BombType.Phantom:
            {
                // Geister-hafte transparente Duplikate
                for (int i = 0; i < 3; i++)
                {
                    float offset = MathF.Sin(_globalTimer * 2f + i * 2.094f) * 4f;
                    byte alpha = (byte)(30 + MathF.Sin(_globalTimer * 3f + i) * 20);
                    _fillPaint.Color = new SKColor(200, 220, 255, alpha);
                    _fillPaint.MaskFilter = _smallGlow;
                    canvas.DrawCircle(bomb.X + offset, bomb.Y + MathF.Cos(_globalTimer * 1.5f + i) * 3f,
                        radius * 0.6f, _fillPaint);
                    _fillPaint.MaskFilter = null;
                }
                break;
            }
            case BombType.Nova:
            {
                // Goldener pulsierender Strahlenkranz
                _strokePaint.StrokeWidth = 1.5f;
                _strokePaint.MaskFilter = _smallGlow;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * MathF.PI / 4f + _globalTimer * 1.5f;
                    float rayLength = radius * (1.2f + MathF.Sin(_globalTimer * 4f + i * 0.8f) * 0.3f);
                    byte alpha = (byte)(100 + MathF.Sin(_globalTimer * 3f + i) * 60);
                    _strokePaint.Color = new SKColor(255, 215, 0, alpha);
                    canvas.DrawLine(
                        bomb.X + MathF.Cos(angle) * radius * 0.5f,
                        bomb.Y + MathF.Sin(angle) * radius * 0.5f,
                        bomb.X + MathF.Cos(angle) * rayLength,
                        bomb.Y + MathF.Sin(angle) * rayLength,
                        _strokePaint);
                }
                _strokePaint.MaskFilter = null;
                break;
            }
            case BombType.BlackHole:
            {
                // Dunkler Sog-Effekt: Partikel die zum Zentrum gezogen werden
                for (int i = 0; i < 6; i++)
                {
                    float phase = (_globalTimer * 1.2f + i * 0.5f) % 1f;
                    float angle = i * 1.047f + _globalTimer * 2f;
                    float dist = radius * (1.5f - phase * 1.2f); // Von außen nach innen
                    float px = bomb.X + MathF.Cos(angle) * dist;
                    float py = bomb.Y + MathF.Sin(angle) * dist;
                    byte alpha = (byte)(40 + phase * 120);
                    _fillPaint.Color = new SKColor(100, 0, 200, alpha);
                    _fillPaint.MaskFilter = null;
                    canvas.DrawCircle(px, py, 1.5f * (1f - phase), _fillPaint);
                }
                // Dunkler Kern-Ring
                _strokePaint.Color = new SKColor(30, 0, 60, 80);
                _strokePaint.StrokeWidth = 2f;
                _strokePaint.MaskFilter = _smallGlow;
                canvas.DrawCircle(bomb.X, bomb.Y, radius * 0.9f, _strokePaint);
                _strokePaint.MaskFilter = null;
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXPLOSION
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderExplosion(SKCanvas canvas, Explosion explosion)
    {
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float progress = 1f - (explosion.Timer / Explosion.DURATION); // 0→1
        float alpha = 1f - progress * 0.3f;
        float cs = GameGrid.CELL_SIZE;

        // Shockwave-Ring (expandierender Kreis in den ersten 40%) - doppelter Ring
        if (progress < 0.4f && explosion.SourceBomb != null)
        {
            float shockProgress = progress / 0.4f;
            float maxRadius = explosion.SourceBomb.Range * cs;
            float radius = shockProgress * maxRadius;
            float shockAlpha = (1f - shockProgress) * 0.5f;

            float centerX = explosion.X + cs / 2f;
            float centerY = explosion.Y + cs / 2f;

            // Äußerer Ring (breit, diffus)
            _strokePaint.Color = _explOuter.WithAlpha((byte)(120 * shockAlpha));
            _strokePaint.StrokeWidth = 3f + (1f - shockProgress) * 3f;
            _strokePaint.MaskFilter = _mediumGlow;
            canvas.DrawCircle(centerX, centerY, radius, _strokePaint);

            // Innerer Ring (dünn, hell)
            _strokePaint.Color = _explCore.WithAlpha((byte)(255 * shockAlpha));
            _strokePaint.StrokeWidth = 1.5f + (1f - shockProgress) * 1.5f;
            _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
            canvas.DrawCircle(centerX, centerY, radius * 0.85f, _strokePaint);
            _strokePaint.MaskFilter = null;
        }

        // Envelope berechnen (einmal für alle Arme)
        float envelope = ExplosionShaders.CalculateEnvelope(progress, alpha);
        if (envelope < 0.01f) return;

        _fillPaint.MaskFilter = null; // Sauberer State

        // Center-Punkt (Pixel-Mitte der Center-Zelle)
        float cx = explosion.GridX * cs + cs / 2f;
        float cy = explosion.GridY * cs + cs / 2f;

        // Arm-Längen berechnen: Wie viele Zellen in jede Richtung?
        int armLeft = 0, armRight = 0, armUp = 0, armDown = 0;
        foreach (var cell in explosion.AffectedCells)
        {
            int relX = cell.X - explosion.GridX;
            int relY = cell.Y - explosion.GridY;

            if (relX < 0 && relY == 0) armLeft = Math.Max(armLeft, -relX);
            if (relX > 0 && relY == 0) armRight = Math.Max(armRight, relX);
            if (relY < 0 && relX == 0) armUp = Math.Max(armUp, -relY);
            if (relY > 0 && relX == 0) armDown = Math.Max(armDown, relY);
        }

        // Arme als durchgehende Flammenstreifen rendern (keine Zell-Grenzen sichtbar)
        if (armLeft > 0)
            ExplosionShaders.DrawFlameArm(canvas, cx, cy, armLeft, -1, 0, cs,
                _globalTimer, _explOuter, _explInner, _explCore, envelope);
        if (armRight > 0)
            ExplosionShaders.DrawFlameArm(canvas, cx, cy, armRight, 1, 0, cs,
                _globalTimer, _explOuter, _explInner, _explCore, envelope);
        if (armUp > 0)
            ExplosionShaders.DrawFlameArm(canvas, cx, cy, armUp, 0, -1, cs,
                _globalTimer, _explOuter, _explInner, _explCore, envelope);
        if (armDown > 0)
            ExplosionShaders.DrawFlameArm(canvas, cx, cy, armDown, 0, 1, cs,
                _globalTimer, _explOuter, _explInner, _explCore, envelope);

        // Center-Feuerball (über den Armen, damit er die Übergänge verdeckt)
        ExplosionShaders.DrawCenterFire(canvas, cx, cy, cs,
            _globalTimer, _explOuter, _explInner, _explCore, envelope);

        // Wärme-Distortion (Heat Haze) über dem gesamten Explosionsbereich
        if (progress > 0.1f)
        {
            // Bounding-Box aller Explosions-Zellen berechnen
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var cell in explosion.AffectedCells)
            {
                float px = cell.X * cs;
                float py = cell.Y * cs;
                if (px < minX) minX = px;
                if (py < minY) minY = py;
                if (px + cs > maxX) maxX = px + cs;
                if (py + cs > maxY) maxY = py + cs;
            }

            // Heat Haze reicht über die Explosion hinaus (nach oben mehr)
            float hazeExpand = cs * 0.5f;
            var hazeRect = new SKRect(
                minX - hazeExpand, minY - cs, // Mehr Platz nach oben (Hitze steigt auf)
                maxX + hazeExpand, maxY + hazeExpand);

            float hazeIntensity = alpha * (1f - progress * 0.5f);
            ExplosionShaders.DrawHeatHaze(canvas, hazeRect, _globalTimer, hazeIntensity, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POWERUP
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderPowerUp(SKCanvas canvas, PowerUp powerUp)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Blinking when about to expire
        if (powerUp.IsBlinking && !powerUp.IsBeingCollected && ((int)(_globalTimer * 8) % 2) == 0)
            return;

        // Einsammel-Animation: Shrink + Spin + Fade
        float collectScale = 1f;
        float collectRotation = 0f;
        if (powerUp.IsBeingCollected)
        {
            float progress = 1f - (powerUp.CollectTimer / PowerUp.COLLECT_DURATION); // 0→1
            collectScale = 1f - progress; // 1→0
            collectRotation = progress * 720f; // 2 volle Drehungen
        }

        // Schwebe-Animation: Sanftes Auf/Ab + leichte Neigung
        float bob = MathF.Sin(_globalTimer * 2.5f) * 2.5f;
        float px = powerUp.X;
        float py = powerUp.Y + bob;

        SKColor color = GetPowerUpColor(powerUp.Type);
        float radius = cs * 0.38f * collectScale;

        // Glow-Pulsierung (unabhängig vom Bobbing)
        float glowPulse = MathF.Sin(_globalTimer * 4f) * 0.3f + 0.7f;

        // Canvas-Transform: Langsame Rotation + Einsammel-Animation
        canvas.Save();
        canvas.Translate(px, py);
        float idleRotation = MathF.Sin(_globalTimer * 0.8f) * 5f; // ±5° Schaukeln
        canvas.RotateDegrees(idleRotation + collectRotation);
        canvas.Translate(-px, -py);

        // Äußerer Glow-Ring (pulsierend)
        _glowPaint.Color = color.WithAlpha((byte)(40 * glowPulse));
        _glowPaint.MaskFilter = _mediumGlow;
        canvas.DrawCircle(px, py, radius + 5, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Schatten unter dem PowerUp
        _fillPaint.Color = new SKColor(0, 0, 0, 30);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(px, py + radius + 2, radius * 0.6f, 2f, _fillPaint);

        // Diamant-/Rauten-Form statt Kreis
        byte bgAlpha = powerUp.IsBeingCollected
            ? (byte)(255 * Math.Clamp(powerUp.CollectTimer / PowerUp.COLLECT_DURATION, 0f, 1f))
            : (byte)255;

        _fusePath.Reset();
        _fusePath.MoveTo(px, py - radius); // Oben
        _fusePath.LineTo(px + radius * 0.85f, py); // Rechts
        _fusePath.LineTo(px, py + radius); // Unten
        _fusePath.LineTo(px - radius * 0.85f, py); // Links
        _fusePath.Close();

        // Dunklerer Rand der Raute
        _fillPaint.Color = DarkenColor(color, 0.6f).WithAlpha(bgAlpha);
        canvas.DrawPath(_fusePath, _fillPaint);

        // Innere hellere Raute (3D-Effekt)
        float innerR = radius * 0.82f;
        _fusePath.Reset();
        _fusePath.MoveTo(px, py - innerR);
        _fusePath.LineTo(px + innerR * 0.85f, py);
        _fusePath.LineTo(px, py + innerR);
        _fusePath.LineTo(px - innerR * 0.85f, py);
        _fusePath.Close();
        _fillPaint.Color = color.WithAlpha(bgAlpha);
        canvas.DrawPath(_fusePath, _fillPaint);

        // Glanz-Highlight oben links (3D-Look)
        _fillPaint.Color = new SKColor(255, 255, 255, (byte)(60 * (bgAlpha / 255f)));
        _fusePath.Reset();
        _fusePath.MoveTo(px, py - innerR * 0.9f);
        _fusePath.LineTo(px - innerR * 0.75f, py);
        _fusePath.LineTo(px - innerR * 0.3f, py - innerR * 0.15f);
        _fusePath.LineTo(px - innerR * 0.15f, py - innerR * 0.6f);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);

        // Rauten-Kontur
        _strokePaint.Color = new SKColor(255, 255, 255, (byte)(80 * (bgAlpha / 255f)));
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        _fusePath.Reset();
        _fusePath.MoveTo(px, py - radius);
        _fusePath.LineTo(px + radius * 0.85f, py);
        _fusePath.LineTo(px, py + radius);
        _fusePath.LineTo(px - radius * 0.85f, py);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Icon/Symbol
        byte iconAlpha = powerUp.IsBeingCollected
            ? (byte)(255 * Math.Clamp(powerUp.CollectTimer / PowerUp.COLLECT_DURATION, 0f, 1f))
            : (byte)255;
        _fillPaint.Color = SKColors.White.WithAlpha(iconAlpha);
        RenderPowerUpIcon(canvas, powerUp.Type, px, py, radius * 0.55f);

        // Funkelnde Partikel um das PowerUp (2-3 kleine leuchtende Punkte)
        for (int i = 0; i < 3; i++)
        {
            float sparkAngle = _globalTimer * 1.2f + i * 2.094f; // 2*PI/3
            float sparkDist = radius * 1.1f + MathF.Sin(_globalTimer * 3f + i * 1.5f) * 3f;
            float sparkX = px + MathF.Cos(sparkAngle) * sparkDist;
            float sparkY = py + MathF.Sin(sparkAngle) * sparkDist;
            byte sparkAlpha = (byte)(120 * MathF.Abs(MathF.Sin(_globalTimer * 2.5f + i * 1.2f)));
            _fillPaint.Color = SKColors.White.WithAlpha(sparkAlpha);
            canvas.DrawCircle(sparkX, sparkY, 1.2f, _fillPaint);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Farbe abdunkeln (Faktor 0-1, 1 = unverändert)
    /// </summary>
    private static SKColor DarkenColor(SKColor c, float factor)
    {
        return new SKColor(
            (byte)(c.Red * factor),
            (byte)(c.Green * factor),
            (byte)(c.Blue * factor),
            c.Alpha);
    }

    private void RenderPowerUpIcon(SKCanvas canvas, PowerUpType type, float cx, float cy, float size)
    {
        _strokePaint.Color = SKColors.White;
        _strokePaint.StrokeWidth = 2f;
        _strokePaint.MaskFilter = null;

        switch (type)
        {
            case PowerUpType.BombUp:
                // Detaillierte Mini-Bombe mit Zündschnur + Highlight
                canvas.DrawCircle(cx, cy + 1, size * 0.5f, _fillPaint);
                // Highlight auf der Bombe
                _fillPaint.Color = new SKColor(255, 255, 255, 60);
                canvas.DrawCircle(cx - size * 0.15f, cy - size * 0.05f, size * 0.18f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                // Zündschnur
                _strokePaint.StrokeWidth = 1.5f;
                _fusePath.Reset();
                _fusePath.MoveTo(cx, cy - size * 0.3f);
                _fusePath.QuadTo(cx + size * 0.2f, cy - size * 0.55f, cx + size * 0.35f, cy - size * 0.45f);
                canvas.DrawPath(_fusePath, _strokePaint);
                // Funke
                _fillPaint.Color = new SKColor(255, 200, 50);
                canvas.DrawCircle(cx + size * 0.35f, cy - size * 0.45f, size * 0.12f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                _strokePaint.StrokeWidth = 2f;
                break;

            case PowerUpType.Fire:
                // Detaillierte Flamme mit 3 Zungen + innerem Kern
                _fusePath.Reset();
                _fusePath.MoveTo(cx, cy - size * 0.7f); // Hauptspitze
                _fusePath.QuadTo(cx + size * 0.25f, cy - size * 0.2f, cx + size * 0.45f, cy + size * 0.3f);
                _fusePath.QuadTo(cx + size * 0.15f, cy + size * 0.15f, cx + size * 0.2f, cy + size * 0.5f);
                _fusePath.LineTo(cx, cy + size * 0.25f);
                _fusePath.LineTo(cx - size * 0.2f, cy + size * 0.5f);
                _fusePath.QuadTo(cx - size * 0.15f, cy + size * 0.15f, cx - size * 0.45f, cy + size * 0.3f);
                _fusePath.QuadTo(cx - size * 0.25f, cy - size * 0.2f, cx, cy - size * 0.7f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                // Innerer gelber Kern
                _fillPaint.Color = new SKColor(255, 220, 100);
                _fusePath.Reset();
                _fusePath.MoveTo(cx, cy - size * 0.3f);
                _fusePath.QuadTo(cx + size * 0.12f, cy, cx + size * 0.1f, cy + size * 0.2f);
                _fusePath.LineTo(cx - size * 0.1f, cy + size * 0.2f);
                _fusePath.QuadTo(cx - size * 0.12f, cy, cx, cy - size * 0.3f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                _fillPaint.Color = SKColors.White;
                break;

            case PowerUpType.Speed:
                // Doppelpfeil (Geschwindigkeit) mit Speed-Lines
                _fusePath.Reset();
                _fusePath.MoveTo(cx - size * 0.2f, cy - size * 0.35f);
                _fusePath.LineTo(cx + size * 0.5f, cy);
                _fusePath.LineTo(cx - size * 0.2f, cy + size * 0.35f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                // Speed-Lines dahinter
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawLine(cx - size * 0.5f, cy - size * 0.15f, cx - size * 0.3f, cy - size * 0.15f, _strokePaint);
                canvas.DrawLine(cx - size * 0.55f, cy, cx - size * 0.3f, cy, _strokePaint);
                canvas.DrawLine(cx - size * 0.5f, cy + size * 0.15f, cx - size * 0.3f, cy + size * 0.15f, _strokePaint);
                _strokePaint.StrokeWidth = 2f;
                break;

            case PowerUpType.Wallpass:
                // Geist-Form mit welligem Saum + Augen
                _fusePath.Reset();
                float gTop = cy - size * 0.4f;
                float gBot = cy + size * 0.45f;
                // Kopf (Halbkreis)
                _fusePath.ArcTo(new SKRect(cx - size * 0.35f, gTop, cx + size * 0.35f, gTop + size * 0.7f), 180, 180, true);
                // Rechte Seite runter
                _fusePath.LineTo(cx + size * 0.35f, gBot);
                // Welliger Saum
                _fusePath.LineTo(cx + size * 0.15f, gBot - size * 0.15f);
                _fusePath.LineTo(cx, gBot);
                _fusePath.LineTo(cx - size * 0.15f, gBot - size * 0.15f);
                _fusePath.LineTo(cx - size * 0.35f, gBot);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                // Augen
                _fillPaint.Color = SKColors.Black;
                canvas.DrawCircle(cx - size * 0.12f, cy - size * 0.05f, size * 0.08f, _fillPaint);
                canvas.DrawCircle(cx + size * 0.12f, cy - size * 0.05f, size * 0.08f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                break;

            case PowerUpType.Detonator:
                // Blitz mit gefülltem Zickzack
                _fusePath.Reset();
                _fusePath.MoveTo(cx + size * 0.1f, cy - size * 0.65f);
                _fusePath.LineTo(cx - size * 0.15f, cy - size * 0.05f);
                _fusePath.LineTo(cx + size * 0.15f, cy - size * 0.05f);
                _fusePath.LineTo(cx - size * 0.1f, cy + size * 0.65f);
                _fusePath.LineTo(cx + size * 0.05f, cy + size * 0.15f);
                _fusePath.LineTo(cx - size * 0.12f, cy + size * 0.15f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                break;

            case PowerUpType.Bombpass:
                // Bombe mit Pfeil durch (durchdringt)
                canvas.DrawCircle(cx, cy, size * 0.32f, _fillPaint);
                _fillPaint.Color = new SKColor(255, 255, 255, 60);
                canvas.DrawCircle(cx - size * 0.1f, cy - size * 0.1f, size * 0.12f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawLine(cx - size * 0.6f, cy, cx + size * 0.6f, cy, _strokePaint);
                // Pfeilspitze
                canvas.DrawLine(cx + size * 0.4f, cy - size * 0.15f, cx + size * 0.6f, cy, _strokePaint);
                canvas.DrawLine(cx + size * 0.4f, cy + size * 0.15f, cx + size * 0.6f, cy, _strokePaint);
                _strokePaint.StrokeWidth = 2f;
                break;

            case PowerUpType.Flamepass:
                // Detaillierter Schild mit Kreuz-Emblem
                _fusePath.Reset();
                _fusePath.MoveTo(cx, cy - size * 0.55f);
                _fusePath.QuadTo(cx + size * 0.45f, cy - size * 0.45f, cx + size * 0.4f, cy - size * 0.1f);
                _fusePath.QuadTo(cx + size * 0.35f, cy + size * 0.3f, cx, cy + size * 0.6f);
                _fusePath.QuadTo(cx - size * 0.35f, cy + size * 0.3f, cx - size * 0.4f, cy - size * 0.1f);
                _fusePath.QuadTo(cx - size * 0.45f, cy - size * 0.45f, cx, cy - size * 0.55f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                // Kreuz-Emblem
                _fillPaint.Color = new SKColor(255, 255, 255, 80);
                canvas.DrawRect(cx - size * 0.06f, cy - size * 0.25f, size * 0.12f, size * 0.5f, _fillPaint);
                canvas.DrawRect(cx - size * 0.2f, cy - size * 0.06f, size * 0.4f, size * 0.12f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                break;

            case PowerUpType.Mystery:
                // Fragezeichen mit Glow
                _textPaint.Color = SKColors.White;
                canvas.DrawText("?", cx, cy + size * 0.25f, SKTextAlign.Center, _powerUpFont, _textPaint);
                break;

            case PowerUpType.Kick:
                // Schuh-Form mit Sohle + Bombe
                _fusePath.Reset();
                _fusePath.MoveTo(cx - size * 0.5f, cy - size * 0.15f);
                _fusePath.LineTo(cx - size * 0.5f, cy + size * 0.15f);
                _fusePath.LineTo(cx + size * 0.3f, cy + size * 0.15f);
                _fusePath.LineTo(cx + size * 0.45f, cy);
                _fusePath.LineTo(cx + size * 0.3f, cy - size * 0.15f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                // Sohle (dunkler)
                _fillPaint.Color = new SKColor(255, 255, 255, 80);
                canvas.DrawRect(cx - size * 0.5f, cy + size * 0.08f, size * 0.8f, size * 0.07f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                // Mini-Bombe die wegfliegt
                canvas.DrawCircle(cx + size * 0.35f, cy - size * 0.4f, size * 0.18f, _fillPaint);
                break;

            case PowerUpType.LineBomb:
                // 3 Bomben in Reihe mit Verbindungslinie
                for (int i = -1; i <= 1; i++)
                {
                    float bx = cx + i * size * 0.4f;
                    canvas.DrawCircle(bx, cy, size * 0.18f, _fillPaint);
                    // Highlight
                    _fillPaint.Color = new SKColor(255, 255, 255, 60);
                    canvas.DrawCircle(bx - size * 0.05f, cy - size * 0.05f, size * 0.07f, _fillPaint);
                    _fillPaint.Color = SKColors.White;
                }
                // Pfeil nach rechts (Richtung)
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawLine(cx - size * 0.55f, cy + size * 0.35f, cx + size * 0.55f, cy + size * 0.35f, _strokePaint);
                canvas.DrawLine(cx + size * 0.35f, cy + size * 0.25f, cx + size * 0.55f, cy + size * 0.35f, _strokePaint);
                canvas.DrawLine(cx + size * 0.35f, cy + size * 0.45f, cx + size * 0.55f, cy + size * 0.35f, _strokePaint);
                _strokePaint.StrokeWidth = 2f;
                break;

            case PowerUpType.PowerBomb:
                // Große Bombe mit Stern-Emblem + Strahlen
                canvas.DrawCircle(cx, cy, size * 0.42f, _fillPaint);
                _fillPaint.Color = new SKColor(255, 255, 255, 60);
                canvas.DrawCircle(cx - size * 0.12f, cy - size * 0.12f, size * 0.15f, _fillPaint);
                _fillPaint.Color = new SKColor(255, 255, 100);
                // 4-zackiger Stern
                _fusePath.Reset();
                float sr = size * 0.22f;
                float si = sr * 0.4f;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4f - MathF.PI / 8f;
                    float r = (i % 2 == 0) ? sr : si;
                    float sx = cx + MathF.Cos(a) * r;
                    float sy = cy + MathF.Sin(a) * r;
                    if (i == 0) _fusePath.MoveTo(sx, sy);
                    else _fusePath.LineTo(sx, sy);
                }
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                _fillPaint.Color = SKColors.White;
                break;

            case PowerUpType.Skull:
                // Detaillierter Totenkopf mit Kiefer-Zähnen
                canvas.DrawCircle(cx, cy - size * 0.1f, size * 0.4f, _fillPaint);
                // Kiefer
                canvas.DrawRoundRect(cx - size * 0.3f, cy + size * 0.15f, size * 0.6f, size * 0.2f, 2, 2, _fillPaint);
                _fillPaint.Color = SKColors.Black;
                // Augenhöhlen (oval statt rund)
                canvas.DrawOval(cx - size * 0.16f, cy - size * 0.15f, size * 0.11f, size * 0.14f, _fillPaint);
                canvas.DrawOval(cx + size * 0.16f, cy - size * 0.15f, size * 0.11f, size * 0.14f, _fillPaint);
                // Nasendreieck
                _fusePath.Reset();
                _fusePath.MoveTo(cx, cy + size * 0.02f);
                _fusePath.LineTo(cx - size * 0.06f, cy + size * 0.12f);
                _fusePath.LineTo(cx + size * 0.06f, cy + size * 0.12f);
                _fusePath.Close();
                canvas.DrawPath(_fusePath, _fillPaint);
                // Zähne (3 vertikale Streifen im Kiefer)
                for (int i = -1; i <= 1; i++)
                    canvas.DrawRect(cx + i * size * 0.12f - size * 0.03f, cy + size * 0.17f, size * 0.06f, size * 0.13f, _fillPaint);
                _fillPaint.Color = SKColors.White;
                break;

            default:
                _textPaint.Color = SKColors.White;
                canvas.DrawText("?", cx, cy + size * 0.25f, SKTextAlign.Center, _powerUpFont, _textPaint);
                break;
        }
    }

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
}
