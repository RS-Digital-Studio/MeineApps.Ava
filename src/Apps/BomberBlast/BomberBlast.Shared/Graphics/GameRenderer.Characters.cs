using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Charakter-Rendering: Spieler mit Armen/Beinen/Gesicht + 12 einzigartige Gegner-Typen
/// </summary>
public partial class GameRenderer
{
    // Gepoolte SKPaths für Charakter-Rendering (statt pro-Gegner new SKPath())
    private readonly SKPath _charPath1 = new();
    private readonly SKPath _charPath2 = new();
    // ═══════════════════════════════════════════════════════════════════════
    // SPIELER
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderPlayer(SKCanvas canvas, Player player)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Blink-Effekt bei Unverwundbarkeit / Spawn-Schutz
        // Schnelleres Blinken in den letzten 0.5s als visuelles Feedback fuer auslaufenden Schutz
        if (player.IsInvincible || player.HasSpawnProtection)
        {
            float remainingTimer = player.IsInvincible
                ? player.InvincibilityTimer
                : player.SpawnProtectionTimer;
            float blinkRate = remainingTimer <= 0.5f ? 20f : 10f;
            if (((int)(_globalTimer * blinkRate) % 2) == 0)
                return;
        }

        if (player.IsDying)
        {
            RenderPlayerDeath(canvas, player, cs);
            return;
        }

        float bodyW = cs * 0.5f;
        float bodyH = cs * 0.55f;

        // Walk-Animation
        float walkBob = 0f;
        float walkPhase = 0f;
        if (player.IsMoving)
        {
            walkPhase = _globalTimer * 14f;
            walkBob = MathF.Sin(walkPhase) * 1.5f;
        }

        // Skin-Farben
        var skin = _customizationService.PlayerSkin;
        var skinBody = skin.Id != "default" ? skin.PrimaryColor : _palette.PlayerBody;
        var skinHelm = skin.Id != "default" ? skin.SecondaryColor : _palette.PlayerHelm;
        float fdx = player.FacingDirection.GetDeltaX();
        float fdy = player.FacingDirection.GetDeltaY();

        // --- Schatten unter dem Spieler ---
        _fillPaint.Color = new SKColor(0, 0, 0, 35);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(player.X, player.Y + bodyH * 0.38f, bodyW * 0.4f, bodyH * 0.12f, _fillPaint);

        // --- Beine (2 kurze Striche, Walk-Animation) ---
        float legBaseY = player.Y + bodyH * 0.22f + walkBob;
        float legLen = cs * 0.12f;
        float legSpacing = bodyW * 0.2f;
        _fillPaint.Color = skinHelm;
        if (player.IsMoving)
        {
            float legSwing1 = MathF.Sin(walkPhase) * legLen * 0.7f;
            float legSwing2 = MathF.Sin(walkPhase + MathF.PI) * legLen * 0.7f;
            // Bein-Richtung basiert auf Blickrichtung
            if (MathF.Abs(fdx) >= MathF.Abs(fdy))
            {
                // Horizontal: Beine vor/zurück
                canvas.DrawRect(player.X - legSpacing - 1.5f, legBaseY, 3, legLen, _fillPaint);
                canvas.DrawRect(player.X + legSpacing - 1.5f, legBaseY, 3, legLen, _fillPaint);
                // Füße
                _fillPaint.Color = new SKColor(60, 60, 70);
                canvas.DrawOval(player.X - legSpacing + legSwing1, legBaseY + legLen, 2.5f, 1.5f, _fillPaint);
                canvas.DrawOval(player.X + legSpacing + legSwing2, legBaseY + legLen, 2.5f, 1.5f, _fillPaint);
            }
            else
            {
                // Vertikal: Beine seitlich schwingen
                canvas.DrawRect(player.X - legSpacing - 1.5f, legBaseY, 3, legLen, _fillPaint);
                canvas.DrawRect(player.X + legSpacing - 1.5f, legBaseY, 3, legLen, _fillPaint);
                _fillPaint.Color = new SKColor(60, 60, 70);
                canvas.DrawOval(player.X - legSpacing, legBaseY + legLen + legSwing1 * 0.5f, 2.5f, 1.5f, _fillPaint);
                canvas.DrawOval(player.X + legSpacing, legBaseY + legLen + legSwing2 * 0.5f, 2.5f, 1.5f, _fillPaint);
            }
        }
        else
        {
            // Idle: Beine stehen still
            canvas.DrawRect(player.X - legSpacing - 1.5f, legBaseY, 3, legLen, _fillPaint);
            canvas.DrawRect(player.X + legSpacing - 1.5f, legBaseY, 3, legLen, _fillPaint);
            _fillPaint.Color = new SKColor(60, 60, 70);
            canvas.DrawOval(player.X - legSpacing, legBaseY + legLen, 2.5f, 1.5f, _fillPaint);
            canvas.DrawOval(player.X + legSpacing, legBaseY + legLen, 2.5f, 1.5f, _fillPaint);
        }

        float bx = player.X - bodyW / 2f;
        float by = player.Y - bodyH / 2f + walkBob;

        // --- Aura/Glow ---
        if (skin.GlowColor.HasValue)
        {
            _glowPaint.Color = skin.GlowColor.Value;
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRoundRect(bx - 3, by - 3, bodyW + 6, bodyH + 6, 8, 8, _glowPaint);
            _glowPaint.MaskFilter = null;
        }
        else if (isNeon && _palette.PlayerAura.Alpha > 0)
        {
            _glowPaint.Color = _palette.PlayerAura;
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRoundRect(bx - 3, by - 3, bodyW + 6, bodyH + 6, 8, 8, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // --- Schild-Glow ---
        if (player.HasShield)
        {
            float shieldPulse = MathF.Sin(_globalTimer * 4f) * 0.2f + 0.8f;
            _glowPaint.Color = new SKColor(0, 229, 255, (byte)(100 * shieldPulse));
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawCircle(player.X, player.Y + walkBob, cs * 0.5f, _glowPaint);
            _glowPaint.MaskFilter = null;
            _strokePaint.Color = new SKColor(0, 229, 255, (byte)(180 * shieldPulse));
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;
            canvas.DrawCircle(player.X, player.Y + walkBob, cs * 0.45f, _strokePaint);
            _strokePaint.MaskFilter = null;
        }

        // --- Curse-Glow ---
        if (player.IsCursed)
        {
            float cursePulse = MathF.Sin(_globalTimer * 8f) * 0.3f + 0.7f;
            _glowPaint.Color = new SKColor(180, 0, 180, (byte)(140 * cursePulse));
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRoundRect(bx - 4, by - 4, bodyW + 8, bodyH + 8, 10, 10, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // --- Arme (gegenlauf zu Beinen) ---
        float armBaseY = by + bodyH * 0.25f;
        float armLen = cs * 0.1f;
        float armSwing = player.IsMoving ? MathF.Sin(walkPhase + MathF.PI) * 3f : 0f;
        _fillPaint.Color = skinBody;
        _fillPaint.MaskFilter = null;
        // Linker Arm
        canvas.DrawRoundRect(bx - 3.5f, armBaseY + armSwing, 3.5f, armLen, 1.5f, 1.5f, _fillPaint);
        // Rechter Arm
        canvas.DrawRoundRect(bx + bodyW, armBaseY - armSwing, 3.5f, armLen, 1.5f, 1.5f, _fillPaint);

        // --- Körper (abgerundetes Rechteck) ---
        _fillPaint.Color = skinBody;
        canvas.DrawRoundRect(bx, by, bodyW, bodyH, 6, 6, _fillPaint);

        // --- Helm (Halbkreis oben) ---
        _fillPaint.Color = skinHelm;
        float helmR = bodyW * 0.45f;
        canvas.DrawCircle(player.X, by + 2, helmR, _fillPaint);
        _fillPaint.Color = skinBody;
        canvas.DrawRect(bx, by + 2, bodyW, helmR, _fillPaint);

        // Helm-Glanzlicht
        _fillPaint.Color = new SKColor(255, 255, 255, 50);
        canvas.DrawCircle(player.X - helmR * 0.3f, by - helmR * 0.1f, helmR * 0.25f, _fillPaint);

        // --- Gesicht ---
        float eyeY = player.Y - bodyH * 0.12f + walkBob;
        float eyeSpacing = bodyW * 0.2f;
        float eyeR = 3.5f;
        float pupilR = 1.8f;
        float pdx = fdx * 1.5f;
        float pdy = fdy * 1.5f;

        // Blinzeln: Alle 3-5s für 150ms (deterministisch aus globalTimer)
        float blinkCycle = _globalTimer % 4f;
        bool isBlinking = blinkCycle > 3.85f;

        if (isBlinking)
        {
            // Geschlossene Augen (Striche)
            _strokePaint.Color = SKColors.Black;
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(player.X - eyeSpacing - 2, eyeY, player.X - eyeSpacing + 2, eyeY, _strokePaint);
            canvas.DrawLine(player.X + eyeSpacing - 2, eyeY, player.X + eyeSpacing + 2, eyeY, _strokePaint);
        }
        else
        {
            // Augenweiß
            _fillPaint.Color = SKColors.White;
            canvas.DrawCircle(player.X - eyeSpacing, eyeY, eyeR, _fillPaint);
            canvas.DrawCircle(player.X + eyeSpacing, eyeY, eyeR, _fillPaint);
            // Pupillen
            _fillPaint.Color = new SKColor(30, 30, 50);
            canvas.DrawCircle(player.X - eyeSpacing + pdx, eyeY + pdy, pupilR, _fillPaint);
            canvas.DrawCircle(player.X + eyeSpacing + pdx, eyeY + pdy, pupilR, _fillPaint);
            // Lichtpunkt in Pupillen
            _fillPaint.Color = new SKColor(255, 255, 255, 180);
            canvas.DrawCircle(player.X - eyeSpacing + pdx - 0.5f, eyeY + pdy - 0.5f, 0.7f, _fillPaint);
            canvas.DrawCircle(player.X + eyeSpacing + pdx - 0.5f, eyeY + pdy - 0.5f, 0.7f, _fillPaint);
        }

        // Wangenröte
        _fillPaint.Color = new SKColor(255, 150, 150, 40);
        canvas.DrawOval(player.X - eyeSpacing - 2, eyeY + 4, 2.5f, 1.5f, _fillPaint);
        canvas.DrawOval(player.X + eyeSpacing + 2, eyeY + 4, 2.5f, 1.5f, _fillPaint);

        // Mund (kleines Lächeln)
        float mouthY = eyeY + bodyH * 0.22f;
        _strokePaint.Color = new SKColor(80, 50, 50);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        _fusePath.Reset();
        _fusePath.MoveTo(player.X - 2.5f, mouthY);
        _fusePath.QuadTo(player.X, mouthY + 2, player.X + 2.5f, mouthY);
        canvas.DrawPath(_fusePath, _strokePaint);
    }

    private void RenderPlayerDeath(SKCanvas canvas, Player player, float cs)
    {
        float progress = player.DeathTimer / 1.5f;
        byte alpha = (byte)(255 * (1 - progress));

        _fillPaint.Color = SKColors.Red.WithAlpha(alpha);
        _fillPaint.MaskFilter = null;

        // Squash/Stretch: Erst strecken, dann zusammenfallen
        float phase1 = Math.Min(progress / 0.3f, 1f);
        float phase2 = progress > 0.3f ? (progress - 0.3f) / 0.7f : 0f;
        float scaleX = 1f - phase1 * 0.3f + phase2 * 0.8f;
        float scaleY = 1f + phase1 * 0.4f - phase2 * 0.6f;
        float drawSize = cs * (1f + progress * 0.2f);
        float rx = drawSize / 3 * scaleX;
        float ry = drawSize / 3 * scaleY;
        canvas.DrawOval(player.X, player.Y + phase2 * 6f, rx, ry, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GEGNER - Dispatcher
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderEnemy(SKCanvas canvas, Enemy enemy, GameGrid grid)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        if (enemy.IsDying)
        {
            if (enemy is BossEnemy dyingBoss)
            {
                RenderBossDeath(canvas, dyingBoss, cs);
                return;
            }
            RenderEnemyDeath(canvas, enemy, cs);
            return;
        }

        if (enemy is BossEnemy boss)
        {
            RenderBoss(canvas, boss);
            RenderFrozenEnemyOverlay(canvas, enemy, grid, cs, isNeon);
            return;
        }

        // Ghost unsichtbar
        if (enemy.Type == EnemyType.Ghost && enemy.IsInvisible)
        {
            var (gr, gg, gb) = enemy.Type.GetColor();
            _fillPaint.Color = new SKColor(gr, gg, gb, 60);
            _fillPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(enemy.X, enemy.Y, cs * 0.35f, cs * 0.35f, _fillPaint);
            _fillPaint.MaskFilter = null;
            return;
        }

        // Mimic getarnt
        if (enemy.Type == EnemyType.Mimic && enemy.IsDisguised)
        {
            RenderMimicDisguised(canvas, enemy, cs);
            return;
        }

        // Spawn-Animation (Portal-Effekt, 0.5s)
        if (enemy.IsSpawning)
        {
            RenderSpawnAnimation(canvas, enemy, cs);
            return;
        }

        // Skalierung für Mini-Splitter
        float sc = enemy.IsMiniSplitter ? 0.6f : 1f;
        var enemyCell = grid.TryGetCell(enemy.GridX, enemy.GridY);
        bool isFrozen = enemyCell?.IsFrozen == true;

        // Walk-Parameter
        float wobbleY = 0f;
        if (enemy.IsMoving && !isFrozen)
            wobbleY = MathF.Sin(_globalTimer * 10f + enemy.Y * 0.1f) * 1.2f;

        // Schatten unter dem Gegner
        _fillPaint.Color = new SKColor(0, 0, 0, 30);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(enemy.X, enemy.Y + cs * 0.3f * sc, cs * 0.28f * sc, cs * 0.08f * sc, _fillPaint);

        // Neon-Aura
        if (isNeon)
        {
            var (nr, ng, nb) = enemy.Type.GetColor();
            _glowPaint.Color = new SKColor(nr, ng, nb, 40);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(enemy.X, enemy.Y, cs * 0.35f * sc, cs * 0.35f * sc, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Typ-spezifisches Rendering
        switch (enemy.Type)
        {
            case EnemyType.Ballom: RenderBallom(canvas, enemy, cs, sc, wobbleY, isFrozen); break;
            case EnemyType.Onil: RenderOnil(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Doll: RenderDoll(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Minvo: RenderMinvo(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Kondoria: RenderKondoria(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Ovapi: RenderOvapi(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Pass: RenderPass(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Pontan: RenderPontan(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Tanker: RenderTanker(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Ghost: RenderGhost(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Splitter: RenderSplitter(canvas, enemy, cs, sc, wobbleY); break;
            case EnemyType.Mimic: RenderMimicActive(canvas, enemy, cs, sc, wobbleY); break;
        }

        RenderFrozenEnemyOverlay(canvas, enemy, grid, cs, isNeon);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 12 GEGNER-TYPEN - Einzigartige Körperformen + Gesichter
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Ballom: Runder Blob, große doofe Augen, breites Grinsen, hüpfend</summary>
    private void RenderBallom(SKCanvas canvas, Enemy e, float cs, float sc, float wy, bool frozen)
    {
        float bounce = (e.IsMoving && !frozen) ? MathF.Abs(MathF.Sin(_globalTimer * 8f + e.X * 0.1f)) * 3f : 0f;
        float r = cs * 0.32f * sc;

        // Runder Blob-Körper (leicht wellig)
        _fillPaint.Color = new SKColor(255, 180, 50);
        _fillPaint.MaskFilter = null;
        float wobble = MathF.Sin(_globalTimer * 4f) * 0.03f;
        canvas.DrawOval(e.X, e.Y + wy - bounce, r * (1f + wobble), r * (1f - wobble), _fillPaint);
        // Bauch-Highlight
        _fillPaint.Color = new SKColor(255, 210, 100, 80);
        canvas.DrawOval(e.X, e.Y + wy - bounce + r * 0.15f, r * 0.5f, r * 0.4f, _fillPaint);

        // Große doofe Augen
        float eyeY = e.Y + wy - bounce - r * 0.2f;
        float eyeSp = r * 0.4f;
        RenderEnemyEyes(canvas, e, eyeY, eyeSp, 4f * sc, 2.2f * sc, false);

        // Breites Grinsen
        _strokePaint.Color = new SKColor(120, 60, 0);
        _strokePaint.StrokeWidth = 1.2f;
        _strokePaint.MaskFilter = null;
        float my = e.Y + wy - bounce + r * 0.35f;
        _fusePath.Reset();
        _fusePath.MoveTo(e.X - 4 * sc, my);
        _fusePath.QuadTo(e.X, my + 3 * sc, e.X + 4 * sc, my);
        canvas.DrawPath(_fusePath, _strokePaint);
    }

    /// <summary>Onil: Tropfenform, listige Augen, 1 Fangzahn, schleichend</summary>
    private void RenderOnil(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float r = cs * 0.3f * sc;
        // Tropfenform (breiter unten)
        _fillPaint.Color = new SKColor(80, 120, 255);
        _fillPaint.MaskFilter = null;
        _charPath1.Rewind();
        _charPath1.MoveTo(e.X, e.Y + wy - r * 1.1f);                         // Spitze oben
        _charPath1.CubicTo(e.X + r * 1.2f, e.Y + wy - r * 0.3f,             // Rechts oben
                      e.X + r * 1.1f, e.Y + wy + r * 0.8f,             // Rechts unten
                      e.X, e.Y + wy + r);                                // Mitte unten
        _charPath1.CubicTo(e.X - r * 1.1f, e.Y + wy + r * 0.8f,             // Links unten
                      e.X - r * 1.2f, e.Y + wy - r * 0.3f,             // Links oben
                      e.X, e.Y + wy - r * 1.1f);                        // Zurück
        _charPath1.Close();
        canvas.DrawPath(_charPath1, _fillPaint);

        // Listige schräge Augen
        float eyeY = e.Y + wy - r * 0.1f;
        float eyeSp = r * 0.45f;
        RenderEnemyEyes(canvas, e, eyeY, eyeSp, 3f * sc, 1.8f * sc, true);

        // Ein Fangzahn
        _fillPaint.Color = SKColors.White;
        float my = eyeY + r * 0.55f;
        canvas.DrawRect(e.X + 1, my, 2 * sc, 3 * sc, _fillPaint);
    }

    /// <summary>Doll: Rund mit Haarschleife, große niedliche Augen, trippelnd</summary>
    private void RenderDoll(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float r = cs * 0.28f * sc;
        float tripple = e.IsMoving ? MathF.Sin(_globalTimer * 16f + e.X * 0.2f) * 1f : 0f;

        // Runder Körper
        _fillPaint.Color = new SKColor(255, 150, 200);
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(e.X, e.Y + wy + tripple, r, _fillPaint);

        // Bauch-Highlight
        _fillPaint.Color = new SKColor(255, 190, 220, 80);
        canvas.DrawOval(e.X, e.Y + wy + tripple + 2, r * 0.55f, r * 0.4f, _fillPaint);

        // Haarschleife oben
        _fillPaint.Color = new SKColor(255, 60, 100);
        canvas.DrawCircle(e.X - 3 * sc, e.Y + wy + tripple - r - 1, 3 * sc, _fillPaint);
        canvas.DrawCircle(e.X + 3 * sc, e.Y + wy + tripple - r - 1, 3 * sc, _fillPaint);
        canvas.DrawCircle(e.X, e.Y + wy + tripple - r, 2 * sc, _fillPaint);

        // Große niedliche Augen (größer als normal)
        float eyeY = e.Y + wy + tripple - r * 0.15f;
        float eyeSp = r * 0.5f;
        _fillPaint.Color = SKColors.White;
        canvas.DrawCircle(e.X - eyeSp, eyeY, 4f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 4f * sc, _fillPaint);
        // Große dunkle Pupillen mit Glanzpunkt
        float pdx = e.FacingDirection.GetDeltaX() * 1.2f;
        float pdy = e.FacingDirection.GetDeltaY() * 1.2f;
        _fillPaint.Color = new SKColor(60, 30, 60);
        canvas.DrawCircle(e.X - eyeSp + pdx, eyeY + pdy, 2.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp + pdx, eyeY + pdy, 2.5f * sc, _fillPaint);
        _fillPaint.Color = new SKColor(255, 255, 255, 200);
        canvas.DrawCircle(e.X - eyeSp + pdx - 1, eyeY + pdy - 1, 1f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp + pdx - 1, eyeY + pdy - 1, 1f * sc, _fillPaint);

        // Kleine Füßchen unten
        _fillPaint.Color = new SKColor(200, 100, 140);
        float footY = e.Y + wy + tripple + r - 1;
        canvas.DrawOval(e.X - 3 * sc, footY, 3 * sc, 2 * sc, _fillPaint);
        canvas.DrawOval(e.X + 3 * sc, footY, 3 * sc, 2 * sc, _fillPaint);
    }

    /// <summary>Minvo: Kantig/eckig, wütende Augen, Hörner, stampfend</summary>
    private void RenderMinvo(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float w = cs * 0.5f * sc, h = cs * 0.55f * sc;
        float stamp = e.IsMoving ? MathF.Abs(MathF.Sin(_globalTimer * 6f + e.X * 0.15f)) * 2f : 0f;

        // Hörner (vor dem Körper)
        _fillPaint.Color = new SKColor(180, 30, 30);
        _fillPaint.MaskFilter = null;
        _charPath1.Rewind();
        _charPath1.MoveTo(e.X - w * 0.35f, e.Y + wy - h * 0.4f);
        _charPath1.LineTo(e.X - w * 0.5f, e.Y + wy - h * 0.75f);
        _charPath1.LineTo(e.X - w * 0.15f, e.Y + wy - h * 0.35f);
        _charPath1.Close();
        canvas.DrawPath(_charPath1, _fillPaint);
        _charPath1.Rewind();
        _charPath1.MoveTo(e.X + w * 0.35f, e.Y + wy - h * 0.4f);
        _charPath1.LineTo(e.X + w * 0.5f, e.Y + wy - h * 0.75f);
        _charPath1.LineTo(e.X + w * 0.15f, e.Y + wy - h * 0.35f);
        _charPath1.Close();
        canvas.DrawPath(_charPath1, _fillPaint);

        // Eckiger Körper
        _fillPaint.Color = new SKColor(255, 60, 60);
        canvas.DrawRoundRect(e.X - w / 2, e.Y + wy - h / 2 + stamp, w, h, 3, 3, _fillPaint);

        // Zusammengekniffene wütende Augen
        float eyeY = e.Y + wy - h * 0.08f + stamp;
        float eyeSp = w * 0.25f;
        _fillPaint.Color = new SKColor(255, 230, 200);
        canvas.DrawOval(e.X - eyeSp, eyeY, 3 * sc, 2 * sc, _fillPaint);
        canvas.DrawOval(e.X + eyeSp, eyeY, 3 * sc, 2 * sc, _fillPaint);
        float pdx = e.FacingDirection.GetDeltaX();
        _fillPaint.Color = new SKColor(200, 0, 0);
        canvas.DrawCircle(e.X - eyeSp + pdx, eyeY, 1.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp + pdx, eyeY, 1.5f * sc, _fillPaint);

        // Schwere Augenbrauen (V-Form)
        _strokePaint.Color = new SKColor(100, 0, 0);
        _strokePaint.StrokeWidth = 2f * sc;
        _strokePaint.MaskFilter = null;
        float browY = eyeY - 3 * sc;
        canvas.DrawLine(e.X - eyeSp - 3 * sc, browY - 2, e.X - eyeSp + 2 * sc, browY + 1, _strokePaint);
        canvas.DrawLine(e.X + eyeSp + 3 * sc, browY - 2, e.X + eyeSp - 2 * sc, browY + 1, _strokePaint);

        // Zähne-fletschendes Maul
        _strokePaint.Color = new SKColor(100, 0, 0);
        _strokePaint.StrokeWidth = 1.2f;
        float my = eyeY + h * 0.28f;
        canvas.DrawLine(e.X - 4 * sc, my, e.X + 4 * sc, my, _strokePaint);
        _fillPaint.Color = SKColors.White;
        for (int i = -2; i <= 2; i++)
            canvas.DrawRect(e.X + i * 2.2f * sc - 0.5f, my - 1, 1.5f * sc, 2.5f * sc, _fillPaint);
    }

    /// <summary>Kondoria: Pilzform (breiter Hut), schläfrige Augen, schwebend</summary>
    private void RenderKondoria(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float r = cs * 0.28f * sc;
        float floatOff = MathF.Sin(_globalTimer * 3f + e.X * 0.1f) * 2f;

        // Stiel (Fuß)
        _fillPaint.Color = new SKColor(200, 120, 240);
        _fillPaint.MaskFilter = null;
        canvas.DrawRoundRect(e.X - r * 0.4f, e.Y + wy + floatOff - r * 0.2f,
            r * 0.8f, r * 1.1f, 3, 3, _fillPaint);

        // Pilzhut (breite Halbkugel oben)
        _fillPaint.Color = new SKColor(180, 80, 220);
        canvas.DrawOval(e.X, e.Y + wy + floatOff - r * 0.5f, r * 1.3f, r * 0.8f, _fillPaint);
        // Punkte auf dem Hut
        _fillPaint.Color = new SKColor(220, 160, 255, 120);
        canvas.DrawCircle(e.X - r * 0.5f, e.Y + wy + floatOff - r * 0.7f, 2 * sc, _fillPaint);
        canvas.DrawCircle(e.X + r * 0.3f, e.Y + wy + floatOff - r * 0.8f, 1.5f * sc, _fillPaint);

        // Schläfrige halbgeschlossene Augen
        float eyeY = e.Y + wy + floatOff - r * 0.1f;
        float eyeSp = r * 0.45f;
        _fillPaint.Color = SKColors.White;
        canvas.DrawOval(e.X - eyeSp, eyeY, 3 * sc, 1.8f * sc, _fillPaint);
        canvas.DrawOval(e.X + eyeSp, eyeY, 3 * sc, 1.8f * sc, _fillPaint);
        // Halb geschlossene Lider (obere Hälfte)
        _fillPaint.Color = new SKColor(200, 120, 240);
        canvas.DrawRect(e.X - eyeSp - 3 * sc, eyeY - 2.5f * sc, 6 * sc, 2 * sc, _fillPaint);
        canvas.DrawRect(e.X + eyeSp - 3 * sc, eyeY - 2.5f * sc, 6 * sc, 2 * sc, _fillPaint);
        // Kleine Pupillen
        float pdx = e.FacingDirection.GetDeltaX() * 0.8f;
        _fillPaint.Color = SKColors.Black;
        canvas.DrawCircle(e.X - eyeSp + pdx, eyeY + 0.5f, 1.2f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp + pdx, eyeY + 0.5f, 1.2f * sc, _fillPaint);
    }

    /// <summary>Ovapi: Oktopus-Form mit Tentakeln, leuchtende Augen, gleitend</summary>
    private void RenderOvapi(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float r = cs * 0.26f * sc;

        // Tentakel (wellig, 4 Stück)
        _fillPaint.Color = new SKColor(60, 220, 220);
        _fillPaint.MaskFilter = null;
        for (int i = 0; i < 4; i++)
        {
            float tx = e.X + (i - 1.5f) * r * 0.6f;
            float tentLen = r * 0.9f;
            float wave = MathF.Sin(_globalTimer * 5f + i * 1.5f) * 3f;
            _strokePaint.Color = new SKColor(60, 220, 220);
            _strokePaint.StrokeWidth = 2.5f * sc;
            _strokePaint.MaskFilter = null;
            _fusePath.Reset();
            _fusePath.MoveTo(tx, e.Y + wy + r * 0.3f);
            _fusePath.QuadTo(tx + wave, e.Y + wy + r * 0.3f + tentLen * 0.6f,
                             tx - wave * 0.5f, e.Y + wy + r * 0.3f + tentLen);
            canvas.DrawPath(_fusePath, _strokePaint);
        }

        // Runder Kopf
        _fillPaint.Color = new SKColor(80, 255, 255);
        canvas.DrawOval(e.X, e.Y + wy - r * 0.1f, r * 1.1f, r, _fillPaint);

        // Leuchtende Augen
        float eyeY = e.Y + wy - r * 0.2f;
        float eyeSp = r * 0.5f;
        _fillPaint.Color = new SKColor(0, 255, 200);
        _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(e.X - eyeSp, eyeY, 3 * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 3 * sc, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Dunkle Schlitz-Pupillen
        _fillPaint.Color = new SKColor(0, 80, 80);
        canvas.DrawOval(e.X - eyeSp, eyeY, 1 * sc, 2.5f * sc, _fillPaint);
        canvas.DrawOval(e.X + eyeSp, eyeY, 1 * sc, 2.5f * sc, _fillPaint);
    }

    /// <summary>Pass: Pfeilförmig/keilförmig, aggressive schmale Augen, Speed-Lines</summary>
    private void RenderPass(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float w = cs * 0.5f * sc, h = cs * 0.5f * sc;
        float fdx = e.FacingDirection.GetDeltaX();
        float fdy = e.FacingDirection.GetDeltaY();

        // Keilform (Pfeilspitze in Bewegungsrichtung)
        _fillPaint.Color = new SKColor(255, 255, 80);
        _fillPaint.MaskFilter = null;
        _charPath1.Rewind();
        if (MathF.Abs(fdx) >= MathF.Abs(fdy))
        {
            // Horizontal
            float front = e.X + fdx * w * 0.5f;
            _charPath1.MoveTo(front, e.Y + wy);
            _charPath1.LineTo(e.X - fdx * w * 0.4f, e.Y + wy - h * 0.45f);
            _charPath1.LineTo(e.X - fdx * w * 0.4f, e.Y + wy + h * 0.45f);
        }
        else
        {
            // Vertikal
            float front = e.Y + wy + fdy * h * 0.5f;
            _charPath1.MoveTo(e.X, front);
            _charPath1.LineTo(e.X - w * 0.45f, e.Y + wy - fdy * h * 0.4f);
            _charPath1.LineTo(e.X + w * 0.45f, e.Y + wy - fdy * h * 0.4f);
        }
        _charPath1.Close();
        canvas.DrawPath(_charPath1, _fillPaint);

        // Aggressive schmale Augen
        float eyeY = e.Y + wy - 1;
        float eyeSp = w * 0.2f;
        _fillPaint.Color = new SKColor(200, 0, 0);
        canvas.DrawOval(e.X - eyeSp, eyeY, 2.5f * sc, 1.5f * sc, _fillPaint);
        canvas.DrawOval(e.X + eyeSp, eyeY, 2.5f * sc, 1.5f * sc, _fillPaint);
        _fillPaint.Color = SKColors.Black;
        canvas.DrawOval(e.X - eyeSp, eyeY, 1.2f * sc, 1.2f * sc, _fillPaint);
        canvas.DrawOval(e.X + eyeSp, eyeY, 1.2f * sc, 1.2f * sc, _fillPaint);

        // Speed-Lines hinter dem Gegner (wenn in Bewegung)
        if (e.IsMoving)
        {
            _strokePaint.Color = new SKColor(255, 255, 80, 80);
            _strokePaint.StrokeWidth = 1f;
            _strokePaint.MaskFilter = null;
            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * 4f * sc;
                float lineLen = 8f + i * 3f;
                canvas.DrawLine(e.X - fdx * w * 0.5f + offset * fdy,
                    e.Y + wy - fdy * h * 0.5f + offset * fdx,
                    e.X - fdx * (w * 0.5f + lineLen) + offset * fdy,
                    e.Y + wy - fdy * (h * 0.5f + lineLen) + offset * fdx,
                    _strokePaint);
            }
        }
    }

    /// <summary>Pontan: Flammenform mit flackernden Kanten, glühende Augen, Partikel</summary>
    private void RenderPontan(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float r = cs * 0.28f * sc;

        // Flammenkörper (3 flackernde Zungen)
        for (int i = 0; i < 3; i++)
        {
            float flicker = MathF.Sin(_globalTimer * (8f + i * 3f) + e.X * 0.1f) * 2f;
            float tongueH = r * (1.4f + i * 0.15f) + flicker;
            byte alpha = (byte)(200 - i * 40);
            _fillPaint.Color = i == 0 ? new SKColor(255, 255, 255, alpha) :
                i == 1 ? new SKColor(255, 200, 60, alpha) :
                         new SKColor(255, 100, 20, alpha);
            _fillPaint.MaskFilter = i == 2 ? _smallGlow : null;
            float tongueW = r * (0.8f + i * 0.2f);
            _charPath2.Rewind();
            _charPath2.MoveTo(e.X - tongueW, e.Y + wy + r * 0.3f);
            _charPath2.QuadTo(e.X + flicker * 0.5f, e.Y + wy - tongueH, e.X + tongueW, e.Y + wy + r * 0.3f);
            _charPath2.Close();
            canvas.DrawPath(_charPath2, _fillPaint);
        }
        _fillPaint.MaskFilter = null;

        // Glühende rote Augen
        float eyeY = e.Y + wy - r * 0.15f;
        float eyeSp = r * 0.4f;
        _fillPaint.Color = new SKColor(255, 50, 20);
        _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(e.X - eyeSp, eyeY, 2.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 2.5f * sc, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Heller Kern
        _fillPaint.Color = new SKColor(255, 200, 100);
        canvas.DrawCircle(e.X - eyeSp, eyeY, 1.2f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 1.2f * sc, _fillPaint);
    }

    /// <summary>Tanker: Kastenform mit Rüstungsplatten, Visier-Schlitz, schwer stampfend</summary>
    private void RenderTanker(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float w = cs * 0.55f * sc, h = cs * 0.6f * sc;
        float stamp = e.IsMoving ? MathF.Abs(MathF.Sin(_globalTimer * 5f + e.X * 0.12f)) * 2.5f : 0f;

        // Schwerer Kastenförmiger Körper
        _fillPaint.Color = new SKColor(100, 100, 120);
        _fillPaint.MaskFilter = null;
        canvas.DrawRoundRect(e.X - w / 2, e.Y + wy - h / 2 + stamp, w, h, 4, 4, _fillPaint);

        // Rüstungsplatten (horizontale Streifen + Nieten)
        _strokePaint.Color = new SKColor(130, 130, 150);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = null;
        float plateY1 = e.Y + wy - h * 0.25f + stamp;
        float plateY2 = e.Y + wy + h * 0.1f + stamp;
        canvas.DrawLine(e.X - w * 0.4f, plateY1, e.X + w * 0.4f, plateY1, _strokePaint);
        canvas.DrawLine(e.X - w * 0.35f, plateY2, e.X + w * 0.35f, plateY2, _strokePaint);
        // Nieten
        _fillPaint.Color = new SKColor(160, 160, 180);
        float nrx = w * 0.35f;
        canvas.DrawCircle(e.X - nrx, plateY1, 1.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X + nrx, plateY1, 1.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X - nrx + 2, plateY2, 1.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X + nrx - 2, plateY2, 1.5f * sc, _fillPaint);

        // Visier-Schlitz (statt Augen)
        float visorY = e.Y + wy - h * 0.1f + stamp;
        _fillPaint.Color = new SKColor(255, 60, 40);
        _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawRoundRect(e.X - w * 0.3f, visorY - 1.5f * sc, w * 0.6f, 3 * sc, 1, 1, _fillPaint);
        _fillPaint.MaskFilter = null;

        // Risse wenn beschädigt
        if (e.HitPoints < e.Type.GetHitPoints())
        {
            _strokePaint.Color = new SKColor(200, 50, 50, 180);
            _strokePaint.StrokeWidth = 1.2f;
            canvas.DrawLine(e.X - 4, e.Y + wy - h * 0.15f + stamp, e.X + 3, e.Y + wy + h * 0.15f + stamp, _strokePaint);
            canvas.DrawLine(e.X + 1, e.Y + wy - h * 0.2f + stamp, e.X - 2, e.Y + wy + stamp, _strokePaint);
        }
    }

    /// <summary>Ghost: Klassische Geisterform mit welligem Saum, leuchtende Augen, schwebend</summary>
    private void RenderGhost(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float r = cs * 0.3f * sc;
        float floatOff = MathF.Sin(_globalTimer * 2.5f + e.X * 0.08f) * 3f;
        byte bodyAlpha = e.IsInvisible ? (byte)80 : (byte)220;

        // Geisterkörper (oben rund, unten wellig)
        _fillPaint.Color = new SKColor(180, 200, 255, bodyAlpha);
        _fillPaint.MaskFilter = null;
        _charPath1.Rewind();
        _charPath1.MoveTo(e.X - r, e.Y + wy + floatOff);
        _charPath1.ArcTo(new SKRect(e.X - r, e.Y + wy + floatOff - r * 1.6f, e.X + r, e.Y + wy + floatOff),
            180, 180, false);
        // Welliger unterer Saum
        float bottomY = e.Y + wy + floatOff + r * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            float segW = r * 0.5f;
            float x1 = e.X + r - i * segW;
            float x2 = x1 - segW;
            float wave = MathF.Sin(_globalTimer * 4f + i * 1.5f) * 2f;
            _charPath1.QuadTo(x1 - segW * 0.5f, bottomY + wave + 3, x2, bottomY);
        }
        _charPath1.Close();
        canvas.DrawPath(_charPath1, _fillPaint);

        // Leuchtende hohle Augen
        float eyeY = e.Y + wy + floatOff - r * 0.3f;
        float eyeSp = r * 0.4f;
        _fillPaint.Color = new SKColor(100, 180, 255, bodyAlpha);
        _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(e.X - eyeSp, eyeY, 3 * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 3 * sc, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Dunkle Pupillen
        _fillPaint.Color = new SKColor(20, 30, 60, bodyAlpha);
        canvas.DrawCircle(e.X - eyeSp, eyeY, 1.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 1.5f * sc, _fillPaint);

        // "O"-Mund
        _strokePaint.Color = new SKColor(60, 80, 120, bodyAlpha);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawCircle(e.X, eyeY + r * 0.5f, 2 * sc, _strokePaint);
    }

    /// <summary>Splitter: Zellform (Kreis mit Noppen), nervöse Augen, wackelnd</summary>
    private void RenderSplitter(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float r = cs * 0.27f * sc;
        float jitter = e.IsMoving ? MathF.Sin(_globalTimer * 20f + e.X * 0.3f) * 1.5f : 0f;

        // Zellkörper (Kreis)
        _fillPaint.Color = new SKColor(255, 200, 0);
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(e.X + jitter, e.Y + wy, r, _fillPaint);

        // Noppen/Pseudopodien (4 Stück, pulsierend)
        _fillPaint.Color = new SKColor(255, 180, 0);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * MathF.PI / 2f + MathF.Sin(_globalTimer * 3f + i) * 0.3f;
            float nx = e.X + jitter + MathF.Cos(angle) * (r + 2 * sc);
            float ny = e.Y + wy + MathF.Sin(angle) * (r + 2 * sc);
            canvas.DrawCircle(nx, ny, 2.5f * sc, _fillPaint);
        }

        // Nervös zitternde Augen
        float nervousX = MathF.Sin(_globalTimer * 12f + e.Y * 0.2f) * 0.8f;
        float eyeY = e.Y + wy - 2;
        float eyeSp = r * 0.35f;
        _fillPaint.Color = SKColors.White;
        canvas.DrawCircle(e.X + jitter - eyeSp, eyeY, 3 * sc, _fillPaint);
        canvas.DrawCircle(e.X + jitter + eyeSp, eyeY, 3 * sc, _fillPaint);
        _fillPaint.Color = SKColors.Black;
        canvas.DrawCircle(e.X + jitter - eyeSp + nervousX, eyeY, 1.8f * sc, _fillPaint);
        canvas.DrawCircle(e.X + jitter + eyeSp + nervousX, eyeY, 1.8f * sc, _fillPaint);

        // Mini-Splitter Markierung (Teilungs-Linie)
        if (!e.IsMiniSplitter)
        {
            _strokePaint.Color = new SKColor(200, 150, 0, 120);
            _strokePaint.StrokeWidth = 1f;
            _strokePaint.MaskFilter = null;
            canvas.DrawLine(e.X + jitter, e.Y + wy - r * 0.8f, e.X + jitter, e.Y + wy + r * 0.8f, _strokePaint);
        }
    }

    /// <summary>Mimic: Block-Tarnung (getarnt, wird in RenderEnemy abgefangen)</summary>
    private void RenderMimicDisguised(SKCanvas canvas, Enemy e, float cs)
    {
        float pulse = 0.05f + MathF.Sin(_globalTimer * 2f) * 0.03f;
        float blockX = e.X - cs / 2f;
        float blockY = e.Y - cs / 2f;

        _fillPaint.Color = new SKColor(
            (byte)Math.Min(255, _worldPalette!.BlockMain.Red + (int)(pulse * 255)),
            _worldPalette.BlockMain.Green,
            _worldPalette.BlockMain.Blue);
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(blockX + 2, blockY + 2, cs - 4, cs - 4, _fillPaint);

        _strokePaint.Color = _worldPalette.BlockMortar;
        _strokePaint.StrokeWidth = 0.8f;
        _strokePaint.MaskFilter = null;
        float midY = blockY + cs / 2f;
        canvas.DrawLine(blockX + 2, midY, blockX + cs - 2, midY, _strokePaint);
        canvas.DrawLine(blockX + cs * 0.35f, blockY + 2, blockX + cs * 0.35f, midY, _strokePaint);
        canvas.DrawLine(blockX + cs * 0.7f, midY, blockX + cs * 0.7f, blockY + cs - 2, _strokePaint);
    }

    /// <summary>Mimic: Aktiver Angriffsmodus (Block → Monster-Verwandlung)</summary>
    private void RenderMimicActive(SKCanvas canvas, Enemy e, float cs, float sc, float wy)
    {
        float w = cs * 0.5f * sc, h = cs * 0.5f * sc;

        // Geöffneter Block-Körper (Split-Maul)
        _fillPaint.Color = new SKColor(180, 120, 60);
        _fillPaint.MaskFilter = null;
        float mawOpen = MathF.Sin(_globalTimer * 6f) * 2f + 3f;
        // Obere Hälfte
        canvas.DrawRoundRect(e.X - w / 2, e.Y + wy - h / 2 - mawOpen, w, h * 0.45f, 3, 3, _fillPaint);
        // Untere Hälfte
        canvas.DrawRoundRect(e.X - w / 2, e.Y + wy + mawOpen, w, h * 0.45f, 3, 3, _fillPaint);

        // Zähne im Maul
        _fillPaint.Color = SKColors.White;
        float teethY = e.Y + wy - mawOpen + 1;
        for (int i = -2; i <= 2; i++)
            canvas.DrawRect(e.X + i * 3f * sc, teethY, 2 * sc, 3 * sc, _fillPaint);
        float teethYBot = e.Y + wy + mawOpen - 3;
        for (int i = -2; i <= 2; i++)
            canvas.DrawRect(e.X + i * 3f * sc + 1.5f * sc, teethYBot, 2 * sc, 3 * sc, _fillPaint);

        // Rote Augen die aufklappen
        float eyeY = e.Y + wy - h * 0.25f - mawOpen;
        float eyeSp = w * 0.25f;
        _fillPaint.Color = new SKColor(255, 40, 40);
        _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(e.X - eyeSp, eyeY, 2.5f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 2.5f * sc, _fillPaint);
        _fillPaint.MaskFilter = null;
        _fillPaint.Color = SKColors.Black;
        canvas.DrawCircle(e.X - eyeSp, eyeY, 1.2f * sc, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp, eyeY, 1.2f * sc, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Standard-Augen für Gegner (wiederverwendbar)</summary>
    private void RenderEnemyEyes(SKCanvas canvas, Enemy e, float eyeY, float eyeSp,
        float eyeR, float pupilR, bool slanted)
    {
        float pdx = e.FacingDirection.GetDeltaX() * 1.5f;
        float pdy = e.FacingDirection.GetDeltaY() * 1.5f;

        _fillPaint.Color = SKColors.White;
        _fillPaint.MaskFilter = null;
        if (slanted)
        {
            // Leicht schräge Augen
            canvas.DrawOval(e.X - eyeSp, eyeY, eyeR, eyeR * 0.7f, _fillPaint);
            canvas.DrawOval(e.X + eyeSp, eyeY, eyeR, eyeR * 0.7f, _fillPaint);
        }
        else
        {
            canvas.DrawCircle(e.X - eyeSp, eyeY, eyeR, _fillPaint);
            canvas.DrawCircle(e.X + eyeSp, eyeY, eyeR, _fillPaint);
        }

        _fillPaint.Color = SKColors.Black;
        canvas.DrawCircle(e.X - eyeSp + pdx, eyeY + pdy, pupilR, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp + pdx, eyeY + pdy, pupilR, _fillPaint);

        // Glanzpunkte
        _fillPaint.Color = new SKColor(255, 255, 255, 180);
        canvas.DrawCircle(e.X - eyeSp + pdx - 0.5f, eyeY + pdy - 0.5f, pupilR * 0.35f, _fillPaint);
        canvas.DrawCircle(e.X + eyeSp + pdx - 0.5f, eyeY + pdy - 0.5f, pupilR * 0.35f, _fillPaint);
    }

    /// <summary>
    /// Eis-Overlay über eingefrorenen Gegnern: Halbtransparenter blauer Frost + weißer Rand
    /// </summary>
    private void RenderFrozenEnemyOverlay(SKCanvas canvas, Enemy enemy, GameGrid grid, float cs, bool isNeon)
    {
        var cell = grid.TryGetCell(enemy.GridX, enemy.GridY);
        if (cell?.IsFrozen != true) return;

        float frostIntensity = Math.Min(1f, cell.FreezeTimer / 0.5f);
        float bodyW = cs * 0.6f;
        float bodyH = cs * 0.65f;
        if (enemy.IsMiniSplitter) { bodyW *= 0.6f; bodyH *= 0.6f; }

        byte frostAlpha = (byte)(100 * frostIntensity);
        _fillPaint.Color = new SKColor(140, 210, 255, frostAlpha);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(enemy.X, enemy.Y, bodyW * 0.5f, bodyH * 0.5f, _fillPaint);

        byte borderAlpha = (byte)(150 * frostIntensity);
        _strokePaint.Color = new SKColor(220, 240, 255, borderAlpha);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawOval(enemy.X, enemy.Y, bodyW * 0.48f, bodyH * 0.48f, _strokePaint);
        _strokePaint.MaskFilter = null;

        float shimmer = MathF.Sin(_globalTimer * 3f + enemy.X * 0.2f) * 0.5f + 0.5f;
        byte crystalAlpha = (byte)(80 * shimmer * frostIntensity);
        if (crystalAlpha > 10)
        {
            _fillPaint.Color = new SKColor(255, 255, 255, crystalAlpha);
            _fillPaint.MaskFilter = _smallGlow;
            canvas.DrawCircle(enemy.X - bodyW * 0.2f, enemy.Y - bodyH * 0.15f, 1.5f, _fillPaint);
            canvas.DrawCircle(enemy.X + bodyW * 0.15f, enemy.Y + bodyH * 0.1f, 1.2f, _fillPaint);
            _fillPaint.MaskFilter = null;
        }
    }

    private void RenderEnemyDeath(SKCanvas canvas, Enemy enemy, float cs)
    {
        float progress = enemy.DeathTimer / 0.8f;
        byte alpha = (byte)(255 * (1 - progress));

        var (r, g, b) = enemy.Type.GetColor();
        _fillPaint.Color = new SKColor(r, g, b, alpha);
        _fillPaint.MaskFilter = null;

        float squashX = 1f + progress * 0.6f;
        float squashY = 1f - progress * 0.4f;
        float drawSize = cs * (1 - progress * 0.3f);
        float rx = drawSize / 3 * squashX;
        float ry = drawSize / 3 * squashY;
        canvas.DrawOval(enemy.X, enemy.Y + progress * 4f, rx, ry, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPAWN-ANIMATION (Portal-Effekt, 0.5s)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gegner materialisiert aus einem wirbelnden Portal in Gegner-Farbe.
    /// Progress: 0→1 (0.5s→0). Expandierender Ring, Scale-Bounce, Fade-In.
    /// </summary>
    private void RenderSpawnAnimation(SKCanvas canvas, Enemy enemy, float cs)
    {
        float progress = 1f - enemy.SpawnTimer / 0.5f; // 0→1
        progress = Math.Clamp(progress, 0f, 1f);

        var (r, g, b) = enemy.Type.GetColor();
        float cx = enemy.X;
        float cy = enemy.Y;

        // 1) Wirbelnder Portal-Ring (expandiert)
        float portalRadius = cs * 0.15f + cs * 0.25f * progress;
        float rotAngle = _globalTimer * 15f; // Schnelle Rotation

        // Portal: 3 rotierende Spiralarme in Gegner-Farbe
        byte portalAlpha = (byte)(200 * (1f - progress * 0.5f));
        _strokePaint.Color = new SKColor(r, g, b, portalAlpha);
        _strokePaint.StrokeWidth = 2f + (1f - progress) * 2f;
        _strokePaint.MaskFilter = _smallGlow;

        for (int arm = 0; arm < 3; arm++)
        {
            float armAngle = rotAngle + arm * MathF.PI * 2f / 3f;
            float armEndX = cx + MathF.Cos(armAngle) * portalRadius;
            float armEndY = cy + MathF.Sin(armAngle) * portalRadius;
            float armMidX = cx + MathF.Cos(armAngle + 0.5f) * portalRadius * 0.6f;
            float armMidY = cy + MathF.Sin(armAngle + 0.5f) * portalRadius * 0.6f;

            _fusePath.Reset();
            _fusePath.MoveTo(cx, cy);
            _fusePath.QuadTo(armMidX, armMidY, armEndX, armEndY);
            canvas.DrawPath(_fusePath, _strokePaint);
        }
        _strokePaint.MaskFilter = null;

        // 2) Portal-Ring
        _strokePaint.Color = new SKColor(r, g, b, (byte)(150 * (1f - progress)));
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawCircle(cx, cy, portalRadius, _strokePaint);

        // 3) Gegner-Silhouette (Fade-In + Scale-Bounce)
        // Scale: Von 0.3 → Overshoot 1.15 → 1.0 (Bounce)
        float scale;
        if (progress < 0.7f)
        {
            scale = 0.3f + progress / 0.7f * 0.85f; // 0.3 → 1.15
        }
        else
        {
            float bounceProgress = (progress - 0.7f) / 0.3f; // 0→1
            scale = 1.15f - 0.15f * bounceProgress; // 1.15 → 1.0
        }

        byte bodyAlpha = (byte)(255 * progress);
        _fillPaint.Color = new SKColor(r, g, b, bodyAlpha);
        _fillPaint.MaskFilter = null;
        float bodyR = cs * 0.3f * scale;
        canvas.DrawOval(cx, cy, bodyR, bodyR * 0.9f, _fillPaint);

        // 4) Innerer heller Kern (verschwindet mit Progress)
        if (progress < 0.6f)
        {
            byte coreAlpha = (byte)(200 * (1f - progress / 0.6f));
            _fillPaint.Color = new SKColor(255, 255, 255, coreAlpha);
            _fillPaint.MaskFilter = _smallGlow;
            canvas.DrawCircle(cx, cy, bodyR * 0.3f, _fillPaint);
            _fillPaint.MaskFilter = null;
        }

        // 5) Partikel-Funken (6 Stück, fliegen nach außen)
        float sparkDist = cs * 0.1f + cs * 0.4f * progress;
        byte sparkAlpha = (byte)(180 * (1f - progress));
        _fillPaint.Color = new SKColor(r, g, b, sparkAlpha);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI * 2f / 6f + rotAngle * 0.3f;
            float sx = cx + MathF.Cos(angle) * sparkDist;
            float sy = cy + MathF.Sin(angle) * sparkDist;
            canvas.DrawCircle(sx, sy, 1.5f * (1f - progress), _fillPaint);
        }
    }
}
