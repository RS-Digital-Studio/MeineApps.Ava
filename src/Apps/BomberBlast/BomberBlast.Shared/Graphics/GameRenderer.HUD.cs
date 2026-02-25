using BomberBlast.Models;
using BomberBlast.Models.Cards;
using BomberBlast.Models.Dungeon;
using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Services;
using SkiaSharp;

namespace BomberBlast.Graphics;

public partial class GameRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // HUD (right side panel)
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderHUD(SKCanvas canvas, float remainingTime, int score, int lives, Player? player)
    {
        bool isNeon = _styleService.CurrentStyle == GameVisualStyle.Neon;
        float x = _hudX;
        float y = _hudY;
        float w = _hudWidth;
        float h = _hudHeight;

        // Background panel
        if (_hudGradientShader == null || Math.Abs(_lastHudShaderHeight - h) > 1f)
        {
            _hudGradientShader?.Dispose();
            _hudGradientShader = SKShader.CreateLinearGradient(
                new SKPoint(x, y),
                new SKPoint(x, y + h),
                new[] { _palette.HudBg, _palette.HudBg.WithAlpha(210) },
                null, SKShaderTileMode.Clamp);
            _lastHudShaderHeight = h;
        }

        _fillPaint.Shader = _hudGradientShader;
        _fillPaint.MaskFilter = null;
        canvas.DrawRect(x, y, w, h, _fillPaint);
        _fillPaint.Shader = null;

        // Left border line
        _strokePaint.Color = _palette.HudBorder;
        _strokePaint.StrokeWidth = isNeon ? 1.5f : 1f;
        _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
        canvas.DrawLine(x, y, x, y + h, _strokePaint);
        _strokePaint.MaskFilter = null;

        float cx = x + w / 2f;
        float cy = y + 20;

        // ── TIME / KILLS (Survival) ──
        _textPaint.MaskFilter = null;
        if (IsSurvivalMode)
        {
            // Survival: Kills-Anzeige + verstrichene Zeit
            _textPaint.Color = _palette.HudText.WithAlpha(150);
            canvas.DrawText(HudLabelKills, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
            cy += 24;

            _textPaint.Color = new SKColor(255, 80, 50); // Rot für Kills
            _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
            string killStr = SurvivalKills.ToString();
            canvas.DrawText(killStr, cx, cy, SKTextAlign.Center, _hudFontLarge, _textPaint);
            _textPaint.MaskFilter = null;
            cy += 20;

            // Verstrichene Zeit (kleiner, unter den Kills)
            _textPaint.Color = _palette.HudText.WithAlpha(120);
            int survMins = (int)(remainingTime / 60);
            int survSecs = (int)(remainingTime % 60);
            canvas.DrawText($"{survMins}:{survSecs:D2}", cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
            cy += 16;
        }
        else
        {
            _textPaint.Color = _palette.HudText.WithAlpha(150);
            canvas.DrawText(HudLabelTime, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
            cy += 24;

            bool timeWarning = remainingTime <= 30;
            _textPaint.Color = timeWarning ? _palette.HudTimeWarning : _palette.HudText;
            _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
            int timeInt = (int)remainingTime;
            if (timeInt != _lastTimeValue)
            {
                _lastTimeValue = timeInt;
                _lastTimeString = $"{timeInt:D3}";
            }
            canvas.DrawText(_lastTimeString, cx, cy, SKTextAlign.Center, _hudFontLarge, _textPaint);
            _textPaint.MaskFilter = null;
            cy += 32;
        }

        // Separator
        _strokePaint.Color = _palette.HudBorder.WithAlpha(80);
        _strokePaint.StrokeWidth = 1;
        _strokePaint.MaskFilter = null;
        canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
        cy += 16;

        // ── SCORE ──
        _textPaint.Color = _palette.HudText.WithAlpha(150);
        canvas.DrawText(HudLabelScore, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
        cy += 22;

        _textPaint.Color = _palette.HudAccent;
        _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
        if (score != _lastScoreValue)
        {
            _lastScoreValue = score;
            _lastScoreString = $"{score:D6}";
        }
        canvas.DrawText(_lastScoreString, cx, cy, SKTextAlign.Center, _hudFontMedium, _textPaint);
        _textPaint.MaskFilter = null;
        cy += 28;

        // ── COMBO (nur wenn aktiv) ──
        if (ComboCount >= 2)
        {
            cy += 4;
            // Alpha-Fade bei ablaufendem Timer (sanftes Ausblenden statt abruptem Verschwinden)
            float comboAlphaFactor = ComboTimer < 0.5f ? ComboTimer / 0.5f : 1f;

            // Combo-Text (pulsierend, farbig nach Stärke)
            float comboPulse = MathF.Sin(_globalTimer * 12f) * 0.15f;
            float comboScale = 1f + comboPulse;
            var comboColor = ComboCount >= 5
                ? new SKColor(255, 50, 50)     // Rot ab x5 (MEGA)
                : ComboCount >= 4
                    ? new SKColor(255, 80, 30)  // Orange-Rot ab x4
                    : new SKColor(255, 165, 0); // Orange x2-x3
            comboColor = comboColor.WithAlpha((byte)(255 * comboAlphaFactor));

            string comboStr = ComboCount >= 5 ? $"MEGA x{ComboCount}" : $"x{ComboCount}";
            _textPaint.Color = comboColor;
            _textPaint.MaskFilter = isNeon ? _hudTextGlow : SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
            float originalFontSize = _hudFontMedium.Size;
            _hudFontMedium.Size = originalFontSize * comboScale;
            canvas.DrawText(comboStr, cx, cy, SKTextAlign.Center, _hudFontMedium, _textPaint);
            _hudFontMedium.Size = originalFontSize;
            _textPaint.MaskFilter = null;
            cy += 16;

            // Timer-Bar (schrumpft von voll nach leer)
            float barW = w - 30;
            float barH = 3f;
            float barX = x + 15;
            float timerFrac = Math.Clamp(ComboTimer / 2f, 0f, 1f); // 2s = COMBO_WINDOW

            // Hintergrund (dunkel)
            _fillPaint.Color = new SKColor(60, 60, 60, (byte)(255 * comboAlphaFactor));
            _fillPaint.MaskFilter = null;
            _fillPaint.Shader = null;
            canvas.DrawRoundRect(barX, cy, barW, barH, 1.5f, 1.5f, _fillPaint);

            // Füllstand (farbig)
            _fillPaint.Color = comboColor.WithAlpha((byte)(200 * comboAlphaFactor));
            canvas.DrawRoundRect(barX, cy, barW * timerFrac, barH, 1.5f, 1.5f, _fillPaint);
            cy += 10;
        }

        // Separator
        canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
        cy += 16;

        // ── LIVES (heart icons) ──
        _textPaint.Color = _palette.HudText.WithAlpha(150);
        canvas.DrawText(HudLabelLives, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
        cy += 20;

        float heartSize = 12;
        float heartsWidth = lives * (heartSize + 4) - 4;
        float heartStartX = cx - heartsWidth / 2f;

        for (int i = 0; i < lives; i++)
        {
            float hx = heartStartX + i * (heartSize + 4) + heartSize / 2f;
            RenderHeart(canvas, hx, cy, heartSize * 0.5f);
        }
        cy += heartSize + 16;

        // ── ENEMIES (Gegner-Zähler) ──
        if (EnemiesRemaining > 0 && !IsSurvivalMode)
        {
            // Separator
            canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
            cy += 14;

            // Mini-Skull + Anzahl
            float skullCx = cx - 12;
            RenderMiniSkull(canvas, skullCx, cy + 2, 6f, isNeon);
            _textPaint.Color = EnemiesRemaining <= 2
                ? new SKColor(255, 80, 50)   // Rot: fast geschafft
                : _palette.HudText;
            _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
            canvas.DrawText($"x{EnemiesRemaining}", cx + 6, cy + 6, SKTextAlign.Left, _hudFontMedium, _textPaint);
            _textPaint.MaskFilter = null;
            cy += 20;
        }

        // Separator
        canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
        cy += 16;

        // ── BOMB/FIRE info ──
        if (player != null)
        {
            _textPaint.Color = _palette.HudText.WithAlpha(150);
            canvas.DrawText(HudLabelBombs, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
            cy += 20;

            _textPaint.Color = _palette.HudText;
            _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
            if (player.MaxBombs != _lastBombsValue)
            {
                _lastBombsValue = player.MaxBombs;
                _lastBombsString = $"{player.MaxBombs}";
            }
            canvas.DrawText(_lastBombsString, cx - 20, cy, SKTextAlign.Center, _hudFontMedium, _textPaint);

            _textPaint.Color = new SKColor(255, 120, 40);
            if (player.FireRange != _lastFireValue)
            {
                _lastFireValue = player.FireRange;
                _lastFireString = $"{player.FireRange}";
            }
            canvas.DrawText(_lastFireString, cx + 20, cy, SKTextAlign.Center, _hudFontMedium, _textPaint);
            _textPaint.MaskFilter = null;

            // Mini-Bomben-Icon unter der Zahl
            RenderMiniBomb(canvas, cx - 20, cy + 10, 5f, isNeon);
            // Mini-Flammen-Icon unter der Zahl
            RenderMiniFlame(canvas, cx + 20, cy + 10, 5f, isNeon);
            cy += 30;

            // ── Active power-ups (stacked vertically, gepoolte Liste) ──
            _activePowers.Clear();
            if (player.SpeedLevel > 0)
            {
                string spdLabel = player.SpeedLevel > 1 ? $"SPD x{player.SpeedLevel}" : "SPD";
                _activePowers.Add((spdLabel, new SKColor(60, 220, 80)));
            }
            if (player.HasWallpass) _activePowers.Add(("WLP", new SKColor(150, 100, 50)));
            if (player.HasDetonator) _activePowers.Add(("DET", new SKColor(240, 40, 40)));
            if (player.HasBombpass) _activePowers.Add(("BMP", new SKColor(50, 50, 150)));
            if (player.HasFlamepass) _activePowers.Add(("FLP", new SKColor(240, 190, 40)));
            if (player.HasShield) _activePowers.Add(("SHIELD", new SKColor(0, 229, 255)));
            if (player.HasKick) _activePowers.Add(("KICK", new SKColor(255, 165, 0)));
            if (player.HasLineBomb) _activePowers.Add(("LINE", new SKColor(0, 180, 255)));
            if (player.HasPowerBomb) _activePowers.Add(("PWR", new SKColor(255, 50, 50)));
            if (player.IsCursed)
            {
                string curseLabel = player.ActiveCurse switch
                {
                    CurseType.Diarrhea => "DIA",
                    CurseType.Slow => "SLOW",
                    CurseType.Constipation => "BLOCK",
                    CurseType.ReverseControls => "REV",
                    _ => "???"
                };
                _activePowers.Add(($"☠{curseLabel}:{(int)player.CurseTimer}", new SKColor(180, 0, 180)));
            }
            if (player.IsInvincible)
            {
                // INV-String nur bei Aenderung des Timer-Werts neu erstellen
                int invTimer = (int)player.InvincibilityTimer;
                if (invTimer != _lastInvTimerValue)
                {
                    _lastInvTimerValue = invTimer;
                    _lastInvString = $"INV:{invTimer}";
                }
                _activePowers.Add((_lastInvString, new SKColor(180, 80, 240)));
            }

            if (_activePowers.Count > 0)
            {
                cy += 6;
                canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
                cy += 14;

                _textPaint.Color = _palette.HudText.WithAlpha(150);
                canvas.DrawText(HudLabelPower, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
                cy += 18;

                foreach (var (label, color) in _activePowers)
                {
                    _textPaint.Color = color;
                    _textPaint.MaskFilter = isNeon ? _hudTextGlow : null;
                    canvas.DrawText(label, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
                    _textPaint.MaskFilter = null;
                    cy += 16;
                }
            }

            // ── CARD SLOTS (Deck) ──
            if (player.EquippedCards.Count > 0)
            {
                cy += 6;
                canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
                cy += 14;

                _textPaint.Color = _palette.HudText.WithAlpha(150);
                canvas.DrawText(HudLabelDeck, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
                cy += 16;

                RenderCardSlots(canvas, player, x, cy, w, isNeon);
                cy += CARD_SLOT_SIZE + 8;
            }

            // ── DUNGEON RAUM-TYP + MODIFIKATOR (nur im Dungeon-Modus) ──
            if (IsDungeonRun && DungeonRoomType != Models.Dungeon.DungeonRoomType.Normal)
            {
                cy += 6;
                var (rtLabel, rtColor) = GetRoomTypeInfo(DungeonRoomType);
                _fillPaint.Color = rtColor.WithAlpha(60);
                _fillPaint.MaskFilter = null;
                _fillPaint.Shader = null;
                canvas.DrawRoundRect(x + 4, cy, w - 8, 16, 4, 4, _fillPaint);
                _strokePaint.Color = rtColor;
                _strokePaint.StrokeWidth = 1f;
                _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
                canvas.DrawRoundRect(x + 4, cy, w - 8, 16, 4, 4, _strokePaint);
                _strokePaint.MaskFilter = null;
                _textPaint.Color = rtColor;
                canvas.DrawText(rtLabel, cx, cy + 12f, SKTextAlign.Center, _hudFontSmall, _textPaint);
                cy += 20;
            }

            if (IsDungeonRun && DungeonFloorModifier != Models.Dungeon.DungeonFloorModifier.None)
            {
                var (modLabel, modColor) = GetFloorModifierInfo(DungeonFloorModifier);
                _fillPaint.Color = modColor.WithAlpha(40);
                _fillPaint.MaskFilter = null;
                _fillPaint.Shader = null;
                canvas.DrawRoundRect(x + 4, cy, w - 8, 16, 4, 4, _fillPaint);
                _strokePaint.Color = modColor;
                _strokePaint.StrokeWidth = 1f;
                canvas.DrawRoundRect(x + 4, cy, w - 8, 16, 4, 4, _strokePaint);
                _textPaint.Color = modColor;
                canvas.DrawText(modLabel, cx, cy + 12f, SKTextAlign.Center, _hudFontSmall, _textPaint);
                cy += 20;
            }

            // ── DUNGEON BUFFS (nur im Dungeon-Modus) ──
            if (IsDungeonRun && DungeonActiveBuffs != null && DungeonActiveBuffs.Count > 0)
            {
                cy += 6;
                canvas.DrawLine(x + 10, cy, x + w - 10, cy, _strokePaint);
                cy += 14;

                _textPaint.Color = _palette.HudText.WithAlpha(150);
                canvas.DrawText(HudLabelBuffs, cx, cy, SKTextAlign.Center, _hudFontSmall, _textPaint);
                cy += 14;

                RenderDungeonBuffIcons(canvas, x, cy, w, isNeon);
            }
        }
    }

    private void RenderHeart(SKCanvas canvas, float cx, float cy, float r)
    {
        _fillPaint.Color = new SKColor(240, 50, 60);
        _fillPaint.MaskFilter = null;

        // Simple heart: two circles + triangle
        float d = r * 0.7f;
        canvas.DrawCircle(cx - d * 0.5f, cy - d * 0.2f, d * 0.55f, _fillPaint);
        canvas.DrawCircle(cx + d * 0.5f, cy - d * 0.2f, d * 0.55f, _fillPaint);

        _fusePath.Reset();
        _fusePath.MoveTo(cx - r, cy);
        _fusePath.LineTo(cx, cy + r);
        _fusePath.LineTo(cx + r, cy);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
    }

    private void RenderMiniBomb(SKCanvas canvas, float cx, float cy, float r, bool isNeon)
    {
        // Bomben-Koerper (Kreis)
        _fillPaint.Color = isNeon ? new SKColor(180, 180, 200) : new SKColor(50, 50, 60);
        _fillPaint.MaskFilter = null;
        canvas.DrawCircle(cx, cy, r, _fillPaint);

        // Glanz-Highlight
        _fillPaint.Color = isNeon ? new SKColor(220, 230, 255, 80) : new SKColor(120, 120, 140, 100);
        canvas.DrawCircle(cx - r * 0.25f, cy - r * 0.3f, r * 0.35f, _fillPaint);

        // Lunte (kurze Linie nach oben)
        _strokePaint.Color = isNeon ? new SKColor(255, 160, 60) : new SKColor(160, 120, 60);
        _strokePaint.StrokeWidth = 1.5f;
        _strokePaint.MaskFilter = isNeon ? _hudTextGlow : null;
        canvas.DrawLine(cx, cy - r, cx + r * 0.4f, cy - r * 1.6f, _strokePaint);

        // Funken-Punkt
        _fillPaint.Color = isNeon ? new SKColor(255, 200, 80) : new SKColor(255, 180, 40);
        canvas.DrawCircle(cx + r * 0.4f, cy - r * 1.6f, 1.5f, _fillPaint);
        _strokePaint.MaskFilter = null;
    }

    private void RenderMiniFlame(SKCanvas canvas, float cx, float cy, float r, bool isNeon)
    {
        // Aeussere Flamme (orange)
        _fillPaint.Color = isNeon ? new SKColor(255, 130, 40) : new SKColor(255, 120, 30);
        _fillPaint.MaskFilter = isNeon ? _hudTextGlow : null;

        _fusePath.Reset();
        _fusePath.MoveTo(cx - r * 0.7f, cy + r);
        _fusePath.QuadTo(cx - r, cy - r * 0.3f, cx, cy - r * 1.4f);
        _fusePath.QuadTo(cx + r, cy - r * 0.3f, cx + r * 0.7f, cy + r);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);

        // Innere Flamme (gelb)
        _fillPaint.Color = isNeon ? new SKColor(255, 220, 80) : new SKColor(255, 200, 60);
        _fillPaint.MaskFilter = null;

        _fusePath.Reset();
        _fusePath.MoveTo(cx - r * 0.35f, cy + r);
        _fusePath.QuadTo(cx - r * 0.5f, cy, cx, cy - r * 0.6f);
        _fusePath.QuadTo(cx + r * 0.5f, cy, cx + r * 0.35f, cy + r);
        _fusePath.Close();
        canvas.DrawPath(_fusePath, _fillPaint);
    }

    private void RenderMiniSkull(SKCanvas canvas, float cx, float cy, float r, bool isNeon)
    {
        // Schädel-Kopf (Oval)
        _fillPaint.Color = isNeon ? new SKColor(220, 200, 200) : new SKColor(200, 190, 180);
        _fillPaint.MaskFilter = null;
        canvas.DrawOval(cx, cy - r * 0.2f, r * 0.8f, r, _fillPaint);

        // Augen (zwei dunkle Punkte)
        _fillPaint.Color = isNeon ? new SKColor(255, 50, 50) : new SKColor(40, 10, 10);
        canvas.DrawCircle(cx - r * 0.3f, cy - r * 0.25f, r * 0.2f, _fillPaint);
        canvas.DrawCircle(cx + r * 0.3f, cy - r * 0.25f, r * 0.2f, _fillPaint);

        // Kiefer (schmaler Rechteck)
        _fillPaint.Color = isNeon ? new SKColor(190, 170, 170) : new SKColor(170, 160, 150);
        canvas.DrawRect(cx - r * 0.5f, cy + r * 0.5f, r, r * 0.4f, _fillPaint);

        // Zähne (kleine vertikale Linien)
        _strokePaint.Color = isNeon ? new SKColor(40, 20, 20) : new SKColor(80, 70, 60);
        _strokePaint.StrokeWidth = 1f;
        _strokePaint.MaskFilter = null;
        for (int i = -1; i <= 1; i++)
        {
            float tx = cx + i * r * 0.25f;
            canvas.DrawLine(tx, cy + r * 0.5f, tx, cy + r * 0.9f, _strokePaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CARD SLOTS (Deck-System)
    // ═══════════════════════════════════════════════════════════════════════

    // Gespeicherte Slot-Positionen für Touch-HitTest
    private const int MAX_CARD_SLOTS = 5;
    private const float CARD_SLOT_SIZE = 26f;
    private const float CARD_SLOT_GAP = 4f;
    private readonly SKRect[] _cardSlotRects = new SKRect[MAX_CARD_SLOTS];
    private int _cardSlotCount;

    /// <summary>
    /// Rendert die ausgerüsteten Karten-Slots im HUD.
    /// Horizontale Anordnung, aktiver Slot hervorgehoben, Raritäts-Rahmen.
    /// </summary>
    private void RenderCardSlots(SKCanvas canvas, Player player, float hudX, float startY, float hudWidth, bool isNeon)
    {
        var cards = player.EquippedCards;
        _cardSlotCount = Math.Min(cards.Count, MAX_CARD_SLOTS);
        if (_cardSlotCount == 0) return;

        float cx = hudX + hudWidth / 2f;
        float totalWidth = _cardSlotCount * (CARD_SLOT_SIZE + CARD_SLOT_GAP) - CARD_SLOT_GAP;
        float slotX = cx - totalWidth / 2f;

        for (int i = 0; i < _cardSlotCount; i++)
        {
            var card = cards[i];
            float sx = slotX + i * (CARD_SLOT_SIZE + CARD_SLOT_GAP);
            bool isActive = i == player.ActiveCardSlot;

            // Slot-Rect speichern für Touch-HitTest
            _cardSlotRects[i] = new SKRect(sx, startY, sx + CARD_SLOT_SIZE, startY + CARD_SLOT_SIZE);

            // Hintergrund
            _fillPaint.Color = isActive
                ? new SKColor(50, 50, 70, 220)
                : card.HasUsesLeft ? new SKColor(30, 30, 45, 200) : new SKColor(20, 20, 25, 180);
            _fillPaint.MaskFilter = null;
            _fillPaint.Shader = null;
            canvas.DrawRoundRect(sx, startY, CARD_SLOT_SIZE, CARD_SLOT_SIZE, 4, 4, _fillPaint);

            // Raritäts-Rahmen
            var rarityColor = GetCardRarityColor(card.Rarity);
            _strokePaint.Color = isActive ? rarityColor : rarityColor.WithAlpha(card.HasUsesLeft ? (byte)120 : (byte)50);
            _strokePaint.StrokeWidth = isActive ? 2f : 1f;
            _strokePaint.MaskFilter = isActive && isNeon ? _hudTextGlow : null;
            canvas.DrawRoundRect(sx, startY, CARD_SLOT_SIZE, CARD_SLOT_SIZE, 4, 4, _strokePaint);
            _strokePaint.MaskFilter = null;

            // Bomben-Typ-Indikator (farbiger Punkt)
            float iconCx = sx + CARD_SLOT_SIZE / 2f;
            float iconCy = startY + CARD_SLOT_SIZE / 2f - 3;
            var bombColor = GetBombTypeColor(card.BombType);

            if (card.HasUsesLeft)
            {
                _fillPaint.Color = bombColor;
                if (isActive && isNeon)
                    _fillPaint.MaskFilter = _smallGlow;
                canvas.DrawCircle(iconCx, iconCy, 6, _fillPaint);
                _fillPaint.MaskFilter = null;
            }
            else
            {
                // Ohne Uses: Ausgegrauter Punkt
                _fillPaint.Color = bombColor.WithAlpha(60);
                canvas.DrawCircle(iconCx, iconCy, 5, _fillPaint);
            }

            // Verbleibende Uses
            _textPaint.Color = card.HasUsesLeft
                ? (isActive ? SKColors.White : new SKColor(200, 200, 200))
                : new SKColor(80, 80, 80);
            canvas.DrawText(card.RemainingUses.ToString(), iconCx,
                startY + CARD_SLOT_SIZE - 2, SKTextAlign.Center, _hudFontSmall, _textPaint);

            // Aktiver Slot: Pulsierender Glow-Rand
            if (isActive)
            {
                float pulse = MathF.Sin(_globalTimer * 6f) * 0.3f + 0.7f;
                _strokePaint.Color = rarityColor.WithAlpha((byte)(pulse * 180));
                _strokePaint.StrokeWidth = 1.5f;
                _strokePaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
                canvas.DrawRoundRect(sx - 1, startY - 1, CARD_SLOT_SIZE + 2, CARD_SLOT_SIZE + 2, 5, 5, _strokePaint);
                _strokePaint.MaskFilter = null;
            }
        }
    }

    /// <summary>
    /// Prüft ob ein Touch-Punkt auf einen Karten-Slot trifft.
    /// Gibt den Slot-Index zurück (0-3), oder -1 wenn kein Treffer.
    /// </summary>
    public int HitTestCardSlot(float touchX, float touchY)
    {
        for (int i = 0; i < _cardSlotCount; i++)
        {
            var rect = _cardSlotRects[i];
            // Etwas größere Hitzone für Touch-Freundlichkeit (+6px)
            if (touchX >= rect.Left - 6 && touchX <= rect.Right + 6 &&
                touchY >= rect.Top - 6 && touchY <= rect.Bottom + 6)
            {
                return i;
            }
        }
        return -1;
    }

    private static SKColor GetBombTypeColor(BombType type) => type switch
    {
        BombType.Ice => new SKColor(100, 200, 255),
        BombType.Fire => new SKColor(255, 80, 30),
        BombType.Sticky => new SKColor(80, 200, 80),
        BombType.Smoke => new SKColor(160, 160, 180),
        BombType.Lightning => new SKColor(255, 240, 60),
        BombType.Gravity => new SKColor(120, 80, 200),
        BombType.Poison => new SKColor(100, 200, 50),
        BombType.TimeWarp => new SKColor(80, 180, 230),
        BombType.Mirror => new SKColor(200, 130, 255),
        BombType.Vortex => new SKColor(180, 60, 200),
        BombType.Phantom => new SKColor(100, 100, 160),
        BombType.Nova => new SKColor(255, 200, 60),
        BombType.BlackHole => new SKColor(80, 0, 120),
        _ => new SKColor(200, 200, 200)
    };

    private static SKColor GetCardRarityColor(Rarity rarity) => rarity switch
    {
        Rarity.Common => SKColors.White,
        Rarity.Rare => new SKColor(33, 150, 243),    // #2196F3
        Rarity.Epic => new SKColor(156, 39, 176),     // #9C27B0
        Rarity.Legendary => new SKColor(255, 215, 0), // #FFD700
        _ => SKColors.White
    };

    // ═══════════════════════════════════════════════════════════════════════
    // DUNGEON BUFF ICONS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rendert aktive Dungeon-Buffs als farbige Mini-Quadrate mit Buchstaben-Kürzel.
    /// Horizontale Anordnung, 2 pro Zeile falls nötig.
    /// </summary>
    private void RenderDungeonBuffIcons(SKCanvas canvas, float hudX, float startY, float hudWidth, bool isNeon)
    {
        if (DungeonActiveBuffs == null || DungeonActiveBuffs.Count == 0) return;

        const float buffSize = 18f;
        const float buffGap = 3f;
        int maxPerRow = 4;
        float cx = hudX + hudWidth / 2f;

        for (int i = 0; i < DungeonActiveBuffs.Count; i++)
        {
            int row = i / maxPerRow;
            int col = i % maxPerRow;
            int countInRow = Math.Min(DungeonActiveBuffs.Count - row * maxPerRow, maxPerRow);
            float totalRowWidth = countInRow * (buffSize + buffGap) - buffGap;
            float rowStartX = cx - totalRowWidth / 2f;

            float bx = rowStartX + col * (buffSize + buffGap);
            float by = startY + row * (buffSize + buffGap);

            var (label, color) = GetDungeonBuffInfo(DungeonActiveBuffs[i]);

            // Farbiges Quadrat
            _fillPaint.Color = color.WithAlpha(180);
            _fillPaint.MaskFilter = null;
            _fillPaint.Shader = null;
            canvas.DrawRoundRect(bx, by, buffSize, buffSize, 3, 3, _fillPaint);

            // Rahmen
            _strokePaint.Color = color;
            _strokePaint.StrokeWidth = 1f;
            _strokePaint.MaskFilter = isNeon ? _smallGlow : null;
            canvas.DrawRoundRect(bx, by, buffSize, buffSize, 3, 3, _strokePaint);
            _strokePaint.MaskFilter = null;

            // Buchstaben-Kürzel
            _textPaint.Color = SKColors.White;
            _textPaint.MaskFilter = null;
            canvas.DrawText(label, bx + buffSize / 2f, by + buffSize / 2f + 4f,
                SKTextAlign.Center, _hudFontSmall, _textPaint);
        }
    }

    /// <summary>
    /// Gibt Label und Farbe für einen Dungeon-Raum-Typ zurück.
    /// </summary>
    private static (string label, SKColor color) GetRoomTypeInfo(Models.Dungeon.DungeonRoomType type) => type switch
    {
        Models.Dungeon.DungeonRoomType.Elite => ("ELITE", new SKColor(244, 67, 54)),         // Rot
        Models.Dungeon.DungeonRoomType.Treasure => ("TREASURE", new SKColor(255, 215, 0)),   // Gold
        Models.Dungeon.DungeonRoomType.Challenge => ("CHALLENGE", new SKColor(255, 152, 0)), // Orange
        Models.Dungeon.DungeonRoomType.Rest => ("REST", new SKColor(76, 175, 80)),           // Grün
        _ => ("", SKColors.White)
    };

    /// <summary>
    /// Gibt Label und Farbe für einen Floor-Modifikator zurück.
    /// </summary>
    private static (string label, SKColor color) GetFloorModifierInfo(Models.Dungeon.DungeonFloorModifier mod) => mod switch
    {
        Models.Dungeon.DungeonFloorModifier.LavaBorders => ("LAVA", new SKColor(255, 87, 34)),     // Orange-Rot
        Models.Dungeon.DungeonFloorModifier.Darkness => ("DARK", new SKColor(120, 120, 160)),      // Grau-Blau
        Models.Dungeon.DungeonFloorModifier.DoubleSpawns => ("x2 EN", new SKColor(244, 67, 54)),   // Rot
        Models.Dungeon.DungeonFloorModifier.FastBombs => ("FAST", new SKColor(255, 193, 7)),       // Gelb
        Models.Dungeon.DungeonFloorModifier.BigExplosions => ("+FIRE", new SKColor(255, 152, 0)),   // Orange
        Models.Dungeon.DungeonFloorModifier.Regeneration => ("REGEN", new SKColor(76, 175, 80)),    // Grün
        Models.Dungeon.DungeonFloorModifier.Wealthy => ("x3 $", new SKColor(255, 215, 0)),          // Gold
        _ => ("", SKColors.White)
    };

    /// <summary>
    /// Gibt Kürzel und Farbe für einen Dungeon-Buff zurück.
    /// </summary>
    private static (string label, SKColor color) GetDungeonBuffInfo(DungeonBuffType type) => type switch
    {
        DungeonBuffType.ExtraBomb => ("B", new SKColor(100, 150, 255)),      // Blau (Bombe)
        DungeonBuffType.ExtraFire => ("F", new SKColor(255, 120, 40)),       // Orange (Feuer)
        DungeonBuffType.SpeedBoost => ("S", new SKColor(80, 220, 100)),      // Grün (Speed)
        DungeonBuffType.Shield => ("SH", new SKColor(0, 229, 255)),          // Cyan (Schild)
        DungeonBuffType.CoinBonus => ("$", new SKColor(255, 215, 0)),        // Gold (Münzen)
        DungeonBuffType.ReloadSpecialBombs => ("R", new SKColor(200, 100, 255)), // Violett (Reload)
        DungeonBuffType.EnemySlow => ("ES", new SKColor(100, 200, 200)),     // Teal (Enemy Slow)
        DungeonBuffType.ExtraLife => ("L", new SKColor(240, 50, 60)),        // Rot (Leben)
        DungeonBuffType.FireImmunity => ("FI", new SKColor(255, 80, 30)),    // Orange-Rot (Feuerimmunität)
        DungeonBuffType.BlastRadius => ("BR", new SKColor(255, 200, 60)),    // Gelb (Blast)
        DungeonBuffType.BombTimer => ("T", new SKColor(180, 180, 200)),      // Grau (Timer)
        DungeonBuffType.PowerUpMagnet => ("M", new SKColor(200, 50, 200)),   // Magenta (Magnet)
        // Legendäre Buffs (goldener Rahmen)
        DungeonBuffType.Berserker => ("BK", new SKColor(255, 50, 50)),       // Rot (Berserker)
        DungeonBuffType.TimeFreeze => ("TF", new SKColor(100, 200, 255)),    // Hellblau (TimeFreeze)
        DungeonBuffType.GoldRush => ("GR", new SKColor(255, 215, 0)),        // Gold (GoldRush)
        DungeonBuffType.Phantom => ("PH", new SKColor(160, 32, 240)),        // Violett (Phantom)
        _ => ("?", SKColors.White)
    };
}
