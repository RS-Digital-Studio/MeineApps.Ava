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

        // Spiel rendern
        _renderer.Render(canvas, _grid, _player,
            _enemies, _bombs, _explosions, _powerUps,
            _timer.RemainingTime, _player.Score, _player.Lives);

        // Partikel rendern (über dem Spielfeld, unter den Controls)
        if (_particleSystem.HasActiveParticles)
        {
            _particleSystem.Render(canvas, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);
        }

        // Screen-Shake Canvas wiederherstellen
        if (_screenShake.IsActive)
        {
            canvas.Restore();
        }

        // Input-Controls rendern (NICHT vom Shake beeinflusst)
        _inputManager.Render(canvas, screenWidth, screenHeight);

        // State-Overlays rendern
        RenderStateOverlay(canvas, screenWidth, screenHeight);

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
        _overlayBgPaint.Color = new SKColor(0, 0, 0, 180);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

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
        int timeBonusMultiplier = _shopService.GetTimeBonusMultiplier();
        int timeBonus = (int)_timer.RemainingTime * timeBonusMultiplier;
        canvas.DrawText(string.Format(_localizationService.GetString("TimeBonusFormat"), timeBonus), screenWidth / 2, screenHeight / 2 + 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
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
}
