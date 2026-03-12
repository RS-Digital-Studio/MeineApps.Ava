namespace RebornSaga.Rendering.Characters;

using RebornSaga.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// Animations-Zustand pro Charakter. Jeder Charakter hat eigenes Blinzel-Timing
/// und eigenen Mund-Zustand, damit sie nicht synchron blinzeln.
/// </summary>
public struct CharacterAnimState
{
    public float NextBlinkTime;
    public bool IsBlinking;
    public bool IsSpeaking;
    public int MouthFrame;        // 0=geschlossen, 1=offen, 2=weit
    public float MouthTimer;
}

/// <summary>
/// Rendert Charaktere aus AI-generierten Einzelbildern (komplette Bilder pro Pose+Emotion).
/// Kein Head/Body-Compositing mehr. Per-Character AnimState für unabhängiges Blinzeln.
/// Statische Klasse — gepoolte Paints, keine per-Frame-Allokationen.
/// </summary>
public static class SpriteCharacterRenderer
{
    // --- Gepoolte SKPaint-Instanzen ---
    private static readonly SKPaint _spritePaint = new() { IsAntialias = true };

    // --- Blinzel-Konstanten ---
    private const float BlinkDuration = 0.15f;
    private const float BlinkIntervalMin = 4.0f;
    private const float BlinkIntervalMax = 7.0f;

    // --- Mund-Animation ---
    private const float MouthToggleInterval = 0.2f;

    // --- Idle-Breathing ---
    private const float BreathFrequency = 0.7f;  // Langsameres Atmen
    private const float BreathAmplitude = 1.0f;   // Subtilere Bewegung

    // Per-Character AnimState (kein synchrones Blinzeln mehr)
    private static readonly Dictionary<string, CharacterAnimState> _animStates = new();
    private static readonly Random _rng = new();

    /// <summary>
    /// Setzt den Sprech-Zustand für einen bestimmten Charakter.
    /// </summary>
    public static void SetSpeaking(string charId, bool speaking)
    {
        var state = GetOrCreateState(charId);
        state.IsSpeaking = speaking;
        if (!speaking)
        {
            state.MouthFrame = 0;
            state.MouthTimer = 0;
        }
        _animStates[charId] = state;
    }

    /// <summary>
    /// Zeichnet einen Charakter aus dem Sprite-Cache auf den Canvas.
    /// Gibt false zurück wenn kein Sprite für den Charakter vorhanden ist.
    /// </summary>
    public static bool Draw(SKCanvas canvas, string charId, Pose pose, Emotion emotion,
        float cx, float cy, float scale, float time, SpriteCache cache)
    {
        // Hauptbild laden
        var sprite = cache.GetSprite(charId, pose, emotion);
        if (sprite == null)
            return false;

        // AnimState holen/erstellen
        var state = GetOrCreateState(charId);

        // Blinzel-Animation aktualisieren
        UpdateBlink(ref state, time);

        // Mund-Animation aktualisieren
        UpdateMouth(ref state, time);

        // Idle-Breathing (subtile vertikale Bewegung)
        var breathOffset = MathF.Sin(time * BreathFrequency * MathF.PI * 2) * BreathAmplitude * scale;

        // Sprite-Ziel-Rect berechnen (zentriert auf cx, cy)
        var destRect = CalculateDestRect(sprite, cx, cy + breathOffset, scale);

        // Sprite immer mit voller Deckkraft zeichnen (kein Crossfade).
        // Die alte Crossfade-Logik (Alpha 0→1 über 300ms) verursachte Portrait/FullBody-Zappeln
        // weil die Vignette-Maske bei niedrigem Alpha den Charakter visuell beschnitt.
        _spritePaint.Color = SKColors.White;

        // Hauptbild zeichnen
        canvas.DrawBitmap(sprite, destRect, _spritePaint);

        // DEAKTIVIERT: Overlay-Dateien sind Vollbilder (nicht transparente Patches).
        // Sie ersetzen den gesamten Charakter visuell → Springen zwischen Framings.
        // TODO: Overlays als echte transparente Eye/Mouth-Patches regenerieren,
        // dann diesen Code reaktivieren.
        // Blinzel-Overlay
        // if (state.IsBlinking)
        // {
        //     var blink = cache.GetBlinkOverlay(charId);
        //     if (blink != null)
        //     {
        //         var blinkRect = CalculateDestRect(blink, cx, cy + breathOffset, scale);
        //         canvas.DrawBitmap(blink, blinkRect, _spritePaint);
        //     }
        // }
        //
        // Mund-Overlay (nur wenn sprechend)
        // if (state.IsSpeaking && state.MouthFrame > 0)
        // {
        //     var wide = state.MouthFrame == 2;
        //     var mouth = cache.GetMouthOverlay(charId, wide);
        //     if (mouth != null)
        //     {
        //         var mouthRect = CalculateDestRect(mouth, cx, cy + breathOffset, scale);
        //         canvas.DrawBitmap(mouth, mouthRect, _spritePaint);
        //     }
        // }

        // State zurückschreiben
        _animStates[charId] = state;

        return true;
    }

    /// <summary>
    /// Entfernt den AnimState eines Charakters (z.B. wenn er die Szene verlässt).
    /// </summary>
    public static void ClearState(string charId)
    {
        _animStates.Remove(charId);
    }

    /// <summary>
    /// Entfernt alle AnimStates.
    /// </summary>
    public static void ClearAllStates()
    {
        _animStates.Clear();
    }

    /// <summary>
    /// Gibt statische Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _spritePaint.Dispose();
        _animStates.Clear();
    }

    // --- Private Hilfsmethoden ---

    private static CharacterAnimState GetOrCreateState(string charId)
    {
        if (_animStates.TryGetValue(charId, out var state))
            return state;

        // Neuen State mit versetztem Blinzel-Timing erstellen
        state = new CharacterAnimState
        {
            NextBlinkTime = (float)_rng.NextDouble() * BlinkIntervalMax
        };
        _animStates[charId] = state;
        return state;
    }

    private static void UpdateBlink(ref CharacterAnimState state, float time)
    {
        if (state.IsBlinking)
        {
            if (time >= state.NextBlinkTime + BlinkDuration)
            {
                state.IsBlinking = false;
                state.NextBlinkTime = time + BlinkIntervalMin +
                    (float)_rng.NextDouble() * (BlinkIntervalMax - BlinkIntervalMin);
            }
        }
        else if (time >= state.NextBlinkTime)
        {
            state.IsBlinking = true;
        }
    }

    private static void UpdateMouth(ref CharacterAnimState state, float time)
    {
        if (!state.IsSpeaking)
        {
            state.MouthFrame = 0;
            return;
        }

        if (time - state.MouthTimer >= MouthToggleInterval)
        {
            // Zwischen geschlossen (0), offen (1), weit (2) wechseln
            state.MouthFrame = state.MouthFrame switch
            {
                0 => 1,
                1 => _rng.Next(3) == 0 ? 2 : 0, // Manchmal weit öffnen
                _ => 0
            };
            state.MouthTimer = time;
        }
    }

    /// <summary>
    /// Berechnet das Ziel-Rect für ein Sprite, zentriert auf (cx, cy).
    /// </summary>
    private static SKRect CalculateDestRect(SKBitmap bitmap, float cx, float cy, float scale)
    {
        var destW = bitmap.Width * scale;
        var destH = bitmap.Height * scale;
        return new SKRect(
            cx - destW * 0.5f,
            cy - destH * 0.5f,
            cx + destW * 0.5f,
            cy + destH * 0.5f);
    }
}
