using BomberBlast.Graphics;
using BomberBlast.Models;
using BomberBlast.Models.Dungeon;
using BomberBlast.Models.Levels;
using SkiaSharp;

namespace BomberBlast.Core;

/// <summary>
/// Rendering: State-Overlays (Starting, Paused, LevelComplete, GameOver, Victory)
/// </summary>
public sealed partial class GameEngine
{
    // Gecachte Countdown-Strings (vermeidet ToString()-Allokation im Render-Loop)
    private static readonly string[] CountdownStrings = ["0", "1", "2", "3", "4", "5"];

    // Audit H16: Cached Event-Accent-Farbe — vermeidet SKColor.Parse + try/catch pro Frame.
    private string? _cachedAccentHex;
    private SKColor _cachedAccentColor;

    // Gepoolter SKPath für Iris-Wipe Clip (statt pro-Frame new SKPath())
    private readonly SKPath _irisClipPath = new();

    // Gepoolter SKPath für Stern-Rendering in LevelComplete
    private readonly SKPath _starPath = new();

    // Gecachte Overlay-Strings (vermeidet GetString()+Format() pro Frame in Overlay-Phasen)
    private string _overlayPaused = "PAUSED";
    private string _overlayTapToResume = "Tap to resume";
    private string _overlayLevelComplete = "LEVEL COMPLETE!";
    private string _overlayFirstVictory = "FIRST VICTORY!";
    private string _overlayGameOver = "GAME OVER";
    private string _overlayVictoryTitle = "VICTORY!";
    private string _overlayAllComplete = "All levels complete!";
    // Dynamische Overlay-Strings (gecacht bei State-Wechsel)
    private string _overlayStageText = "";
    private string _overlayScoreText = "";
    private string _overlayTimeBonusText = "";
    private string _overlayFinalScoreText = "";
    private string _overlayLevelText = "";

    /// <summary>
    /// Statische Overlay-Strings cachen (bei Init + Sprachwechsel).
    /// Wird von CacheHudLabels() mitaufgerufen.
    /// </summary>
    private void CacheOverlayStrings()
    {
        _overlayPaused = _localizationService.GetString("Paused") ?? "PAUSED";
        _overlayTapToResume = _localizationService.GetString("TapToResume") ?? "Tap to resume";
        _overlayLevelComplete = _localizationService.GetString("LevelComplete") ?? "LEVEL COMPLETE!";
        _overlayFirstVictory = _localizationService.GetString("FirstVictory") ?? "FIRST VICTORY!";
        _overlayGameOver = _localizationService.GetString("GameOver") ?? "GAME OVER";
        _overlayVictoryTitle = _localizationService.GetString("VictoryTitle") ?? "VICTORY!";
        _overlayAllComplete = _localizationService.GetString("AllLevelsComplete") ?? "All levels complete!";
    }

    /// <summary>
    /// Dynamische Overlay-Strings cachen (bei State-Wechsel, nicht pro Frame).
    /// </summary>
    private void CacheStartingOverlayStrings()
    {
        var fmt = _localizationService.GetString("StageOverlay") ?? "Stage {0}";
        _overlayStageText = string.Format(fmt, _currentLevelNumber);
    }

    private void CacheLevelCompleteOverlayStrings()
    {
        var scoreFmt = _localizationService.GetString("ScoreFormat") ?? "Score: {0}";
        _overlayScoreText = string.Format(scoreFmt, _player.Score);
        var timeFmt = _localizationService.GetString("TimeBonusFormat") ?? "Time Bonus: +{0}";
        _overlayTimeBonusText = string.Format(timeFmt, LastTimeBonus);
    }

    private void CacheGameOverOverlayStrings()
    {
        var scoreFmt = _localizationService.GetString("FinalScore") ?? "Final Score: {0}";
        _overlayFinalScoreText = string.Format(scoreFmt, _player.Score);
        var levelFmt = _localizationService.GetString("LevelFormat") ?? "Level {0}";
        _overlayLevelText = string.Format(levelFmt, _currentLevelNumber);
    }

    private void CacheVictoryOverlayStrings()
    {
        var scoreFmt = _localizationService.GetString("FinalScore") ?? "Final Score: {0}";
        _overlayFinalScoreText = string.Format(scoreFmt, _player.Score);
    }
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

        // v2.0.60 (B-E1): Overlay-Glow-Filter re-init falls disposed (Android-Resume-Schutz).
        // Idempotent — Allokation nur beim ersten Frame nach Dispose oder beim ersten Render.
        EnsureOverlayFilters();

        // Audit C12: SaveCount-Backstop. Wirft eine Sub-Render-Methode eine Exception,
        // wuerden Colorblind/Zoom/Shake-Saves auf dem Canvas-Stack haengenbleiben →
        // naechster Frame mit doppeltem Zoom + Shake. Im finally klappen wir auf den
        // initialen Stand zurueck (idempotent: Restore-Calls die der Body schon erledigt
        // hat sind no-ops auf hoeherem Level).
        int initialSaveCount = canvas.SaveCount;
        try
        {

        // v2.0.45 — Performance-Telemetry: Frame-Time-Sampling für FPS-Bucket-Reporting.
        // Alle 5s wird der gemessene Avg-FPS auf einen Bucket gerundet (15/30/45/60+) und
        // als Crashlytics-Custom-Key gesetzt — ermöglicht Crash-Filterung nach Frame-Rate.
        TrackFrameSample();

        // v2.0.44 — Accessibility: Colorblind-Filter via SaveLayer + ColorMatrix.
        // SKColorFilter wird gecacht und nur neu erzeugt wenn der Modus sich ändert.
        bool colorblindActive = false;
        if (_accessibility?.ColorblindMode is { } cbMode && cbMode != "Off")
        {
            if (cbMode != _lastColorblindMode)
            {
                _colorblindFilter?.Dispose();
                var matrix = _accessibility.GetColorblindMatrix();
                _colorblindFilter = matrix != null ? SKColorFilter.CreateColorMatrix(matrix) : null;
                _colorblindLayerPaint.ColorFilter = _colorblindFilter;
                _lastColorblindMode = cbMode;
            }
            if (_colorblindFilter != null)
            {
                // Cached Paint statt Pro-Frame-Allokation (Audit C11)
                canvas.SaveLayer(_colorblindLayerPaint);
                colorblindActive = true;
            }
        }
        else if (_lastColorblindMode != "Off")
        {
            _colorblindFilter?.Dispose();
            _colorblindFilter = null;
            _lastColorblindMode = "Off";
        }

        // Viewport aktualisieren
        _renderer.CalculateViewport(screenWidth, screenHeight, _grid.PixelWidth, _grid.PixelHeight);

        // v2.0.47 — Cinematic-Director : Camera-Zoom-Effekt
        // Wenn aktive Sequence + Zoom > 0 wird Canvas auf Pivot skaliert (smoothstep-eased).
        // Reihenfolge: ZUERST Zoom, dann ScreenShake — Shake bleibt auf Bildschirm-Bounds.
        bool cinematicZoomActive = _cinematic.IsPlaying && _cinematic.CurrentZoomFactor > 0.001f;
        if (cinematicZoomActive)
        {
            float zoom = 1f + _cinematic.CurrentZoomFactor;
            float pivotScreenX = _cinematic.ZoomPivotX * _renderer.Scale + _renderer.OffsetX;
            float pivotScreenY = _cinematic.ZoomPivotY * _renderer.Scale + _renderer.OffsetY;
            canvas.Save();
            canvas.Scale(zoom, zoom, pivotScreenX, pivotScreenY);
        }

        // Screen-Shake: Canvas verschieben vor dem Spiel-Rendering
        // Phase 21 (V4): Camera-Pull-Back via canvas.Scale wenn aktiv (BigHit-Reaktion).
        // Pull-Back-Pivot ist Spielfeld-Mitte (in Screen-Koordinaten).
        bool pullBackActive = _screenShake.PullBackFactor < 0.999f;
        if (_screenShake.IsActive || pullBackActive)
        {
            canvas.Save();
            if (pullBackActive)
            {
                float pivotX = (_grid.PixelWidth / 2f) * _renderer.Scale + _renderer.OffsetX;
                float pivotY = (_grid.PixelHeight / 2f) * _renderer.Scale + _renderer.OffsetY;
                canvas.Scale(_screenShake.PullBackFactor, _screenShake.PullBackFactor, pivotX, pivotY);
            }
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

        // Mutator-Daten an Renderer übergeben
        _renderer.ActiveMutator = _activeMutator;
        _renderer.PlayerGridX = _player?.GridX ?? 0;
        _renderer.PlayerGridY = _player?.GridY ?? 0;

        // Saisonales Event (v2.0.42, Plan Task 3.4): Welt-Skin-Override.
        // Nicht im Dungeon (eigener Theme), nicht im BossRush (eigener Boss-Look).
        var currentEvent = _eventService.CurrentEvent;
        bool eventActive = currentEvent != null && !_isDungeonRun && !_isBossRushMode;
        _renderer.HasActiveEvent = eventActive;
        if (eventActive && currentEvent != null)
        {
            // Audit H16: Hex-Parse nur bei Aenderung, nicht jeden Frame im try/catch (Hot-Path).
            if (currentEvent.AccentColor != _cachedAccentHex)
            {
                _cachedAccentHex = currentEvent.AccentColor;
                try { _cachedAccentColor = SKColor.Parse(currentEvent.AccentColor); }
                catch { _cachedAccentColor = SKColors.Transparent; }
            }
            _renderer.EventAccentColor = _cachedAccentColor;
            _renderer.EventType = currentEvent.Type;
        }

        // Lokalisierte HUD-Labels übergeben (gecacht, nicht pro Frame laden)
        _renderer.HudLabelKills = _hudLabelKills;
        _renderer.HudLabelTime = _hudLabelTime;
        _renderer.HudLabelScore = _hudLabelScore;
        _renderer.HudLabelLives = _hudLabelLives;
        _renderer.HudLabelBombs = _hudLabelBombs;
        _renderer.HudLabelPower = _hudLabelPower;
        _renderer.HudLabelDeck = _hudLabelDeck;
        _renderer.HudLabelBuffs = _hudLabelBuffs;

        // Survival-Modus: Verstrichene Zeit anzeigen statt Countdown
        float displayTime = _isSurvivalMode ? (SurvivalModeState?.TimeElapsed ?? 0f) : _timer.RemainingTime;

        // Spiel rendern (gecachte Exit-Zelle + Spezialeffekt-Zellen übergeben für Performance).
        // _player ist im Ctor initialisiert und nie null zur Render-Zeit → Null-Forgiving (!) zulaessig.
        _renderer.Render(canvas, _grid, _player!,
            _enemies, _bombs, _explosions, _powerUps,
            displayTime, _player!.Score, _player.Lives, _exitCell, _specialEffectCells);

        // Partikel rendern (über dem Spielfeld, unter den Controls)
        if (_particleSystem.HasActiveParticles)
        {
            _particleSystem.Render(canvas, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);
        }

        // Floating Text rendern (Score-Popups, Combos, PowerUp-Texte)
        // v2.0.46 — HighContrast: Outline-Stroke wird 2× verstärkt für bessere Lesbarkeit
        _floatingText.HighContrast = _accessibility?.HighContrast == true;
        _floatingText.Render(canvas, _renderer.Scale, _renderer.OffsetX, _renderer.OffsetY);

        // Pontan-Spawn-Warnung rendern (pulsierendes "!" an vorberechneter Position)
        if (_pontanWarningActive && _state == GameState.Playing)
        {
            RenderPontanWarning(canvas);
        }

        // Screen-Shake Canvas wiederherstellen (Phase 21: auch bei aktivem Pull-Back)
        if (_screenShake.IsActive || pullBackActive)
        {
            canvas.Restore();
        }

        // v2.0.47 — Cinematic-Camera-Zoom-Layer schließen (NACH Shake-Restore, VOR Input-Controls
        // damit der Joystick nicht auch reingezoomt wird).
        if (cinematicZoomActive)
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

        //.2 : ULTRA-Combo Vollbild-Vignette-Flash.
        // Vor den Subtitles, ueber Tutorial-Overlay — Flash sind <200ms und blocken nichts.
        if (_ultraFlash.IsActive)
        {
            _ultraFlash.Render(canvas, screenWidth, screenHeight);
        }

        //.3 : Roter Damage-Flash bei Player-Hit.
        if (_damageFlash.IsActive)
        {
            _damageFlash.Render(canvas, screenWidth, screenHeight);
        }

        // v2.0.46 — Accessibility: Audio-Caption-Subtitles für gehörlose Spieler.
        // Wird unten am Bildrand angezeigt, immer über allem (auch über Tutorial).
        if (_accessibility?.SubtitlesEnabled == true)
        {
            _subtitles.Render(canvas, screenWidth, screenHeight);
        }

        // v2.0.44 — Colorblind-Layer wieder schließen (wenn aktiv)
        if (colorblindActive)
        {
            canvas.Restore();
        }
        }
        finally
        {
            // Audit C12: Backstop — falls eine Sub-Render-Methode geworfen hat,
            // klappen wir alle uebrigen Save()-Frames auf den Eingangs-Stand zurueck.
            while (canvas.SaveCount > initialSaveCount)
                canvas.Restore();
        }
    }

    /// <summary>
    /// v2.0.45 — Accessibility: UI-Scale für Overlay-Texte. Wird einmal pro Frame
    /// vor Overlay-Rendering aus dem AccessibilityService geholt.
    /// Wirkt nur auf Overlay-Texte (Stage X, Score Y, GameOver) — HUD-Layout-Boxes bleiben fest,
    /// damit das Spielfeld nicht überlaufen wird.
    /// </summary>
    private float _overlayUiScale = 1.0f;

    /// <summary>
    /// v2.0.48 — Accessibility: HighContrast verstärkt Overlay-Backgrounds + Borders.
    /// Bei aktiv: BG-Alpha 240 (statt 200), 2px weißer Border um Pause/GameOver/Victory-Boxen.
    /// </summary>
    private bool _overlayHighContrast;

    /// <summary>Returnt verstärkten Background-Alpha bei HighContrast (sonst Default).</summary>
    private byte GetOverlayBgAlpha(byte defaultAlpha) =>
        _overlayHighContrast ? (byte)Math.Min(255, defaultAlpha + 40) : defaultAlpha;

    /// <summary>
    /// Zeichnet einen 2px weißen Border um eine Box wenn HighContrast aktiv.
    /// Aufrufer rendert vorher das Background-Rect.
    /// </summary>
    private void RenderHighContrastBorder(SKCanvas canvas, float x, float y, float w, float h, byte alpha)
    {
        if (!_overlayHighContrast) return;
        var prevColor = _overlayTextPaint.Color;
        var prevStyle = _overlayTextPaint.Style;
        var prevWidth = _overlayTextPaint.StrokeWidth;
        _overlayTextPaint.Color = new SKColor(255, 255, 255, alpha);
        _overlayTextPaint.Style = SKPaintStyle.Stroke;
        _overlayTextPaint.StrokeWidth = 2f;
        canvas.DrawRect(x, y, w, h, _overlayTextPaint);
        _overlayTextPaint.Color = prevColor;
        _overlayTextPaint.Style = prevStyle;
        _overlayTextPaint.StrokeWidth = prevWidth;
    }

    /// <summary>
    /// v2.0.45 — Frame-Time-Tracking: pro Render-Aufruf wird ein Tick-Sample gepusht,
    /// alle ~5s wird der Avg-FPS auf einen Bucket gerundet und an Telemetry geschickt.
    /// </summary>
    private void TrackFrameSample()
    {
        var now = DateTime.UtcNow.Ticks;
        _fpsFrameTicks.Enqueue(now);

        // Älter als 5s entfernen
        while (_fpsFrameTicks.Count > 0 && now - _fpsFrameTicks.Peek() > FpsReportIntervalTicks)
            _fpsFrameTicks.Dequeue();

        // Alle 5s reporten
        if (now - _lastFpsReportTicks > FpsReportIntervalTicks && _fpsFrameTicks.Count > 10)
        {
            _lastFpsReportTicks = now;
            float seconds = (now - _fpsFrameTicks.Peek()) / (float)TimeSpan.TicksPerSecond;
            int fps = seconds > 0.1f ? (int)(_fpsFrameTicks.Count / seconds) : 0;

            // Bucket: 15 / 30 / 45 / 60+
            int bucket = fps switch
            {
                < 22 => 15,
                < 37 => 30,
                < 52 => 45,
                _ => 60
            };
            _telemetry?.SetFpsBucket(bucket);
            _telemetry?.SetCustomKey("game_mode", GetCurrentModeTag());
            _telemetry?.SetCustomKey("level", _currentLevelNumber);
        }

        // v2.0.55 — Phase 15 P1-Fix: Memory-Sampling auf Background-Thread + 60s-Intervall.
        // Vorher (UI-Thread, 30s): GC.GetTotalMemory(false) auf Mono-AOT-Android = 1-5ms Heap-Walk-Spike.
        // Telemetry.SetCustomKey-Setter sind in Crashlytics thread-safe.
        if (now - _lastMemoryReportTicks > MemoryReportIntervalTicks)
        {
            _lastMemoryReportTicks = now;
            var localTelemetry = _telemetry;
            if (localTelemetry != null)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        long heapBytes = GC.GetTotalMemory(forceFullCollection: false);
                        int memoryMb = (int)(heapBytes / (1024 * 1024));
                        localTelemetry.SetCustomKey("memory_mb", memoryMb);
                        localTelemetry.SetCustomKey("gc_gen0", GC.CollectionCount(0));
                        localTelemetry.SetCustomKey("gc_gen1", GC.CollectionCount(1));
                        localTelemetry.SetCustomKey("gc_gen2", GC.CollectionCount(2));
                    }
                    catch
                    {
                        // GC-API ist auf manchen AOT-Profilen restricted — silently fail
                    }
                });
            }
        }
    }

    private void RenderStateOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        // UI-Scale holen (0.75 / 1.0 / 1.25 / 1.5)
        _overlayUiScale = (float)(_accessibility?.UiScale ?? 1.0);
        // v2.0.48 — HighContrast-Flag aus Accessibility (verstärkt Overlay-Backgrounds + Borders)
        _overlayHighContrast = _accessibility?.HighContrast == true;
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
        _irisClipPath.Rewind();
        _irisClipPath.AddRect(new SKRect(0, 0, screenWidth, screenHeight));
        _irisClipPath.AddCircle(screenWidth / 2f, screenHeight / 2f, irisRadius, SKPathDirection.CounterClockwise);
        canvas.ClipPath(_irisClipPath);
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

        _overlayFont.Size = 48 * _overlayUiScale;
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = _overlayGlowFilter;

        canvas.DrawText(_overlayStageText, screenWidth / 2, screenHeight / 2, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        // Countdown
        int countdown = (int)MathF.Ceiling(START_DELAY - _stateTimer);
        _overlayFont.Size = 72 * _overlayUiScale;
        _overlayTextPaint.Color = SKColors.Yellow;
        var countdownText = countdown >= 0 && countdown < CountdownStrings.Length ? CountdownStrings[countdown] : countdown.ToString();
        canvas.DrawText(countdownText, screenWidth / 2, screenHeight / 2 + 80, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
    }

    private void RenderPausedOverlay(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        _overlayBgPaint.Color = new SKColor(0, 0, 0, GetOverlayBgAlpha(200));
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);
        // v2.0.48 — HighContrast: Weißer Frame um den Bildschirm
        RenderHighContrastBorder(canvas, 4, 4, screenWidth - 8, screenHeight - 8, 200);

        _overlayFont.Size = 48 * _overlayUiScale;
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = _overlayGlowFilter;

        canvas.DrawText(_overlayPaused, screenWidth / 2, screenHeight / 2, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayFont.Size = 24 * _overlayUiScale;
        _overlayTextPaint.MaskFilter = null;
        canvas.DrawText(_overlayTapToResume, screenWidth / 2, screenHeight / 2 + 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
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
            _irisClipPath.Rewind();
            _irisClipPath.AddRect(new SKRect(0, 0, screenWidth, screenHeight));
            _irisClipPath.AddCircle(screenWidth / 2f, screenHeight / 2f, Math.Max(irisRadius, 1), SKPathDirection.CounterClockwise);
            canvas.ClipPath(_irisClipPath);
            var irisColor = GetIrisWipeColor();
            _overlayBgPaint.Color = new SKColor(irisColor.Red, irisColor.Green, irisColor.Blue, 255);
            canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);
            canvas.Restore();
        }

        _overlayBgPaint.Color = new SKColor(0, 50, 0, 200);
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayBgPaint);

        // Celebration-Confetti (fallende bunte Rechtecke)
        RenderCelebrationParticles(canvas, screenWidth, screenHeight, _stateTimer, 25, false);

        _overlayFont.Size = 48 * _overlayUiScale;
        _overlayTextPaint.Color = SKColors.Green;
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;

        canvas.DrawText(_overlayLevelComplete, screenWidth / 2, screenHeight / 2 - 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        // Erster Sieg: Extra goldener Text über "Level Complete"
        if (_isFirstVictory)
        {
            _overlayFont.Size = 36 * _overlayUiScale;
            _overlayTextPaint.Color = BomberBlastColors.Gold; // Gold
            float victoryPulse = 1f + MathF.Sin(_stateTimer * 6f) * 0.1f;
            canvas.Save();
            canvas.Translate(screenWidth / 2, screenHeight / 2 - 100);
            canvas.Scale(victoryPulse);
            canvas.DrawText(_overlayFirstVictory, 0, 0, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
            canvas.Restore();
        }

        _overlayTextPaint.Color = SKColors.Yellow;
        _overlayTextPaint.MaskFilter = null;
        _overlayFont.Size = 32 * _overlayUiScale;
        canvas.DrawText(_overlayScoreText, screenWidth / 2, screenHeight / 2 + 20, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayTextPaint.Color = SKColors.Cyan;
        _overlayFont.Size = 24 * _overlayUiScale;
        canvas.DrawText(_overlayTimeBonusText, screenWidth / 2, screenHeight / 2 + 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

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

                // Stern zeichnen (5-zackiger Stern via gepooltem SKPath)
                _starPath.Rewind();
                for (int p = 0; p < 10; p++)
                {
                    float angle = MathF.PI / 2f + p * MathF.PI / 5f;
                    float r = p % 2 == 0 ? s : s * 0.4f;
                    float px = sx + MathF.Cos(angle) * r;
                    float py = starY - MathF.Sin(angle) * r;
                    if (p == 0) _starPath.MoveTo(px, py);
                    else _starPath.LineTo(px, py);
                }
                _starPath.Close();

                _overlayTextPaint.Style = SKPaintStyle.Fill;
                _overlayTextPaint.MaskFilter = earned ? _overlayGlowFilter : null;
                _overlayTextPaint.Color = earned
                    ? new SKColor(255, 215, 0, (byte)(255 * starProgress))  // Gold
                    : new SKColor(80, 80, 80, (byte)(150 * starProgress));  // Grau (nicht verdient)

                canvas.DrawPath(_starPath, _overlayTextPaint);

                // Umrandung
                _overlayTextPaint.Style = SKPaintStyle.Stroke;
                _overlayTextPaint.StrokeWidth = 1.5f;
                _overlayTextPaint.Color = earned
                    ? new SKColor(200, 160, 0, (byte)(255 * starProgress))
                    : new SKColor(60, 60, 60, (byte)(150 * starProgress));
                canvas.DrawPath(_starPath, _overlayTextPaint);
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

        _overlayFont.Size = 64 * _overlayUiScale;
        _overlayTextPaint.Color = SKColors.Red;
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;

        canvas.DrawText(_overlayGameOver, screenWidth / 2, screenHeight / 2 - 50, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = null;
        _overlayFont.Size = 32 * _overlayUiScale;
        canvas.DrawText(_overlayFinalScoreText, screenWidth / 2, screenHeight / 2 + 20, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayFont.Size = 24 * _overlayUiScale;
        canvas.DrawText(_overlayLevelText, screenWidth / 2, screenHeight / 2 + 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);
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

        _overlayFont.Size = 56 * _overlayUiScale;
        _overlayTextPaint.Color = BomberBlastColors.Gold; // Gold
        _overlayTextPaint.MaskFilter = _overlayGlowFilterLarge;

        canvas.DrawText(_overlayVictoryTitle, screenWidth / 2, screenHeight / 2 - 60, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayFont.Size = 28 * _overlayUiScale;
        _overlayTextPaint.Color = SKColors.White;
        _overlayTextPaint.MaskFilter = null;

        canvas.DrawText(_overlayAllComplete, screenWidth / 2, screenHeight / 2, SKTextAlign.Center, _overlayFont, _overlayTextPaint);

        _overlayTextPaint.Color = BomberBlastColors.Gold;
        _overlayFont.Size = 32 * _overlayUiScale;
        canvas.DrawText(_overlayFinalScoreText,
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

        if (isVictory)
        {
            // Victory: Goldener Glitzer mit Two-Pass-Rendering.
            // Pass 0 = ohne Glow (sparkle <= 0.7f), Pass 1 = mit Glow (sparkle > 0.7f).
            // Spart Paint-State-Wechsel von ~5-10 pro Frame auf 2 (1 pro Pass).
            // Die deterministische Math wird zwar 2x ausgefuehrt, ist aber sehr
            // guenstig (Modulo + Sinus pro Partikel).
            _overlayTextPaint.Style = SKPaintStyle.Fill;
            for (int pass = 0; pass < 2; pass++)
            {
                bool passUsesGlow = pass == 1;
                _overlayTextPaint.MaskFilter = passUsesGlow ? _overlayGlowFilter : null;

                for (int i = 0; i < count; i++)
                {
                    int seed = i * 7919 + 1013;
                    float phase = ((seed * 17 + 443) % 1000) / 1000f * MathF.PI * 2f;
                    float sparkle = MathF.Abs(MathF.Sin(timer * 8f + phase));
                    bool needsGlow = sparkle > 0.7f;
                    if (needsGlow != passUsesGlow) continue;

                    float px = (seed % 1000) / 1000f;
                    float py = ((seed * 3 + 571) % 1000) / 1000f;
                    float speed = 0.3f + ((seed * 7 + 233) % 600) / 1000f;
                    float drift = ((seed * 13 + 97) % 1000) / 1000f - 0.5f;
                    int colorIdx = (seed * 11 + 67) % colors.Length;
                    float size = 2f + ((seed * 23 + 311) % 400) / 100f;

                    float x = px * sw + MathF.Sin(timer * 2f + phase) * 20f * drift;
                    float y = py * sh + timer * speed * 100f;
                    y = y % (sh + 20f);

                    float delay = i * 0.05f;
                    float alpha = Math.Clamp((timer - delay) * 3f, 0f, 1f);
                    if (alpha <= 0) continue;

                    byte a = (byte)(alpha * (150 + sparkle * 105));
                    _overlayTextPaint.Color = new SKColor(colors[colorIdx]).WithAlpha(a);
                    canvas.DrawCircle(x, y, size * (0.8f + sparkle * 0.4f), _overlayTextPaint);
                }
            }
        }
        else
        {
            // LevelComplete: Confetti-Rechtecke mit Rotation - kein Glow-Toggle noetig
            _overlayTextPaint.Style = SKPaintStyle.Fill;
            _overlayTextPaint.MaskFilter = null;

            for (int i = 0; i < count; i++)
            {
                int seed = i * 7919 + 1013;
                float px = (seed % 1000) / 1000f;
                float py = ((seed * 3 + 571) % 1000) / 1000f;
                float speed = 0.3f + ((seed * 7 + 233) % 600) / 1000f;
                float drift = ((seed * 13 + 97) % 1000) / 1000f - 0.5f;
                float phase = ((seed * 17 + 443) % 1000) / 1000f * MathF.PI * 2f;
                int colorIdx = (seed * 11 + 67) % colors.Length;
                float size = 2f + ((seed * 23 + 311) % 400) / 100f;

                float x = px * sw + MathF.Sin(timer * 2f + phase) * 20f * drift;
                float y = py * sh + timer * speed * 100f;
                y = y % (sh + 20f);

                float delay = i * 0.05f;
                float alpha = Math.Clamp((timer - delay) * 3f, 0f, 1f);
                if (alpha <= 0) continue;

                float rotation = timer * (200f + i * 30f) + phase * 57.3f;
                byte a = (byte)(alpha * 200);
                _overlayTextPaint.Color = new SKColor(colors[colorIdx]).WithAlpha(a);

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
        _overlayFont.Size = 48 * _overlayUiScale;
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
