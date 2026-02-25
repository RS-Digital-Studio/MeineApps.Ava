using BomberBlast.Models;
using BomberBlast.Models.Dungeon;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Rendering: State-Overlays (Starting, Paused, LevelComplete, GameOver, Victory)
/// </summary>
public partial class GameEngine
{
    /// <summary>
    /// Welt-spezifische Iris-Wipe-Farbe (statt immer Schwarz).
    /// </summary>
    private SKColor GetIrisWipeColor()
    {
        int wi = Math.Clamp((_currentLevelNumber - 1) / 10, 0, 9);
        return wi switch
        {
            0 => new SKColor(10, 30, 10),    // Forest: Dunkles Grün
            1 => new SKColor(15, 20, 25),    // Industrial: Dunkles Stahlblau
            2 => new SKColor(20, 10, 35),    // Cavern: Dunkles Violett
            3 => new SKColor(10, 20, 40),    // Sky: Dunkles Blau
            4 => new SKColor(40, 10, 5),     // Inferno: Dunkles Rot
            5 => new SKColor(30, 22, 15),    // Ruins: Dunkles Braun
            6 => new SKColor(5, 20, 35),     // Ocean: Dunkles Teal
            7 => new SKColor(35, 15, 5),     // Volcano: Dunkles Orange
            8 => new SKColor(35, 30, 10),    // SkyFortress: Dunkles Gold
            9 => new SKColor(15, 5, 25),     // ShadowRealm: Dunkles Lila
            _ => new SKColor(0, 0, 0)
        };
    }

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

        // Combo-Daten + Survival-Daten + Dungeon-Daten an Renderer übergeben
        _renderer.ComboCount = _comboCount;
        _renderer.ComboTimer = _comboTimer;
        _renderer.IsSurvivalMode = _isSurvivalMode;
        _renderer.SurvivalKills = _enemiesKilled;
        _renderer.EnemiesRemaining = EnemiesRemaining;
        _renderer.IsDungeonRun = _isDungeonRun;
        _renderer.DungeonActiveBuffs = _isDungeonRun ? _dungeonService.RunState?.ActiveBuffs : null;
        _renderer.DungeonRoomType = _isDungeonRun ? _dungeonService.RunState?.CurrentRoomType ?? DungeonRoomType.Normal : DungeonRoomType.Normal;
        _renderer.DungeonFloorModifier = _isDungeonRun ? _dungeonFloorModifier : DungeonFloorModifier.None;

        // Lokalisierte HUD-Labels übergeben
        _renderer.HudLabelKills = _localizationService.GetString("HudKills") ?? "KILLS";
        _renderer.HudLabelTime = _localizationService.GetString("HudTime") ?? "TIME";
        _renderer.HudLabelScore = _localizationService.GetString("HudScore") ?? "SCORE";
        _renderer.HudLabelLives = _localizationService.GetString("HudLives") ?? "LIVES";
        _renderer.HudLabelBombs = _localizationService.GetString("HudBombs") ?? "BOMBS";
        _renderer.HudLabelPower = _localizationService.GetString("HudPower") ?? "POWER";
        _renderer.HudLabelDeck = _localizationService.GetString("HudDeck") ?? "DECK";
        _renderer.HudLabelBuffs = _localizationService.GetString("HudBuffs") ?? "BUFFS";

        // Survival-Modus: Verstrichene Zeit anzeigen statt Countdown
        float displayTime = _isSurvivalMode ? _survivalTimeElapsed : _timer.RemainingTime;

        // Spiel rendern (gecachte Exit-Zelle übergeben für Performance)
        _renderer.Render(canvas, _grid, _player,
            _enemies, _bombs, _explosions, _powerUps,
            displayTime, _player.Score, _player.Lives, _exitCell);

        // Partikel rendern (über dem Spielfeld, unter den Controls)
        if (_particleSystem.HasActiveParticles)
        {
            _particleSystem.Render(canvas, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);
        }

        // Floating Text rendern (Score-Popups, Combos, PowerUp-Texte)
        _floatingText.Render(canvas, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);

        // Pontan-Spawn-Warnung rendern (pulsierendes "!" an vorberechneter Position)
        if (_pontanWarningActive && _state == GameState.Playing)
        {
            RenderPontanWarning(canvas);
        }

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

        // Discovery-Overlay rendern (über State-Overlay, unter Tutorial)
        if (_discoveryOverlay.IsActive)
        {
            _discoveryOverlay.Render(canvas, screenWidth, screenHeight);
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
        var irisColor = GetIrisWipeColor();
        float progress = _stateTimer / START_DELAY; // 0→1

        // Iris-Wipe: Kreis öffnet sich vom Zentrum (welt-spezifische Farbe)
        float maxRadius = MathF.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight) / 2f;
        float irisRadius = progress * maxRadius;

        // Welt-farbige Maske mit kreisförmigem Ausschnitt (Iris-Wipe)
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddRect(new SKRect(0, 0, screenWidth, screenHeight));
        clipPath.AddCircle(screenWidth / 2f, screenHeight / 2f, irisRadius, SKPathDirection.CounterClockwise);
        canvas.ClipPath(clipPath);
        _overlayBgPaint.Color = new SKColor(irisColor.Red, irisColor.Green, irisColor.Blue, 255);
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
        _overlayBgPaint.Color = new SKColor(irisColor.Red, irisColor.Green, irisColor.Blue, textBgAlpha);
        canvas.DrawRect(screenWidth / 2 - 200, screenHeight / 2 - 60, 400, 160, _overlayBgPaint);

        _overlayFont.Size = 48;
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = _overlayGlowFilter;

        string text = string.Format(_localizationService.GetString("StageOverlay"), _currentLevelNumber);

        canvas.DrawText(text, screenWidth / 2, screenHeight / 2, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        // Countdown
        int countdown = (int)MathF.Ceiling(START_DELAY - _stateTimer);
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
            var irisColor = GetIrisWipeColor();
            _overlayBgPaint.Color = new SKColor(irisColor.Red, irisColor.Green, irisColor.Blue, 255);
            canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);
            canvas.Restore();
        }

        _overlayBgPaint.Color = new SKColor(0, 50, 0, 200);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

        // Celebration-Confetti (fallende bunte Rechtecke)
        RenderCelebrationParticles(canvas, screenWidth, screenHeight, _stateTimer, 25, false);

        _overlayFont.Size = 48;
        _overlayTextPaint.Color = SKColors.Green;
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;

        canvas.DrawText(_localizationService.GetString("LevelComplete"), screenWidth / 2, screenHeight / 2 - 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        // Erster Sieg: Extra goldener Text über "Level Complete"
        if (_isFirstVictory)
        {
            _overlayFont.Size = 36;
            _overlayTextPaint.Color = new SKColor(255, 215, 0); // Gold
            float victoryPulse = 1f + MathF.Sin(_stateTimer * 6f) * 0.1f;
            canvas.Save();
            canvas.Translate(screenWidth / 2, screenHeight / 2 - 100);
            canvas.Scale(victoryPulse);
            string firstVictoryText = _localizationService.GetString("FirstVictory") ?? "FIRST VICTORY!";
            canvas.DrawText(firstVictoryText, 0, 0, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
            canvas.Restore();
        }

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
        var irisColor = GetIrisWipeColor();
        // Rot-Tönung beibehalten, aber leicht welt-gefärbt mischen
        byte goR = (byte)Math.Min(255, 50 + irisColor.Red / 3);
        byte goG = (byte)(irisColor.Green / 5);
        byte goB = (byte)(irisColor.Blue / 5);
        _overlayBgPaint.Color = new SKColor(goR, goG, goB, 220);
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
        canvas.DrawText(string.Format(_localizationService.GetString("LevelFormat"), _currentLevelNumber), screenWidth / 2, screenHeight / 2 + 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
    }

    private void RenderVictoryOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // Goldenes Overlay, leicht welt-gefärbt
        var irisColor = GetIrisWipeColor();
        byte vR = (byte)Math.Min(255, 50 + irisColor.Red / 2);
        byte vG = (byte)Math.Min(255, 40 + irisColor.Green / 3);
        byte vB = (byte)(irisColor.Blue / 4);
        _overlayBgPaint.Color = new SKColor(vR, vG, vB, 220);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

        // Feuerwerk-Bursts + goldene Glitzer-Partikel
        RenderFireworkBursts(canvas, screenWidth, screenHeight, _stateTimer);
        RenderCelebrationParticles(canvas, screenWidth, screenHeight, _stateTimer, 35, true);

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

    /// <summary>
    /// Deterministische Celebration-Partikel (Confetti + Funken).
    /// Basiert auf _stateTimer, keine Heap-Allokationen pro Frame.
    /// </summary>
    private void RenderCelebrationParticles(SKCanvas canvas, float sw, float sh,
        float timer, int count, bool isVictory)
    {
        // Confetti-Farben
        ReadOnlySpan<uint> colors = stackalloc uint[]
        {
            0xFFFFD700, // Gold
            0xFFFF4444, // Rot
            0xFF44FF44, // Grün
            0xFF4488FF, // Blau
            0xFFFF44FF, // Magenta
            0xFFFF8800, // Orange
            0xFF00FFCC, // Cyan
            0xFFFFFF44, // Gelb
        };

        for (int i = 0; i < count; i++)
        {
            // Deterministischer Pseudo-Random pro Partikel
            int seed = i * 7919 + 1013;
            float px = (seed % 1000) / 1000f; // 0-1 X-Position
            float py = ((seed * 3 + 571) % 1000) / 1000f; // 0-1 Start-Y
            float speed = 0.3f + ((seed * 7 + 233) % 600) / 1000f; // 0.3-0.9 Fallgeschwindigkeit
            float drift = ((seed * 13 + 97) % 1000) / 1000f - 0.5f; // -0.5 bis 0.5 Seitendrift
            float phase = ((seed * 17 + 443) % 1000) / 1000f * MathF.PI * 2f;
            int colorIdx = (seed * 11 + 67) % colors.Length;
            float size = 2f + ((seed * 23 + 311) % 400) / 100f; // 2-6

            // Position über Zeit berechnen
            float x = px * sw + MathF.Sin(timer * 2f + phase) * 20f * drift;
            float y = py * sh + timer * speed * 100f; // Fallen

            // Wrap: Unten raus → oben rein
            y = y % (sh + 20f);

            // Alpha: Erscheint nach gestaffeltem Delay
            float delay = i * 0.05f;
            float alpha = Math.Clamp((timer - delay) * 3f, 0f, 1f);
            if (alpha <= 0) continue;

            var color = new SKColor(colors[colorIdx]);

            if (isVictory)
            {
                // Victory: Goldener Glitzer-Effekt
                float sparkle = MathF.Abs(MathF.Sin(timer * 8f + phase));
                byte a = (byte)(alpha * (150 + sparkle * 105));
                _overlayTextPaint.Color = color.WithAlpha(a);
                _overlayTextPaint.Style = SKPaintStyle.Fill;
                _overlayTextPaint.MaskFilter = sparkle > 0.7f ? _overlayGlowFilter : null;
                canvas.DrawCircle(x, y, size * (0.8f + sparkle * 0.4f), _overlayTextPaint);
            }
            else
            {
                // LevelComplete: Confetti-Rechtecke mit Rotation
                float rotation = timer * (200f + i * 30f) + phase * 57.3f;
                byte a = (byte)(alpha * 200);
                _overlayTextPaint.Color = color.WithAlpha(a);
                _overlayTextPaint.Style = SKPaintStyle.Fill;
                _overlayTextPaint.MaskFilter = null;

                canvas.Save();
                canvas.Translate(x, y);
                canvas.RotateDegrees(rotation);
                canvas.DrawRect(-size, -size * 0.4f, size * 2f, size * 0.8f, _overlayTextPaint);
                canvas.Restore();
            }
        }

        _overlayTextPaint.Style = SKPaintStyle.StrokeAndFill;
        _overlayTextPaint.MaskFilter = null;
    }

    /// <summary>
    /// Feuerwerk-Burst (für Victory): Expandierende Kreise von Startposition.
    /// </summary>
    private void RenderFireworkBursts(SKCanvas canvas, float sw, float sh, float timer)
    {
        // 4 gestaffelte Feuerwerke an verschiedenen Positionen
        for (int f = 0; f < 4; f++)
        {
            float delay = f * 0.7f + 0.3f;
            float t = timer - delay;
            if (t < 0 || t > 1.5f) continue;

            float progress = t / 1.5f; // 0→1
            float cx = sw * (0.2f + f * 0.2f);
            float cy = sh * (0.25f + (f % 2) * 0.2f);

            // 12 Strahlen pro Feuerwerk
            for (int r = 0; r < 12; r++)
            {
                float angle = r * MathF.PI * 2f / 12f + f * 0.5f;
                float radius = progress * 60f;
                float px = cx + MathF.Cos(angle) * radius;
                float py = cy + MathF.Sin(angle) * radius + progress * 15f; // Leichtes Sinken

                byte alpha = (byte)((1f - progress) * 200);
                if (alpha < 5) continue;

                // Farbe pro Feuerwerk
                uint baseColor = f switch
                {
                    0 => 0xFFFF4444,
                    1 => 0xFFFFD700,
                    2 => 0xFF44FF88,
                    _ => 0xFF44AAFF,
                };
                _overlayTextPaint.Color = new SKColor(baseColor).WithAlpha(alpha);
                _overlayTextPaint.Style = SKPaintStyle.Fill;
                _overlayTextPaint.MaskFilter = _overlayGlowFilter;

                float sparkSize = (1f - progress * 0.5f) * 3f;
                canvas.DrawCircle(px, py, sparkSize, _overlayTextPaint);
            }
        }

        _overlayTextPaint.MaskFilter = null;
        _overlayTextPaint.Style = SKPaintStyle.StrokeAndFill;
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

    private void RenderPontanWarning(SKCanvas canvas)
    {
        // Pulsierender roter "!" Marker an der vorberechneten Spawn-Position
        float sx = _pontanWarningX * _renderer.Scale + _renderer.OffsetX;
        float sy = _pontanWarningY * _renderer.Scale + _renderer.OffsetY;

        float pulse = MathF.Sin(_stateTimer * 8f) * 0.5f + 0.5f; // 0→1 Puls
        float scale = 0.8f + pulse * 0.4f; // 0.8→1.2
        byte alpha = (byte)(120 + pulse * 135); // 120→255

        // Roter Kreis-Hintergrund
        _overlayBgPaint.Color = new SKColor(255, 0, 0, (byte)(alpha * 0.4f));
        float circleRadius = 14f * scale * _renderer.Scale;
        canvas.DrawCircle(sx, sy, circleRadius, _overlayBgPaint);

        // "!" Text
        _overlayFont.Size = 22f * scale * _renderer.Scale;
        _overlayTextPaint.Color = new SKColor(255, 50, 50, alpha);
        _overlayTextPaint.MaskFilter = _overlayGlowFilter;
        canvas.DrawText("!", sx, sy + 7f * scale * _renderer.Scale, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
        _overlayTextPaint.MaskFilter = null;
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
