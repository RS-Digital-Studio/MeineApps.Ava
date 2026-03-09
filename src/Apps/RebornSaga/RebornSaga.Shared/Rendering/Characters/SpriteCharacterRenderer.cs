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
    public float CrossfadeAlpha;  // 0-1 für Emotions-Überblendung
    public string? PreviousSpriteKey; // Für Crossfade zum vorherigen Bild
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
    private static readonly SKPaint _fadePaint = new() { IsAntialias = true };

    // --- Blinzel-Konstanten ---
    private const float BlinkDuration = 0.15f;
    private const float BlinkIntervalMin = 3.0f;
    private const float BlinkIntervalMax = 5.0f;

    // --- Mund-Animation ---
    private const float MouthToggleInterval = 0.15f;

    // --- Crossfade ---
    private const float CrossfadeDuration = 0.15f; // 150ms Überblendung

    // --- Idle-Breathing ---
    private const float BreathFrequency = 1.5f;
    private const float BreathAmplitude = 1.5f;

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

        // --- Crossfade-Überblendung bei Emotions-Wechsel ---
        var currentKey = $"{charId}_{pose}_{emotion}";
        if (state.PreviousSpriteKey != null && state.PreviousSpriteKey != currentKey && state.CrossfadeAlpha < 1f)
        {
            // Crossfade läuft noch — altes Bild wird nicht mehr gezeichnet wenn Alpha = 1
            state.CrossfadeAlpha = MathF.Min(1f, state.CrossfadeAlpha + (1f / (CrossfadeDuration * 60f)));
            _spritePaint.Color = new SKColor(255, 255, 255, (byte)(state.CrossfadeAlpha * 255));
        }
        else
        {
            // Kein Crossfade oder abgeschlossen
            state.CrossfadeAlpha = 1f;
            state.PreviousSpriteKey = currentKey;
            _spritePaint.Color = SKColors.White;
        }

        // Wenn neuer Sprite-Key erkannt → Crossfade starten
        if (state.PreviousSpriteKey != currentKey && state.CrossfadeAlpha >= 1f)
        {
            state.CrossfadeAlpha = 0f;
            state.PreviousSpriteKey = currentKey;
        }

        // Hauptbild zeichnen
        canvas.DrawBitmap(sprite, destRect, _spritePaint);

        // Blinzel-Overlay
        if (state.IsBlinking)
        {
            var blink = cache.GetBlinkOverlay(charId);
            if (blink != null)
            {
                var blinkRect = CalculateDestRect(blink, cx, cy + breathOffset, scale);
                canvas.DrawBitmap(blink, blinkRect, _spritePaint);
            }
        }

        // Mund-Overlay (nur wenn sprechend)
        if (state.IsSpeaking && state.MouthFrame > 0)
        {
            var wide = state.MouthFrame == 2;
            var mouth = cache.GetMouthOverlay(charId, wide);
            if (mouth != null)
            {
                var mouthRect = CalculateDestRect(mouth, cx, cy + breathOffset, scale);
                canvas.DrawBitmap(mouth, mouthRect, _spritePaint);
            }
        }

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
        _fadePaint.Dispose();
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
            NextBlinkTime = (float)_rng.NextDouble() * BlinkIntervalMax,
            CrossfadeAlpha = 1f
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
