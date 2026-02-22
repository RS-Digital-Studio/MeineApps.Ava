using BomberBlast.Models;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

public partial class GameRenderer
{
    private void RenderBoss(SKCanvas canvas, BossEnemy boss)
    {
        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float bossPixelSize = boss.BossSize * cs;

        // Wobble-Animation bei Bewegung (wie normale Gegner, etwas langsamer)
        float wobbleScale = 1f;
        float wobbleY = 0f;
        if (boss.IsMoving)
        {
            wobbleScale = 1f + MathF.Sin(_globalTimer * 8f + boss.X * 0.05f) * 0.03f;
            wobbleY = MathF.Sin(_globalTimer * 7f + boss.Y * 0.05f) * 2f;
        }

        // Pulsieren bei Telegraph/Angriff
        float attackPulse = 1f;
        if (boss.IsTelegraphing)
            attackPulse = 1f + MathF.Sin(_globalTimer * 12f) * 0.05f;
        else if (boss.IsAttacking)
            attackPulse = 1f + MathF.Sin(_globalTimer * 18f) * 0.08f;

        float totalScale = wobbleScale * attackPulse;

        switch (boss.BossKind)
        {
            case BossType.StoneGolem:
                RenderStoneGolem(canvas, boss, bossPixelSize, totalScale, wobbleY, isNeon);
                break;
            case BossType.IceDragon:
                RenderIceDragon(canvas, boss, bossPixelSize, totalScale, wobbleY, isNeon);
                break;
            case BossType.FireDemon:
                RenderFireDemon(canvas, boss, bossPixelSize, totalScale, wobbleY, isNeon);
                break;
            case BossType.ShadowMaster:
                RenderShadowMaster(canvas, boss, bossPixelSize, totalScale, wobbleY, isNeon);
                break;
            case BossType.FinalBoss:
                RenderFinalBoss(canvas, boss, bossPixelSize, totalScale, wobbleY, isNeon);
                break;
        }
    }

    /// <summary>
    /// StoneGolem (2x2): Dunkelgrauer Fels mit Stein-Textur, Arme, rote Augen, Risse bei Enrage
    /// </summary>
    private void RenderStoneGolem(SKCanvas canvas, BossEnemy boss, float size,
        float scale, float wobbleY, bool isNeon)
    {
        float halfSize = size * 0.45f * scale;
        float cx = boss.X;
        float cy = boss.Y + wobbleY;

        // Schatten unter dem Boss
        _fillPaint.Color = new SKColor(0, 0, 0, 35);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(cx, cy + halfSize * 0.85f, halfSize * 0.7f, halfSize * 0.15f, _fillPaint);

        // Neon-Aura
        if (isNeon)
        {
            _glowPaint.Color = new SKColor(85, 85, 85, 50);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawRoundRect(cx - halfSize, cy - halfSize, halfSize * 2, halfSize * 2, 6, 6, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Idle-Atmen (langsames Pulsieren)
        float breathe = MathF.Sin(_globalTimer * 1.5f) * 0.02f;
        float bodyW = halfSize * 1.6f * (1f + breathe);
        float bodyH = halfSize * 1.8f * (1f - breathe);

        // Körper: Dunkelgrauer Fels mit abgerundeten Ecken
        _fillPaint.Color = new SKColor(85, 85, 85);
        canvas.DrawRoundRect(cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH, 5, 5, _fillPaint);

        // Stein-Textur: Prozedurale Risslinien + Sprenkel
        _strokePaint.Color = new SKColor(60, 60, 60);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        ProceduralTextures.DrawCracks(canvas, _strokePaint, cx - bodyW * 0.35f, cy - bodyH * 0.4f,
            (int)(bodyW * 0.7f), 4, 42, new SKColor(60, 60, 60));

        // Moos-Flecken an den Schultern
        _fillPaint.Color = new SKColor(50, 100, 40, 60);
        canvas.DrawOval(cx - bodyW * 0.35f, cy - bodyH * 0.1f, 4f, 3f, _fillPaint);
        canvas.DrawOval(cx + bodyW * 0.3f, cy + bodyH * 0.05f, 3f, 4f, _fillPaint);

        // Arme: Seitliche Rechtecke mit Schulter-Verbindung
        _fillPaint.Color = new SKColor(100, 100, 100);
        float armW = halfSize * 0.28f;
        float armH = halfSize * 1.0f;
        // Arm-Swing Animation
        float armSwing = boss.IsMoving ? MathF.Sin(_globalTimer * 5f) * 3f : 0f;
        canvas.DrawRoundRect(cx - bodyW / 2 - armW + 2, cy - armH * 0.3f + armSwing, armW, armH, 3, 3, _fillPaint);
        canvas.DrawRoundRect(cx + bodyW / 2 - 2, cy - armH * 0.3f - armSwing, armW, armH, 3, 3, _fillPaint);
        // Fäuste
        _fillPaint.Color = new SKColor(75, 75, 75);
        canvas.DrawCircle(cx - bodyW / 2 - armW / 2 + 2, cy - armH * 0.3f + armH + armSwing, armW * 0.45f, _fillPaint);
        canvas.DrawCircle(cx + bodyW / 2 + armW / 2 - 2, cy - armH * 0.3f + armH - armSwing, armW * 0.45f, _fillPaint);

        // Augenbrauen: Schwere Stein-Leiste über den Augen
        float eyeY = cy - halfSize * 0.3f;
        float eyeSpacing = halfSize * 0.35f;
        _fillPaint.Color = new SKColor(65, 65, 65);
        canvas.DrawRoundRect(cx - eyeSpacing - 5, eyeY - 6, 10, 4, 1, 1, _fillPaint);
        canvas.DrawRoundRect(cx + eyeSpacing - 5, eyeY - 6, 10, 4, 1, 1, _fillPaint);

        // Augen: Rot leuchtend mit Pupillen-Pulsation
        float eyeR = 3.5f;
        float eyePulse = MathF.Sin(_globalTimer * 3f) * 0.3f + 0.7f;
        _fillPaint.Color = new SKColor(255, 40, 40);
        if (isNeon) _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(cx - eyeSpacing, eyeY, eyeR * eyePulse, _fillPaint);
        canvas.DrawCircle(cx + eyeSpacing, eyeY, eyeR * eyePulse, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Heller Kern
        _fillPaint.Color = new SKColor(255, 180, 80, 180);
        canvas.DrawCircle(cx - eyeSpacing, eyeY, eyeR * 0.35f, _fillPaint);
        canvas.DrawCircle(cx + eyeSpacing, eyeY, eyeR * 0.35f, _fillPaint);

        // Mund: Grimmig zusammengebissene Fels-Zähne
        _fillPaint.Color = new SKColor(50, 50, 50);
        float mouthY = cy + halfSize * 0.1f;
        canvas.DrawRoundRect(cx - halfSize * 0.25f, mouthY, halfSize * 0.5f, halfSize * 0.12f, 2, 2, _fillPaint);
        // Zähne
        _fillPaint.Color = new SKColor(120, 120, 120);
        for (int i = -2; i <= 2; i++)
            canvas.DrawRect(cx + i * halfSize * 0.1f - 1.5f, mouthY, 3, halfSize * 0.08f, _fillPaint);

        // Enrage: Orange-rote glühende Risse + Partikel-Aura
        if (boss.IsEnraged)
        {
            float enragePulse = MathF.Sin(_globalTimer * 6f) * 0.3f + 0.7f;
            byte enrageAlpha = (byte)(220 * enragePulse);
            _strokePaint.Color = new SKColor(255, 100, 20, enrageAlpha);
            _strokePaint.StrokeWidth = 2f;
            if (isNeon) _strokePaint.MaskFilter = _smallGlow;

            // Tiefe glühende Risse
            canvas.DrawLine(cx - bodyW * 0.4f, cy - bodyH * 0.35f,
                cx - bodyW * 0.05f, cy + bodyH * 0.2f, _strokePaint);
            canvas.DrawLine(cx + bodyW * 0.1f, cy - bodyH * 0.4f,
                cx + bodyW * 0.35f, cy + bodyH * 0.15f, _strokePaint);
            canvas.DrawLine(cx - bodyW * 0.2f, cy + bodyH * 0.1f,
                cx + bodyW * 0.2f, cy + bodyH * 0.35f, _strokePaint);
            _strokePaint.MaskFilter = null;

            // Fels-Splitter-Partikel die sich lösen
            _fillPaint.Color = new SKColor(120, 100, 80, enrageAlpha);
            for (int i = 0; i < 4; i++)
            {
                float splX = cx + MathF.Sin(_globalTimer * 2f + i * 2.3f) * halfSize * 0.9f;
                float splY = cy - halfSize * 0.5f - MathF.Abs(MathF.Sin(_globalTimer * 1.5f + i)) * 8f;
                canvas.DrawRect(splX - 2, splY - 1.5f, 4, 3, _fillPaint);
            }
        }
    }

    /// <summary>
    /// IceDragon (2x2): Hellblauer Drache mit Eis-Kristallen, Flügelschlag, Frost-Atem
    /// </summary>
    private void RenderIceDragon(SKCanvas canvas, BossEnemy boss, float size,
        float scale, float wobbleY, bool isNeon)
    {
        float halfSize = size * 0.45f * scale;
        float cx = boss.X;
        float cy = boss.Y + wobbleY;

        // Schatten
        _fillPaint.Color = new SKColor(0, 0, 0, 30);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(cx, cy + halfSize * 0.75f, halfSize * 0.65f, halfSize * 0.12f, _fillPaint);

        // Frost-Aura bei Enrage (expandierende Eis-Kristalle)
        if (boss.IsEnraged)
        {
            float frostPulse = MathF.Sin(_globalTimer * 5f) * 0.3f + 0.7f;
            byte frostAlpha = (byte)(70 * frostPulse);
            _glowPaint.Color = new SKColor(0, 191, 255, frostAlpha);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(cx, cy, halfSize * 1.2f, halfSize * 1.1f, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Fliegende Eis-Kristall-Partikel
            _fillPaint.Color = new SKColor(180, 230, 255, (byte)(150 * frostPulse));
            for (int i = 0; i < 6; i++)
            {
                float angle = _globalTimer * 1.5f + i * MathF.PI * 2f / 6f;
                float dist = halfSize * (0.8f + MathF.Sin(_globalTimer * 2f + i) * 0.2f);
                float ix = cx + MathF.Cos(angle) * dist;
                float iy = cy + MathF.Sin(angle) * dist;
                canvas.DrawRect(ix - 1.5f, iy - 1.5f, 3, 3, _fillPaint);
            }
        }

        // Neon-Aura
        if (isNeon)
        {
            _glowPaint.Color = new SKColor(0, 191, 255, 40);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(cx, cy, halfSize * 0.7f, halfSize * 0.6f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Flügel mit Flap-Animation (mehrgelenkig)
        float wingFlap = MathF.Sin(_globalTimer * 4f) * halfSize * 0.2f;
        float wingFlapTip = MathF.Sin(_globalTimer * 4f + 0.5f) * halfSize * 0.1f;

        _fillPaint.Color = new SKColor(0, 140, 200);
        // Linker Flügel (2-Segment)
        _fusePath.Reset();
        _fusePath.MoveTo(cx - halfSize * 0.35f, cy - halfSize * 0.05f);
        _fusePath.LineTo(cx - halfSize * 0.75f, cy - halfSize * 0.4f + wingFlap);
        _fusePath.LineTo(cx - halfSize * 0.95f, cy - halfSize * 0.55f + wingFlap + wingFlapTip);
        _fusePath.LineTo(cx - halfSize * 0.6f, cy - halfSize * 0.15f + wingFlap);
        _fusePath.LineTo(cx - halfSize * 0.25f, cy + halfSize * 0.2f);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
        // Flügel-Membran (dünner, heller)
        _fillPaint.Color = new SKColor(60, 180, 230, 100);
        canvas.DrawPath(_fusePath, _fillPaint);

        // Rechter Flügel (gespiegelt)
        _fillPaint.Color = new SKColor(0, 140, 200);
        _fusePath.Reset();
        _fusePath.MoveTo(cx + halfSize * 0.35f, cy - halfSize * 0.05f);
        _fusePath.LineTo(cx + halfSize * 0.75f, cy - halfSize * 0.4f + wingFlap);
        _fusePath.LineTo(cx + halfSize * 0.95f, cy - halfSize * 0.55f + wingFlap + wingFlapTip);
        _fusePath.LineTo(cx + halfSize * 0.6f, cy - halfSize * 0.15f + wingFlap);
        _fusePath.LineTo(cx + halfSize * 0.25f, cy + halfSize * 0.2f);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
        _fillPaint.Color = new SKColor(60, 180, 230, 100);
        canvas.DrawPath(_fusePath, _fillPaint);

        // Körper: Hellblau, oval mit Bauch-Highlight
        _fillPaint.Color = new SKColor(0, 191, 255);
        canvas.DrawOval(cx, cy, halfSize * 0.55f, halfSize * 0.5f, _fillPaint);
        _fillPaint.Color = new SKColor(100, 220, 255, 80);
        canvas.DrawOval(cx, cy + halfSize * 0.1f, halfSize * 0.3f, halfSize * 0.25f, _fillPaint);

        // Schuppen-Textur auf dem Körper
        _strokePaint.Color = new SKColor(0, 150, 200, 80);
        _strokePaint.StrokeWidth = 0.8f;
        _strokePaint.MaskFilter = null;
        for (int row = 0; row < 3; row++)
        {
            float sy = cy - halfSize * 0.15f + row * halfSize * 0.15f;
            for (int col = -1; col <= 1; col++)
            {
                float sx = cx + col * halfSize * 0.15f + (row % 2 == 0 ? halfSize * 0.07f : 0);
                canvas.DrawOval(sx, sy, halfSize * 0.06f, halfSize * 0.04f, _strokePaint);
            }
        }

        // Kopf: Drachen-Schnauze (Oval + spitze Schnauze)
        _fillPaint.Color = new SKColor(50, 210, 255);
        canvas.DrawOval(cx, cy - halfSize * 0.35f, halfSize * 0.3f, halfSize * 0.25f, _fillPaint);
        // Hörner
        _fillPaint.Color = new SKColor(0, 120, 180);
        _fusePath.Reset();
        _fusePath.MoveTo(cx - halfSize * 0.18f, cy - halfSize * 0.5f);
        _fusePath.LineTo(cx - halfSize * 0.28f, cy - halfSize * 0.7f);
        _fusePath.LineTo(cx - halfSize * 0.12f, cy - halfSize * 0.45f);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
        _fusePath.Reset();
        _fusePath.MoveTo(cx + halfSize * 0.18f, cy - halfSize * 0.5f);
        _fusePath.LineTo(cx + halfSize * 0.28f, cy - halfSize * 0.7f);
        _fusePath.LineTo(cx + halfSize * 0.12f, cy - halfSize * 0.45f);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);

        // Augen: Cyan-weiß leuchtend mit scharfen Pupillen
        float eyeY = cy - halfSize * 0.4f;
        float eyeSpacing = halfSize * 0.15f;
        float eyeR = 3.5f;
        _fillPaint.Color = new SKColor(200, 255, 255);
        if (isNeon) _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawOval(cx - eyeSpacing, eyeY, eyeR, eyeR * 0.8f, _fillPaint);
        canvas.DrawOval(cx + eyeSpacing, eyeY, eyeR, eyeR * 0.8f, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Schlitz-Pupillen (Drache!)
        _fillPaint.Color = new SKColor(0, 40, 80);
        canvas.DrawRect(cx - eyeSpacing - 0.5f, eyeY - eyeR * 0.6f, 1f, eyeR * 1.2f, _fillPaint);
        canvas.DrawRect(cx + eyeSpacing - 0.5f, eyeY - eyeR * 0.6f, 1f, eyeR * 1.2f, _fillPaint);

        // Schnauze + Nasenlöcher
        _fillPaint.Color = new SKColor(0, 100, 160);
        canvas.DrawCircle(cx - 2, cy - halfSize * 0.2f, 1.2f, _fillPaint);
        canvas.DrawCircle(cx + 2, cy - halfSize * 0.2f, 1.2f, _fillPaint);

        // Frost-Atem Idle (subtiler Dampf bei jedem Ausatmen)
        float breathePhase = _globalTimer % 3f;
        if (breathePhase < 0.5f)
        {
            float breatheAlpha = (1f - breathePhase / 0.5f);
            _fillPaint.Color = new SKColor(180, 230, 255, (byte)(40 * breatheAlpha));
            float bx = cx;
            float by = cy - halfSize * 0.15f - breathePhase * 8f;
            canvas.DrawOval(bx, by, 4f + breathePhase * 3f, 2f + breathePhase * 2f, _fillPaint);
        }
    }

    /// <summary>
    /// FireDemon (2x2): Dunkelroter Körper mit Lava-Adern, Flammenkrone, Flammen-Partikel
    /// </summary>
    private void RenderFireDemon(SKCanvas canvas, BossEnemy boss, float size,
        float scale, float wobbleY, bool isNeon)
    {
        float halfSize = size * 0.45f * scale;
        float cx = boss.X;
        float cy = boss.Y + wobbleY;

        // Schatten (rötlich getönt)
        _fillPaint.Color = new SKColor(40, 0, 0, 35);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(cx, cy + halfSize * 0.8f, halfSize * 0.6f, halfSize * 0.12f, _fillPaint);

        // Flammen-Aura bei Enrage (intensiver, mit Flammen-Partikel)
        if (boss.IsEnraged)
        {
            float flamePulse = MathF.Sin(_globalTimer * 8f) * 0.4f + 0.6f;
            byte flameAlpha = (byte)(90 * flamePulse);
            _glowPaint.Color = new SKColor(255, 140, 0, flameAlpha);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(cx, cy, halfSize * 1.3f, halfSize * 1.2f, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Fliegende Funken bei Enrage
            _fillPaint.Color = new SKColor(255, 200, 50, (byte)(200 * flamePulse));
            for (int i = 0; i < 5; i++)
            {
                float sparkAngle = _globalTimer * 3f + i * MathF.PI * 2f / 5f;
                float sparkDist = halfSize * (0.7f + MathF.Sin(_globalTimer * 4f + i * 1.7f) * 0.3f);
                float sx = cx + MathF.Cos(sparkAngle) * sparkDist;
                float sy = cy + MathF.Sin(sparkAngle) * sparkDist - MathF.Abs(MathF.Sin(_globalTimer * 5f + i)) * 5f;
                canvas.DrawCircle(sx, sy, 2f, _fillPaint);
            }
        }

        // Neon-Aura
        if (isNeon)
        {
            _glowPaint.Color = new SKColor(139, 0, 0, 50);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(cx, cy, halfSize * 0.7f, halfSize * 0.65f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Körper: Dunkelrot, rund mit Idle-Atmen
        float breathe = MathF.Sin(_globalTimer * 2f) * 0.03f;
        _fillPaint.Color = new SKColor(139, 0, 0);
        canvas.DrawOval(cx, cy, halfSize * 0.55f * (1f + breathe), halfSize * 0.55f * (1f - breathe), _fillPaint);

        // Lava-Adern im Körper (glühende orangene Linien)
        float lavaGlow = MathF.Sin(_globalTimer * 3f) * 0.3f + 0.7f;
        _strokePaint.Color = new SKColor(255, 120, 0, (byte)(150 * lavaGlow));
        _strokePaint.StrokeWidth = 1.2f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawLine(cx - halfSize * 0.3f, cy - halfSize * 0.2f,
            cx, cy + halfSize * 0.3f, _strokePaint);
        canvas.DrawLine(cx + halfSize * 0.1f, cy - halfSize * 0.35f,
            cx + halfSize * 0.3f, cy + halfSize * 0.1f, _strokePaint);
        canvas.DrawLine(cx - halfSize * 0.15f, cy + halfSize * 0.1f,
            cx + halfSize * 0.2f, cy + halfSize * 0.35f, _strokePaint);
        _strokePaint.MaskFilter = null;

        // Innerer Kern: Heller Glow-Punkt (pulsierend)
        byte coreAlpha = (byte)(60 + 40 * MathF.Sin(_globalTimer * 4f));
        _fillPaint.Color = new SKColor(255, 100, 30, coreAlpha);
        _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(cx, cy, halfSize * 0.2f, _fillPaint);
        _fillPaint.MaskFilter = null;

        // Flammenkrone: 7 Zacken mit mehrschichtigem Rendering
        float crownBase = cy - halfSize * 0.45f;

        // Äußere orangene Flammen (größer)
        for (int i = -3; i <= 3; i++)
        {
            float fx = cx + i * halfSize * 0.14f;
            float fh = halfSize * 0.45f * (1f - MathF.Abs(i) * 0.1f)
                        + MathF.Sin(_globalTimer * 9f + i * 1.5f) * 3f;
            _fillPaint.Color = new SKColor(255, 100, 0, 180);
            _fusePath.Reset();
            _fusePath.MoveTo(fx - halfSize * 0.06f, crownBase);
            _fusePath.LineTo(fx + MathF.Sin(_globalTimer * 7f + i) * 1.5f, crownBase - fh);
            _fusePath.LineTo(fx + halfSize * 0.06f, crownBase);
            _fusePath.Close();
            canvas.DrawPath(_fusePath, _fillPaint);
        }
        // Innere gelbe Flammen (kleiner)
        for (int i = -2; i <= 2; i++)
        {
            float fx = cx + i * halfSize * 0.16f;
            float fh = halfSize * 0.3f * (1f - MathF.Abs(i) * 0.12f)
                        + MathF.Sin(_globalTimer * 10f + i * 2f) * 2f;
            _fillPaint.Color = new SKColor(255, 220, 60, 200);
            _fusePath.Reset();
            _fusePath.MoveTo(fx - halfSize * 0.04f, crownBase);
            _fusePath.LineTo(fx, crownBase - fh);
            _fusePath.LineTo(fx + halfSize * 0.04f, crownBase);
            _fusePath.Close();
            canvas.DrawPath(_fusePath, _fillPaint);
        }

        // Augen: Gelb leuchtend mit Feuer-Pupillen
        float eyeY = cy - halfSize * 0.1f;
        float eyeSpacing = halfSize * 0.25f;
        float eyeR = 3.5f;
        _fillPaint.Color = new SKColor(255, 255, 0);
        if (isNeon) _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(cx - eyeSpacing, eyeY, eyeR, _fillPaint);
        canvas.DrawCircle(cx + eyeSpacing, eyeY, eyeR, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Feuer-Pupillen (rote Schlitze die flackern)
        float pupilFlicker = MathF.Sin(_globalTimer * 12f) * 0.3f;
        _fillPaint.Color = new SKColor(200, 0, 0);
        canvas.DrawRect(cx - eyeSpacing - 0.5f, eyeY - eyeR * (0.5f + pupilFlicker), 1f, eyeR * (1f + pupilFlicker * 2), _fillPaint);
        canvas.DrawRect(cx + eyeSpacing - 0.5f, eyeY - eyeR * (0.5f + pupilFlicker), 1f, eyeR * (1f + pupilFlicker * 2), _fillPaint);

        // Böses Grinsen mit scharfen Zähnen
        _fillPaint.Color = new SKColor(80, 0, 0);
        float mouthY = cy + halfSize * 0.15f;
        _fusePath.Reset();
        _fusePath.MoveTo(cx - halfSize * 0.22f, mouthY);
        _fusePath.QuadTo(cx, mouthY + halfSize * 0.18f, cx + halfSize * 0.22f, mouthY);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
        // Zähne (kleine Dreiecke)
        _fillPaint.Color = new SKColor(255, 200, 100);
        for (int i = -2; i <= 2; i++)
        {
            float tx = cx + i * halfSize * 0.08f;
            canvas.DrawRect(tx - 1, mouthY, 2, 2.5f, _fillPaint);
        }
    }

    /// <summary>
    /// ShadowMaster (2x2): Dunkelvioletter Umhang-Träger mit Kapuze, Schatten-Ranken, Teleport-Effekt
    /// </summary>
    private void RenderShadowMaster(SKCanvas canvas, BossEnemy boss, float size,
        float scale, float wobbleY, bool isNeon)
    {
        float halfSize = size * 0.45f * scale;
        float cx = boss.X;
        float cy = boss.Y + wobbleY;

        // Schatten auf dem Boden (dunkel, diffus)
        _fillPaint.Color = new SKColor(20, 0, 40, 40);
        _fillPaint.MaskFilter = _mediumGlow;
        canvas.DrawOval(cx, cy + halfSize * 0.75f, halfSize * 0.7f, halfSize * 0.15f, _fillPaint);
        _fillPaint.MaskFilter = null;

        // Schatten-Wisps bei Enrage (orbit + Tentakel)
        if (boss.IsEnraged)
        {
            float wispPulse = MathF.Sin(_globalTimer * 4f) * 0.3f + 0.7f;
            // Orbiting Schatten-Kugeln
            for (int i = 0; i < 6; i++)
            {
                float angle = _globalTimer * 2.5f + i * MathF.PI * 2f / 6f;
                float dist = halfSize * (0.8f + MathF.Sin(_globalTimer * 1.5f + i * 1.2f) * 0.15f);
                float wispX = cx + MathF.Cos(angle) * dist;
                float wispY = cy + MathF.Sin(angle) * dist * 0.7f;
                byte wispAlpha = (byte)(120 * wispPulse);
                _fillPaint.Color = new SKColor(75, 0, 130, wispAlpha);
                _fillPaint.MaskFilter = _smallGlow;
                canvas.DrawCircle(wispX, wispY, 3.5f, _fillPaint);
                // Schwanz-Spur
                float tailX = cx + MathF.Cos(angle - 0.4f) * dist * 0.9f;
                float tailY = cy + MathF.Sin(angle - 0.4f) * dist * 0.7f * 0.9f;
                _fillPaint.Color = new SKColor(50, 0, 100, (byte)(60 * wispPulse));
                canvas.DrawCircle(tailX, tailY, 2f, _fillPaint);
            }
            _fillPaint.MaskFilter = null;
        }

        // Neon-Aura
        if (isNeon)
        {
            _glowPaint.Color = new SKColor(75, 0, 130, 50);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(cx, cy, halfSize * 0.7f, halfSize * 0.65f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Umhang: Trapez mit welligem Saum (schwebend)
        float cloakWave = MathF.Sin(_globalTimer * 2.5f) * 2f;
        _fillPaint.Color = new SKColor(35, 0, 60);
        _fillPaint.MaskFilter = null;
        _fusePath.Reset();
        _fusePath.MoveTo(cx - halfSize * 0.35f, cy - halfSize * 0.2f);
        _fusePath.LineTo(cx - halfSize * 0.6f, cy + halfSize * 0.55f);
        // Welliger Saum
        float saumY = cy + halfSize * 0.6f;
        _fusePath.QuadTo(cx - halfSize * 0.3f, saumY + cloakWave + 3, cx, saumY);
        _fusePath.QuadTo(cx + halfSize * 0.3f, saumY - cloakWave + 3, cx + halfSize * 0.6f, saumY - 2);
        _fusePath.LineTo(cx + halfSize * 0.35f, cy - halfSize * 0.2f);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);

        // Umhang-Innenseite (dunkler)
        _fillPaint.Color = new SKColor(25, 0, 45);
        canvas.DrawOval(cx, cy + halfSize * 0.15f, halfSize * 0.35f, halfSize * 0.45f, _fillPaint);

        // Körper: Dunkelviolett, oval
        _fillPaint.Color = new SKColor(75, 0, 130);
        canvas.DrawOval(cx, cy - halfSize * 0.1f, halfSize * 0.4f, halfSize * 0.35f, _fillPaint);

        // Kapuze: Spitze Form oben
        _fillPaint.Color = new SKColor(45, 0, 80);
        _fusePath.Reset();
        _fusePath.MoveTo(cx - halfSize * 0.35f, cy - halfSize * 0.15f);
        _fusePath.QuadTo(cx, cy - halfSize * 0.7f, cx + halfSize * 0.35f, cy - halfSize * 0.15f);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);

        // Dunkler Kapuzen-Schatten (nur die Augen leuchten heraus)
        _fillPaint.Color = new SKColor(15, 0, 25);
        canvas.DrawOval(cx, cy - halfSize * 0.3f, halfSize * 0.22f, halfSize * 0.15f, _fillPaint);

        // Augen: Violett leuchtend (starker Glow, pulsierend)
        float eyeY = cy - halfSize * 0.28f;
        float eyeSpacing = halfSize * 0.14f;
        float eyeR = 3f;
        float eyeGlow = MathF.Sin(_globalTimer * 3f) * 0.3f + 0.7f;
        _fillPaint.Color = new SKColor(200, 100, 255, (byte)(255 * eyeGlow));
        _fillPaint.MaskFilter = isNeon ? _mediumGlow : _smallGlow;
        canvas.DrawCircle(cx - eyeSpacing, eyeY, eyeR, _fillPaint);
        canvas.DrawCircle(cx + eyeSpacing, eyeY, eyeR, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Heller Kern in den Augen
        _fillPaint.Color = new SKColor(240, 200, 255, 200);
        canvas.DrawCircle(cx - eyeSpacing, eyeY, eyeR * 0.3f, _fillPaint);
        canvas.DrawCircle(cx + eyeSpacing, eyeY, eyeR * 0.3f, _fillPaint);

        // Schatten-Hände die aus dem Umhang ragen (Idle: langsames Schweben)
        float handFloat = MathF.Sin(_globalTimer * 2f) * 3f;
        _fillPaint.Color = new SKColor(60, 0, 100, 180);
        canvas.DrawOval(cx - halfSize * 0.5f, cy + halfSize * 0.1f + handFloat, 4, 3, _fillPaint);
        canvas.DrawOval(cx + halfSize * 0.5f, cy + halfSize * 0.1f - handFloat, 4, 3, _fillPaint);
    }

    /// <summary>
    /// FinalBoss (3x3): Imposanter schwarzer Körper, goldene Krone, 4 Element-Orbits, Aura
    /// </summary>
    private void RenderFinalBoss(SKCanvas canvas, BossEnemy boss, float size,
        float scale, float wobbleY, bool isNeon)
    {
        float halfSize = size * 0.42f * scale;
        float cx = boss.X;
        float cy = boss.Y + wobbleY;

        // Großer Schatten (3x3 Boss)
        _fillPaint.Color = new SKColor(0, 0, 0, 40);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(cx, cy + halfSize * 0.8f, halfSize * 0.75f, halfSize * 0.18f, _fillPaint);

        // Multi-Color Glow-Aura bei Enrage (intensiver, 4 Element-Ringe)
        if (boss.IsEnraged)
        {
            SKColor[] elementColors =
            {
                new(120, 120, 120, 60),  // Stein
                new(0, 200, 255, 60),    // Eis
                new(255, 80, 0, 60),     // Feuer
                new(150, 0, 230, 60)     // Schatten
            };
            float rotAngle = _globalTimer * 3f;
            for (int i = 0; i < 4; i++)
            {
                float angle = rotAngle + i * MathF.PI / 2f;
                float auraX = cx + MathF.Cos(angle) * halfSize * 0.45f;
                float auraY = cy + MathF.Sin(angle) * halfSize * 0.35f;
                _glowPaint.Color = elementColors[i];
                _glowPaint.MaskFilter = _mediumGlow;
                canvas.DrawOval(auraX, auraY, halfSize * 0.7f, halfSize * 0.6f, _glowPaint);
            }
            _glowPaint.MaskFilter = null;

            // Element-Blitze zwischen den Orbits
            _strokePaint.StrokeWidth = 1.5f;
            _strokePaint.MaskFilter = _smallGlow;
            for (int i = 0; i < 4; i++)
            {
                float a1 = rotAngle + i * MathF.PI / 2f;
                float a2 = rotAngle + ((i + 1) % 4) * MathF.PI / 2f;
                float flash = MathF.Sin(_globalTimer * 8f + i * 2f);
                if (flash > 0.7f)
                {
                    _strokePaint.Color = elementColors[i].WithAlpha((byte)(100 * (flash - 0.7f) / 0.3f));
                    canvas.DrawLine(
                        cx + MathF.Cos(a1) * halfSize * 0.4f, cy + MathF.Sin(a1) * halfSize * 0.3f,
                        cx + MathF.Cos(a2) * halfSize * 0.4f, cy + MathF.Sin(a2) * halfSize * 0.3f,
                        _strokePaint);
                }
            }
            _strokePaint.MaskFilter = null;
        }

        // Neon-Aura
        if (isNeon)
        {
            _glowPaint.Color = new SKColor(255, 215, 0, 40);
            _glowPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(cx, cy, halfSize * 0.75f, halfSize * 0.7f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Idle-Atmen
        float breathe = MathF.Sin(_globalTimer * 1.5f) * 0.025f;

        // Körper: Schwarz mit goldenem Schimmer
        _fillPaint.Color = new SKColor(22, 22, 40);
        float bodyRX = halfSize * 0.6f * (1f + breathe);
        float bodyRY = halfSize * 0.55f * (1f - breathe);
        canvas.DrawOval(cx, cy, bodyRX, bodyRY, _fillPaint);

        // Rüstungsplatten (goldene Linien)
        _strokePaint.Color = new SKColor(200, 170, 50, 120);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawOval(cx, cy, bodyRX * 0.85f, bodyRY * 0.85f, _strokePaint);
        canvas.DrawLine(cx, cy - bodyRY * 0.7f, cx, cy + bodyRY * 0.7f, _strokePaint);

        // Innerer Kern: Pulsierender Gold-Glow (Herz des Bosses)
        float coreGlow = MathF.Sin(_globalTimer * 3f) * 0.3f + 0.7f;
        _fillPaint.Color = new SKColor(120, 100, 30, (byte)(80 * coreGlow));
        _fillPaint.MaskFilter = _smallGlow;
        canvas.DrawCircle(cx, cy, halfSize * 0.2f, _fillPaint);
        _fillPaint.MaskFilter = null;

        // Goldene Krone: 5 Zacken mit Edelstein-Akzenten
        _fillPaint.Color = new SKColor(255, 215, 0);
        float crownBase = cy - halfSize * 0.5f;
        float crownH = halfSize * 0.38f;

        for (int i = -2; i <= 2; i++)
        {
            float fx = cx + i * halfSize * 0.15f;
            float fh = crownH * (1f - MathF.Abs(i) * 0.08f)
                        + MathF.Sin(_globalTimer * 3f + i * 1.2f) * 1.5f;

            _fusePath.Reset();
            _fusePath.MoveTo(fx - halfSize * 0.065f, crownBase);
            _fusePath.LineTo(fx, crownBase - fh);
            _fusePath.LineTo(fx + halfSize * 0.065f, crownBase);
            _fusePath.Close();
            canvas.DrawPath(_fusePath, _fillPaint);
        }

        // Kronen-Basis mit Edelsteinen
        _fillPaint.Color = new SKColor(255, 215, 0);
        canvas.DrawRect(cx - halfSize * 0.38f, crownBase, halfSize * 0.76f, halfSize * 0.07f, _fillPaint);
        // 3 Edelsteine auf der Krone
        SKColor[] gemColors = { new(255, 0, 0), new(0, 150, 255), new(100, 255, 50) };
        for (int g = -1; g <= 1; g++)
        {
            _fillPaint.Color = gemColors[g + 1];
            if (isNeon) _fillPaint.MaskFilter = _smallGlow;
            canvas.DrawCircle(cx + g * halfSize * 0.15f, crownBase + halfSize * 0.035f, 2f, _fillPaint);
        }
        _fillPaint.MaskFilter = null;

        // 4 Element-Akzent-Orbits um den Körper (größer, mit Spur)
        float accentR = 3f;
        float accentDist = halfSize * 0.52f;
        SKColor[] accents =
        {
            new(150, 150, 150), // Stein
            new(0, 191, 255),   // Eis
            new(255, 60, 0),    // Feuer
            new(160, 0, 255)    // Schatten
        };
        for (int i = 0; i < 4; i++)
        {
            float angle = _globalTimer * 1.5f + i * MathF.PI / 2f;
            float ax = cx + MathF.Cos(angle) * accentDist;
            float ay = cy + MathF.Sin(angle) * accentDist * 0.8f;
            // Spur
            _fillPaint.Color = accents[i].WithAlpha(40);
            float trailAngle = angle - 0.3f;
            float tx = cx + MathF.Cos(trailAngle) * accentDist;
            float ty = cy + MathF.Sin(trailAngle) * accentDist * 0.8f;
            canvas.DrawCircle(tx, ty, accentR * 0.6f, _fillPaint);
            // Haupt-Orbit
            _fillPaint.Color = accents[i];
            if (isNeon) _fillPaint.MaskFilter = _smallGlow;
            canvas.DrawCircle(ax, ay, accentR, _fillPaint);
        }
        _fillPaint.MaskFilter = null;

        // Augen: Gold leuchtend mit Schlitz-Pupillen
        float eyeY = cy - halfSize * 0.12f;
        float eyeSpacing = halfSize * 0.2f;
        float eyeR = 4.5f;
        _fillPaint.Color = new SKColor(255, 215, 0);
        _fillPaint.MaskFilter = isNeon ? _mediumGlow : _smallGlow;
        canvas.DrawCircle(cx - eyeSpacing, eyeY, eyeR, _fillPaint);
        canvas.DrawCircle(cx + eyeSpacing, eyeY, eyeR, _fillPaint);
        _fillPaint.MaskFilter = null;
        // Schlitz-Pupillen
        _fillPaint.Color = new SKColor(80, 0, 0);
        canvas.DrawRect(cx - eyeSpacing - 0.5f, eyeY - eyeR * 0.6f, 1f, eyeR * 1.2f, _fillPaint);
        canvas.DrawRect(cx + eyeSpacing - 0.5f, eyeY - eyeR * 0.6f, 1f, eyeR * 1.2f, _fillPaint);

        // Gold-Shimmer (pulsierender doppelter Rand)
        float shimmer = MathF.Sin(_globalTimer * 4f) * 0.3f + 0.7f;
        _strokePaint.Color = new SKColor(255, 215, 0, (byte)(60 * shimmer));
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawOval(cx, cy, bodyRX + 2, bodyRY + 2, _strokePaint);
        _strokePaint.Color = new SKColor(255, 215, 0, (byte)(30 * shimmer));
        canvas.DrawOval(cx, cy, bodyRX + 5, bodyRY + 5, _strokePaint);
        _strokePaint.MaskFilter = null;
    }

    /// <summary>
    /// Boss-Death-Animation: Größer als normale Gegner, mehrstufig
    /// </summary>
    private void RenderBossDeath(SKCanvas canvas, BossEnemy boss, float cs)
    {
        float progress = boss.DeathTimer / 0.8f;
        if (progress > 1f) progress = 1f;
        byte alpha = (byte)(255 * (1f - progress));
        float bossPixelSize = boss.BossSize * cs;

        // Boss-Farbe basierend auf Typ
        SKColor bodyColor = boss.BossKind switch
        {
            BossType.StoneGolem => new SKColor(85, 85, 85, alpha),
            BossType.IceDragon => new SKColor(0, 191, 255, alpha),
            BossType.FireDemon => new SKColor(139, 0, 0, alpha),
            BossType.ShadowMaster => new SKColor(75, 0, 130, alpha),
            BossType.FinalBoss => new SKColor(26, 26, 46, alpha),
            _ => new SKColor(85, 85, 85, alpha)
        };

        _fillPaint.Color = bodyColor;
        _fillPaint.MaskFilter = null;

        // Größerer Squash/Stretch (skaliert mit BossSize)
        float squashX = 1f + progress * 0.8f;
        float squashY = 1f - progress * 0.5f;
        float drawSize = bossPixelSize * 0.4f * (1f - progress * 0.3f);
        float rx = drawSize * squashX;
        float ry = drawSize * squashY;
        canvas.DrawOval(boss.X, boss.Y + progress * 6f, rx, ry, _fillPaint);

        // Blitz-Effekt beim Tod (erste 30% der Animation)
        if (progress < 0.3f)
        {
            byte flashAlpha = (byte)(200 * (1f - progress / 0.3f));
            SKColor flashColor = boss.BossKind switch
            {
                BossType.StoneGolem => new SKColor(255, 200, 100, flashAlpha),
                BossType.IceDragon => new SKColor(200, 240, 255, flashAlpha),
                BossType.FireDemon => new SKColor(255, 200, 50, flashAlpha),
                BossType.ShadowMaster => new SKColor(200, 150, 255, flashAlpha),
                BossType.FinalBoss => new SKColor(255, 255, 200, flashAlpha),
                _ => new SKColor(255, 255, 255, flashAlpha)
            };
            _fillPaint.Color = flashColor;
            _fillPaint.MaskFilter = _mediumGlow;
            canvas.DrawOval(boss.X, boss.Y, bossPixelSize * 0.5f, bossPixelSize * 0.5f, _fillPaint);
            _fillPaint.MaskFilter = null;
        }
    }

    /// <summary>
    /// HP-Balken über dem Boss: Grün/Gelb/Rot je nach verbleibenden HP
    /// </summary>
    private void RenderBossHPBar(SKCanvas canvas, BossEnemy boss)
    {
        float cs = GameGrid.CELL_SIZE;
        float bossPixelSize = boss.BossSize * cs;
        float barWidth = bossPixelSize;
        float barHeight = 4f;
        float barX = boss.X - barWidth / 2f;
        float barY = boss.Y - bossPixelSize * 0.45f - 8f;

        // Hintergrund
        _fillPaint.Color = new SKColor(51, 51, 51, 200);
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(barX, barY, barWidth, barHeight, _fillPaint);

        // HP-Füllung
        float hpPercent = boss.HitPoints / (float)boss.MaxHitPoints;
        hpPercent = Math.Clamp(hpPercent, 0f, 1f);

        SKColor hpColor;
        if (hpPercent > 0.66f)
            hpColor = new SKColor(0, 255, 0);       // Grün
        else if (hpPercent > 0.33f)
            hpColor = new SKColor(255, 215, 0);      // Gelb
        else
            hpColor = new SKColor(255, 0, 0);         // Rot

        _fillPaint.Color = hpColor;
        canvas.DrawRect(barX, barY, barWidth * hpPercent, barHeight, _fillPaint);

        // Border
        _strokePaint.Color = SKColors.Black;
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        canvas.DrawRect(barX, barY, barWidth, barHeight, _strokePaint);

        // Enrage: Pulsierender roter Rand
        if (boss.IsEnraged)
        {
            float enragePulse = MathF.Sin(_globalTimer * 8f) * 0.4f + 0.6f;
            byte enrageAlpha = (byte)(180 * enragePulse);
            _strokePaint.Color = new SKColor(255, 0, 0, enrageAlpha);
            _strokePaint.StrokeWidth = 1.5f;
            canvas.DrawRect(barX - 1, barY - 1, barWidth + 2, barHeight + 2, _strokePaint);
        }
    }

    /// <summary>
    /// Rote pulsierende Warnzonen auf den Boss-Angriffszellen.
    /// Intensität steigt mit sinkendem TelegraphTimer.
    /// </summary>
    private void RenderBossAttackWarning(SKCanvas canvas, BossEnemy boss)
    {
        if (boss.AttackTargetCells.Count == 0) return;

        float cs = GameGrid.CELL_SIZE;
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;

        // Intensität: Bei Telegraph steigend, bei Angriff voll
        float intensity;
        if (boss.IsTelegraphing)
        {
            // Telegraph: 2s → 0s, Intensität steigt von 0.2 → 1.0
            float telegraphProgress = 1f - (boss.TelegraphTimer / 2f);
            intensity = 0.2f + telegraphProgress * 0.8f;
        }
        else
        {
            // Aktiver Angriff: Volle Intensität mit schnellem Puls
            intensity = 1f;
        }

        // Pulsieren (schneller bei höherer Intensität)
        float pulse = MathF.Sin(_globalTimer * (8f + intensity * 8f)) * 0.3f + 0.7f;
        byte alpha = (byte)(80 * intensity * pulse);
        if (alpha < 5) return;

        // Dunkelrot für Boss-Angriff (unterscheidbar von normaler Danger-Warning)
        var warningColor = isNeon
            ? new SKColor(200, 20, 60, alpha)
            : new SKColor(180, 30, 30, alpha);

        _fillPaint.Color = warningColor;
        _fillPaint.MaskFilter = null;

        foreach (var (cellX, cellY) in boss.AttackTargetCells)
        {
            canvas.DrawRect(cellX * cs, cellY * cs, cs, cs, _fillPaint);
        }

        // Bei hoher Intensität: Zusätzlicher Glow-Rand auf Angriffszellen
        if (intensity > 0.6f)
        {
            byte glowAlpha = (byte)(40 * (intensity - 0.6f) / 0.4f * pulse);
            _strokePaint.Color = isNeon
                ? new SKColor(255, 40, 80, glowAlpha)
                : new SKColor(220, 50, 30, glowAlpha);
            _strokePaint.StrokeWidth = 2f;
            _strokePaint.MaskFilter = _smallGlow;

            foreach (var (cellX, cellY) in boss.AttackTargetCells)
            {
                canvas.DrawRect(cellX * cs + 1, cellY * cs + 1, cs - 2, cs - 2, _strokePaint);
            }
            _strokePaint.MaskFilter = null;
        }
    }
}
