namespace RebornSaga.Overlays;

using RebornSaga.Engine;
using RebornSaga.Models;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// Zeigt kurze Effekt-Benachrichtigungen (Karma, Affinität, EXP, Gold)
/// als aufsteigende Floating-Texte. Auto-Dismiss nach 2.5 Sekunden.
/// Blockiert keine Eingaben (passthrough).
/// </summary>
public class EffectFeedbackOverlay : Scene
{
    /// <summary>Input wird an die Szene darunter durchgereicht.</summary>
    public override bool ConsumesInput => false;

    private float _time;
    private const float Duration = 2.5f;
    private const float FadeIn = 0.2f;
    private const float FadeOut = 0.6f;
    private const float RiseSpeed = 30f; // Pixel pro Sekunde nach oben

    private readonly List<EffectLine> _lines = new();

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKFont _font = new() { LinearMetrics = true };
    private static readonly SKFont _headerFont = new() { LinearMetrics = true };

    /// <summary>
    /// Erzeugt Benachrichtigungs-Zeilen aus StoryEffects.
    /// </summary>
    public void SetEffects(StoryEffects effects)
    {
        _lines.Clear();

        if (effects.Karma != 0)
        {
            var sign = effects.Karma > 0 ? "+" : "";
            var color = effects.Karma > 0
                ? new SKColor(0x4A, 0xDE, 0x80) // Grün für positives Karma
                : new SKColor(0xF8, 0x71, 0x71); // Rot für negatives Karma
            _lines.Add(new EffectLine($"Karma {sign}{effects.Karma}", color));
        }

        if (effects.Affinity != null)
        {
            foreach (var (charId, delta) in effects.Affinity)
            {
                if (delta == 0) continue;
                var sign = delta > 0 ? "+" : "";
                var name = char.ToUpper(charId[0]) + charId[1..];
                var color = delta > 0
                    ? new SKColor(0x93, 0xC5, 0xFD) // Hellblau für positive Affinität
                    : new SKColor(0xFD, 0xBA, 0x74); // Orange für negative Affinität
                _lines.Add(new EffectLine($"{name} {sign}{delta}", color));
            }
        }

        if (effects.Exp != 0)
        {
            _lines.Add(new EffectLine($"+{effects.Exp} EXP",
                new SKColor(0xFB, 0xBF, 0x24))); // Gelb/Gold
        }

        if (effects.Gold != 0)
        {
            var sign = effects.Gold > 0 ? "+" : "";
            _lines.Add(new EffectLine($"{sign}{effects.Gold} Gold",
                new SKColor(0xF5, 0xC5, 0x42))); // Gold-Shimmer
        }

        if (effects.AddItems != null)
        {
            foreach (var item in effects.AddItems)
                _lines.Add(new EffectLine($"+ {item}", new SKColor(0xA7, 0x8B, 0xFA))); // Lila
        }
    }

    public override void OnEnter()
    {
        _time = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        if (_time >= Duration + FadeOut)
            SceneManager.HideOverlay(this);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        if (_lines.Count == 0) return;

        // Alpha berechnen
        float alpha;
        if (_time < FadeIn)
            alpha = _time / FadeIn;
        else if (_time > Duration)
            alpha = Math.Max(0, 1f - (_time - Duration) / FadeOut);
        else
            alpha = 1f;

        var byteAlpha = (byte)(alpha * 255);

        // Position: oben rechts, schwebt nach oben
        var fontSize = bounds.Width * 0.028f;
        _font.Size = fontSize;
        _headerFont.Size = fontSize * 0.7f;

        var lineHeight = fontSize * 1.6f;
        var totalHeight = _lines.Count * lineHeight + lineHeight; // +1 für Header
        var panelW = bounds.Width * 0.35f;
        var panelH = totalHeight + 16;
        var riseOffset = _time * RiseSpeed;

        var panelX = bounds.Right - panelW - 12;
        var panelY = bounds.Top + bounds.Height * 0.08f - riseOffset * 0.3f;

        // Halbtransparenter Hintergrund
        var panelRect = new SKRect(panelX, panelY, panelX + panelW, panelY + panelH);
        _bgPaint.Color = new SKColor(0x0A, 0x0E, 0x15, (byte)(byteAlpha * 0.75f));
        using var roundRect = new SKRoundRect(panelRect, 6f);
        canvas.DrawRoundRect(roundRect, _bgPaint);

        // Header ">>> EFFEKTE"
        _textPaint.Color = UIRenderer.Primary.WithAlpha((byte)(byteAlpha * 0.6f));
        canvas.DrawText(">>> STATUS", panelX + 10, panelY + lineHeight * 0.8f,
            SKTextAlign.Left, _headerFont, _textPaint);

        // Effekt-Zeilen
        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            var y = panelY + lineHeight * (i + 1.6f);

            _textPaint.Color = line.Color.WithAlpha(byteAlpha);
            canvas.DrawText(line.Text, panelX + 14, y, SKTextAlign.Left, _font, _textPaint);
        }
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        // Kein Input-Blocking - Input wird an die Szene darunter durchgereicht
        // Overlay dismissed automatisch nach 2.5s
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
        _headerFont.Dispose();
    }

    private readonly record struct EffectLine(string Text, SKColor Color);
}
