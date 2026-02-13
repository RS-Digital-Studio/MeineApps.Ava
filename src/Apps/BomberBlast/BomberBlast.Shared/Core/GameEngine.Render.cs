using BomberBlast.Models;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Rendering: State-Overlays (Starting, Paused, LevelComplete, GameOver, Victory)
/// </summary>
public partial class GameEngine
{
    /// <summary>
    /// Spiel rendern
    /// </summary>
    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Nicht rendern wenn nicht initialisiert
        if (_state == GameState.Menu)
        {
            canvas.Clear(new SKColor(20, 20, 30));
            return;
        }

        // Viewport aktualisieren
        _renderer.CalculateViewport(screenWidth, screenHeight, _grid.PixelWidth, _grid.PixelHeight);

        // Screen-Shake: Canvas verschieben vor dem Spiel-Rendering
        if (_screenShake.IsActive)
        {
            canvas.Save();
            canvas.Translate(_screenShake.OffsetX, _screenShake.OffsetY);
        }

        // Spiel rendern (gecachte Exit-Zelle übergeben für Performance)
        _renderer.Render(canvas, _grid, _player,
            _enemies, _bombs, _explosions, _powerUps,
            _timer.RemainingTime, _player.Score, _player.Lives, _exitCell);

        // Partikel rendern (über dem Spielfeld, unter den Controls)
        if (_particleSystem.HasActiveParticles)
        {
            _particleSystem.Render(canvas, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);
        }

        // Floating Text rendern (Score-Popups, Combos, PowerUp-Texte)
        _floatingText.Render(canvas, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);

        // Screen-Shake Canvas wiederherstellen
        if (_screenShake.IsActive)
        {
            canvas.Restore();
        }

        // Input-Controls rendern (NICHT vom Shake beeinflusst)
        _inputManager.Render(canvas, screenWidth, screenHeight);

        // Pause-Button rendern (nur Android, nur im Playing-State)
        if (_state == GameState.Playing && OperatingSystem.IsAndroid())
        {
            RenderPauseButton(canvas);
        }

        // Timer-Warnung rendern (pulsierender roter Rand unter 30s)
        if (_state == GameState.Playing && _timer.IsWarning)
        {
            RenderTimerWarning(canvas, screenWidth, screenHeight);
        }

        // State-Overlays rendern
        RenderStateOverlay(canvas, screenWidth, screenHeight);

        // Welt-/Wave-Ankündigung rendern (über State-Overlay, unter Tutorial)
        if (_worldAnnouncementTimer > 0)
        {
            RenderWorldAnnouncement(canvas, screenWidth, screenHeight);
        }

        // Tutorial-Overlay rendern (über allem)
        if (_tutorialService.IsActive && _state == GameState.Playing && _tutorialService.CurrentStep != null)
        {
            _tutorialOverlay.Render(canvas, screenWidth, screenHeight,
                _tutorialService.CurrentStep, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);
        }
    }

    private void RenderStateOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        switch (_state)
        {
            case GameState.Starting:
                RenderStartingOverlay(canvas, screenWidth, screenHeight);
                break;

            case GameState.Paused:
                RenderPausedOverlay(canvas, screenWidth, screenHeight);
                break;

            case GameState.LevelComplete:
                RenderLevelCompleteOverlay(canvas, screenWidth, screenHeight);
                break;

            case GameState.GameOver:
                RenderGameOverOverlay(canvas, screenWidth, screenHeight);
                break;

            case GameState.Victory:
                RenderVictoryOverlay(canvas, screenWidth, screenHeight);
                break;
        }
    }

    private void RenderStartingOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        float progress = _stateTimer / START_DELAY; // 0→1

        // Iris-Wipe: Schwarzer Kreis öffnet sich vom Zentrum
        float maxRadius = MathF.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight) / 2f;
        float irisRadius = progress * maxRadius;

        // Schwarze Maske mit kreisförmigem Ausschnitt (Iris-Wipe)
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddRect(new SKRect(0, 0, screenWidth, screenHeight));
        clipPath.AddCircle(screenWidth / 2f, screenHeight / 2f, irisRadius, SKPathDirection.CounterClockwise);
        canvas.ClipPath(clipPath);
        _overlayBgPaint.Color = new SKColor(0, 0, 0, 255);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);
        canvas.Restore();

        // Iris-Rand-Glow (goldener Ring am Rand des Iris-Kreises)
        if (irisRadius > 10)
        {
            _overlayTextPaint.Style = SKPaintStyle.Stroke;
            _overlayTextPaint.StrokeWidth = 3f;
            _overlayTextPaint.Color = new SKColor(255, 200, 50, (byte)(200 * (1f - progress)));
            _overlayTextPaint.MaskFilter = _overlayGlowFilter;
            canvas.DrawCircle(screenWidth / 2f, screenHeight / 2f, irisRadius, _overlayTextPaint);
            _overlayTextPaint.Style = SKPaintStyle.StrokeAndFill;
            _overlayTextPaint.MaskFilter = null;
        }

        // Text-Overlay (halbtransparent, wird mit dem Iris-Wipe sichtbarer)
        byte textBgAlpha = (byte)(180 * (1f - progress * 0.5f));
        _overlayBgPaint.Color = new SKColor(0, 0, 0, textBgAlpha);
        canvas.DrawRect(screenWidth / 2 - 200, screenHeight / 2 - 60, 400, 160, _overlayBgPaint);

        _overlayFont.Size = 48;
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = _overlayGlowFilter;

        string text = _isArcadeMode
            ? string.Format(_localizationService.GetString("WaveOverlay"), _arcadeWave)
            : string.Format(_localizationService.GetString("StageOverlay"), _currentLevelNumber);

        canvas.DrawText(text, screenWidth / 2, screenHeight / 2, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        // Countdown
        int countdown = (int)(START_DELAY - _stateTimer) + 1;
        _overlayFont.Size = 72;
        _overlayTextPaint.Color = SKColors.Yellow;
        canvas.DrawText(countdown.ToString(), screenWidth / 2, screenHeight / 2 + 80, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
    }

    private void RenderPausedOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        _overlayBgPaint.Color = new SKColor(0, 0, 0, 200);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

        _overlayFont.Size = 48;
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = _overlayGlowFilter;

        canvas.DrawText(_localizationService.GetString("Paused"), screenWidth / 2, screenHeight / 2, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayFont.Size = 24;
        _overlayTextPaint.MaskFilter = null;
        canvas.DrawText(_localizationService.GetString("TapToResume"), screenWidth / 2, screenHeight / 2 + 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
    }

    private void RenderLevelCompleteOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        float progress = Math.Clamp(_stateTimer / LEVEL_COMPLETE_DELAY, 0f, 1f);

        // Iris-Close in letzter Sekunde (Kreis schließt sich)
        float irisCloseStart = 1f - (1f / LEVEL_COMPLETE_DELAY); // ~0.67
        if (progress > irisCloseStart)
        {
            float closeProgress = (progress - irisCloseStart) / (1f - irisCloseStart); // 0→1
            float maxRadius = MathF.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight) / 2f;
            float irisRadius = (1f - closeProgress) * maxRadius;

            canvas.Save();
            using var clipPath = new SKPath();
            clipPath.AddRect(new SKRect(0, 0, screenWidth, screenHeight));
            clipPath.AddCircle(screenWidth / 2f, screenHeight / 2f, Math.Max(irisRadius, 1), SKPathDirection.CounterClockwise);
            canvas.ClipPath(clipPath);
            _overlayBgPaint.Color = new SKColor(0, 0, 0, 255);
            canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);
            canvas.Restore();
        }

        _overlayBgPaint.Color = new SKColor(0, 50, 0, 200);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

        _overlayFont.Size = 48;
        _overlayTextPaint.Color = SKColors.Green;
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;

        canvas.DrawText(_localizationService.GetString("LevelComplete"), screenWidth / 2, screenHeight / 2 - 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayTextPaint.Color = SKColors.Yellow;
        _overlayTextPaint.MaskFilter = null;
        _overlayFont.Size = 32;
        canvas.DrawText(string.Format(_localizationService.GetString("ScoreFormat"), _player.Score), screenWidth / 2, screenHeight / 2 + 20, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayTextPaint.Color = SKColors.Cyan;
        _overlayFont.Size = 24;
        // Gecachten TimeBonus verwenden (berechnet in CompleteLevel, nicht neu berechnen)
        canvas.DrawText(string.Format(_localizationService.GetString("TimeBonusFormat"), LastTimeBonus), screenWidth / 2, screenHeight / 2 + 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        // Sterne-Anzeige (nur Story-Modus, mit Bounce-Animation)
        if (_levelCompleteStars > 0)
        {
            float starY = screenHeight / 2 + 100;
            float starSize = 20f;
            float starSpacing = 50f;
            float startX = screenWidth / 2 - starSpacing;

            for (int i = 0; i < 3; i++)
            {
                float sx = startX + i * starSpacing;

                // Gestaffelte Animation: Stern i erscheint nach i*0.3s
                float starDelay = i * 0.3f;
                float starProgress = Math.Clamp((_stateTimer - 0.5f - starDelay) / 0.3f, 0f, 1f);

                if (starProgress <= 0) continue;

                bool earned = i < _levelCompleteStars;

                // Scale-Bounce: Overshoots auf 1.3, dann zurück auf 1.0
                float bounceScale = starProgress < 0.6f
                    ? starProgress / 0.6f * 1.3f
                    : 1.3f - (starProgress - 0.6f) / 0.4f * 0.3f;

                float s = starSize * bounceScale;

                // Stern zeichnen (5-zackiger Stern via SKPath)
                using var starPath = new SKPath();
                for (int p = 0; p < 10; p++)
                {
                    float angle = MathF.PI / 2f + p * MathF.PI / 5f;
                    float r = p % 2 == 0 ? s : s * 0.4f;
                    float px = sx + MathF.Cos(angle) * r;
                    float py = starY - MathF.Sin(angle) * r;
                    if (p == 0) starPath.MoveTo(px, py);
                    else starPath.LineTo(px, py);
                }
                starPath.Close();

                _overlayTextPaint.Style = SKPaintStyle.Fill;
                _overlayTextPaint.MaskFilter = earned ? _overlayGlowFilter : null;
                _overlayTextPaint.Color = earned
                    ? new SKColor(255, 215, 0, (byte)(255 * starProgress))  // Gold
                    : new SKColor(80, 80, 80, (byte)(150 * starProgress));  // Grau (nicht verdient)

                canvas.DrawPath(starPath, _overlayTextPaint);

                // Umrandung
                _overlayTextPaint.Style = SKPaintStyle.Stroke;
                _overlayTextPaint.StrokeWidth = 1.5f;
                _overlayTextPaint.Color = earned
                    ? new SKColor(200, 160, 0, (byte)(255 * starProgress))
                    : new SKColor(60, 60, 60, (byte)(150 * starProgress));
                canvas.DrawPath(starPath, _overlayTextPaint);
            }
            _overlayTextPaint.Style = SKPaintStyle.StrokeAndFill;
            _overlayTextPaint.MaskFilter = null;
        }
    }

    private void RenderGameOverOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        _overlayBgPaint.Color = new SKColor(50, 0, 0, 220);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

        _overlayFont.Size = 64;
        _overlayTextPaint.Color = SKColors.Red;
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;

        canvas.DrawText(_localizationService.GetString("GameOver"), screenWidth / 2, screenHeight / 2 - 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = null;
        _overlayFont.Size = 32;
        canvas.DrawText(string.Format(_localizationService.GetString("FinalScore"), _player.Score), screenWidth / 2, screenHeight / 2 + 20, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayFont.Size = 24;
        if (_isArcadeMode)
        {
            canvas.DrawText(string.Format(_localizationService.GetString("WaveReached"), _arcadeWave), screenWidth / 2, screenHeight / 2 + 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
        }
        else
        {
            canvas.DrawText(string.Format(_localizationService.GetString("LevelFormat"), _currentLevelNumber), screenWidth / 2, screenHeight / 2 + 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
        }
    }

    private void RenderVictoryOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Goldenes Overlay
        _overlayBgPaint.Color = new SKColor(50, 40, 0, 220);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

        _overlayFont.Size = 56;
        _overlayTextPaint.Color = new SKColor(255, 215, 0); // Gold
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;

        string victoryText = _localizationService.GetString("VictoryTitle");
        if (string.IsNullOrEmpty(victoryText)) victoryText = "VICTORY!";
        canvas.DrawText(victoryText, screenWidth / 2, screenHeight / 2 - 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayFont.Size = 28;
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = null;

        string allComplete = _localizationService.GetString("AllLevelsComplete");
        if (string.IsNullOrEmpty(allComplete)) allComplete = "All 50 levels complete!";
        canvas.DrawText(allComplete, screenWidth / 2, screenHeight / 2, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayTextPaint.Color = new SKColor(255, 215, 0);
        _overlayFont.Size = 32;
        canvas.DrawText(string.Format(_localizationService.GetString("FinalScore"), _player.Score),
            screenWidth / 2, screenHeight / 2 + 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
    }

    private void RenderTimerWarning(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Dringlichkeit steigt je weniger Zeit (0 bei 30s → 1 bei 0s)
        float urgency = 1f - (_timer.RemainingTime / 30f);
        // Pulsierender Effekt (schneller bei weniger Zeit)
        float pulseSpeed = 3f + urgency * 5f;
        float pulse = MathF.Sin(_timer.RemainingTime * pulseSpeed) * 0.5f + 0.5f;
        byte alpha = (byte)(120 * urgency * pulse);

        if (alpha < 5) return;

        _overlayBgPaint.Color = new SKColor(255, 0, 0, alpha);
        float borderWidth = 3 + urgency * 5; // 3-8 Pixel

        // Vier Ränder
        canvas.DrawRect(0, 0, screenWidth, borderWidth, _overlayBgPaint);
        canvas.DrawRect(0, screenHeight - borderWidth, screenWidth, borderWidth, _overlayBgPaint);
        canvas.DrawRect(0, 0, borderWidth, screenHeight, _overlayBgPaint);
        canvas.DrawRect(screenWidth - borderWidth, 0, borderWidth, screenHeight, _overlayBgPaint);
    }

    private void RenderPauseButton(SKCanvas canvas)
    {
        float x = PAUSE_BUTTON_MARGIN;
        float y = PAUSE_BUTTON_MARGIN + BannerTopOffset;
        float size = PAUSE_BUTTON_SIZE;

        // Halbtransparenter Hintergrund-Kreis
        _overlayBgPaint.Color = new SKColor(0, 0, 0, 120);
        canvas.DrawCircle(x + size / 2, y + size / 2, size / 2, _overlayBgPaint);

        // Zwei vertikale Pause-Balken
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = null;
        _overlayTextPaint.Style = SKPaintStyle.Fill;

        float barW = size * 0.15f;
        float barH = size * 0.4f;
        float cx = x + size / 2;
        float cy = y + size / 2;
        float gap = size * 0.1f;

        canvas.DrawRect(cx - gap - barW, cy - barH / 2, barW, barH, _overlayTextPaint);
        canvas.DrawRect(cx + gap, cy - barH / 2, barW, barH, _overlayTextPaint);

        // Style zurücksetzen
        _overlayTextPaint.Style = SKPaintStyle.StrokeAndFill;
    }

    private void RenderWorldAnnouncement(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Fade: 0→1 in den ersten 0.3s, halten bis 1.7s, dann 1→0 in 0.3s
        float alpha;
        if (_worldAnnouncementTimer > 1.7f)
            alpha = (2.0f - _worldAnnouncementTimer) / 0.3f; // Fade-In
        else if (_worldAnnouncementTimer < 0.3f)
            alpha = _worldAnnouncementTimer / 0.3f; // Fade-Out
        else
            alpha = 1f;

        alpha = Math.Clamp(alpha, 0f, 1f);
        if (alpha < 0.01f) return;

        byte a = (byte)(255 * alpha);

        // Hintergrund-Band (halbtransparent)
        _overlayBgPaint.Color = new SKColor(0, 0, 0, (byte)(160 * alpha));
        float bandHeight = 80;
        float bandY = screenHeight * 0.25f - bandHeight / 2;
        canvas.DrawRect(0, bandY, screenWidth, bandHeight, _overlayBgPaint);

        // Grosser Text mit Glow
        _overlayFont.Size = 48;
        _overlayTextPaint.Color = new SKColor(255, 215, 0, a); // Gold
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;
        _overlayTextPaint.Style = SKPaintStyle.Fill;

        // Leichter Scale-Bounce
        float scale = _worldAnnouncementTimer > 1.7f
            ? 0.8f + 0.2f * ((2.0f - _worldAnnouncementTimer) / 0.3f)
            : 1.0f;

        canvas.Save();
        canvas.Translate(screenWidth / 2, bandY + bandHeight / 2 + 12);
        canvas.Scale(scale);
        canvas.DrawText(_worldAnnouncementText, 0, 0, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
        canvas.Restore();

        _overlayTextPaint.MaskFilter = null;
        _overlayTextPaint.Style = SKPaintStyle.StrokeAndFill;
    }
}
