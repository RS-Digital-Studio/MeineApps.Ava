namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// Scrollbare Dialog-Historie. Zeigt die letzten Dialogzeilen.
/// Max 200 Einträge, älteste werden automatisch entfernt.
/// Thread-safe: Lock schützt _entries vor gleichzeitigem Zugriff (AddEntry von Szenen, Render vom UI-Thread).
/// </summary>
public class BacklogOverlay : Scene
{
    private const int MaxEntries = 200;
    private static readonly List<BacklogEntry> _entries = new(); // Statisch: überlebt Szenen-Wechsel
    private static readonly object _entriesLock = new(); // Schützt _entries vor konkurrierenden Zugriffen
    private float _scrollOffset;
    private float _scrollVelocity;
    private float _time;
    private float _lastEntryHeight;   // Gecacht für Scroll-Grenze
    private float _visibleAreaHeight; // Sichtbarer Bereich für korrekte Scroll-Grenze

    // Drag-Tracking für fließendes Scrollen per Touch
    private bool _isDragging;
    private float _dragLastY;

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Color = new SKColor(0x0D, 0x11, 0x17, 230) };
    private static readonly SKFont _nameFont = new() { LinearMetrics = true };
    private static readonly SKFont _textFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKPaint _separatorPaint = new() { IsAntialias = true, Color = UIRenderer.Border.WithAlpha(40), StrokeWidth = 0.5f };

    // Back-Button
    private SKRect _closeButtonRect;

    private readonly ILocalizationService _localization;
    private string _backlogTitle = "Dialogue Log";
    private string _closeText = "Close";

    public BacklogOverlay(ILocalizationService localization)
    {
        _localization = localization;
        _backlogTitle = _localization.GetString("DialogBacklog") ?? "Dialogue Log";
        _closeText = _localization.GetString("Close") ?? "Close";
    }

    /// <summary>
    /// Fügt einen Dialog-Eintrag zum Backlog hinzu (static, damit von DialogueScene aus zugänglich).
    /// Thread-safe durch Lock.
    /// </summary>
    public static void AddEntry(string speakerName, string text, SKColor nameColor)
    {
        lock (_entriesLock)
        {
            _entries.Add(new BacklogEntry
            {
                SpeakerName = speakerName,
                Text = text,
                NameColor = nameColor
            });

            // Alte Einträge entfernen
            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
        }
    }

    /// <summary>Leert das Backlog (z.B. bei neuem Spiel).</summary>
    public static void Clear()
    {
        lock (_entriesLock)
        {
            _entries.Clear();
        }
    }

    public override void OnEnter()
    {
        _scrollOffset = 0;
        _scrollVelocity = 0;
        _time = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        // Scroll-Trägheit (framerate-unabhängig)
        if (MathF.Abs(_scrollVelocity) > 0.5f)
        {
            _scrollOffset += _scrollVelocity * deltaTime;
            _scrollVelocity *= MathF.Pow(0.005f, deltaTime); // ~0.005 nach 1 Sekunde
        }
        else
        {
            _scrollVelocity = 0;
        }

        // Scroll-Grenzen (oben und unten)
        _scrollOffset = Math.Max(0, _scrollOffset);
        if (_lastEntryHeight > 0 && _visibleAreaHeight > 0)
        {
            int entryCount;
            lock (_entriesLock)
            {
                entryCount = _entries.Count;
            }
            // Gesamthöhe aller Einträge minus sichtbarer Bereich
            var totalContentHeight = entryCount * _lastEntryHeight;
            var maxScroll = Math.Max(0, totalContentHeight - _visibleAreaHeight);
            _scrollOffset = Math.Min(_scrollOffset, maxScroll);
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Halbtransparenter Hintergrund
        canvas.DrawRect(bounds, _bgPaint);

        // Titel
        UIRenderer.DrawTextWithShadow(canvas, _backlogTitle,
            bounds.MidX, bounds.Height * 0.05f, bounds.Width * 0.05f, UIRenderer.PrimaryGlow);

        // Einträge (älteste oben, neueste unten)
        var entryHeight = bounds.Height * 0.08f;
        _lastEntryHeight = entryHeight;
        _visibleAreaHeight = bounds.Height * 0.8f; // Sichtbarer Bereich (~12% bis ~93%)
        var startY = bounds.Height * 0.12f - _scrollOffset;
        var margin = bounds.Width * 0.05f;

        _nameFont.Size = entryHeight * 0.25f;
        _textFont.Size = entryHeight * 0.3f;

        // Thread-sicherer Snapshot der Einträge (Lock nur kurz für Kopie)
        BacklogEntry[] snapshot;
        lock (_entriesLock)
        {
            snapshot = _entries.ToArray();
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            var entry = snapshot[i];
            var y = startY + i * entryHeight;

            // Nur sichtbare Einträge rendern
            if (y + entryHeight < bounds.Top || y > bounds.Bottom) continue;

            // Sprecher-Name
            _textPaint.Color = entry.NameColor;
            canvas.DrawText(entry.SpeakerName, margin, y + entryHeight * 0.3f,
                SKTextAlign.Left, _nameFont, _textPaint);

            // Text
            _textPaint.Color = UIRenderer.TextSecondary;
            canvas.DrawText(entry.Text, margin, y + entryHeight * 0.65f,
                SKTextAlign.Left, _textFont, _textPaint);

            // Trennlinie (gecachter Paint statt per-Frame-Allokation)
            canvas.DrawLine(margin, y + entryHeight - 1, bounds.Right - margin, y + entryHeight - 1,
                _separatorPaint);
        }

        // Schließen-Button
        var btnW = bounds.Width * 0.2f;
        var btnH = bounds.Height * 0.045f;
        _closeButtonRect = new SKRect(
            bounds.MidX - btnW / 2, bounds.Height * 0.93f,
            bounds.MidX + btnW / 2, bounds.Height * 0.93f + btnH);
        UIRenderer.DrawButton(canvas, _closeButtonRect, _closeText);
    }

    public override void HandlePointerDown(SKPoint position)
    {
        _isDragging = true;
        _dragLastY = position.Y;
        _scrollVelocity = 0; // Trägheit stoppen bei neuem Touch
    }

    public override void HandlePointerMove(SKPoint position)
    {
        if (!_isDragging) return;

        var deltaY = _dragLastY - position.Y;
        _scrollOffset += deltaY;
        _scrollVelocity = deltaY / 0.016f; // Geschwindigkeit für Trägheit (~60fps)
        _dragLastY = position.Y;
    }

    public override void HandlePointerUp(SKPoint position)
    {
        _isDragging = false;
        // _scrollVelocity bleibt für Trägheits-Effekt erhalten
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        switch (action)
        {
            case InputAction.Back:
                SceneManager.HideOverlay(this);
                break;

            case InputAction.Tap:
                if (UIRenderer.HitTest(_closeButtonRect, position))
                    SceneManager.HideOverlay(this);
                break;

            case InputAction.SwipeUp:
                _scrollVelocity = 300f;
                break;

            case InputAction.SwipeDown:
                _scrollVelocity = -300f;
                break;
        }
    }

    /// <summary>
    /// Gibt statische Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _nameFont.Dispose();
        _textFont.Dispose();
        _textPaint.Dispose();
        _separatorPaint.Dispose();
    }
}

/// <summary>
/// Ein Eintrag im Dialog-Backlog.
/// </summary>
public class BacklogEntry
{
    public string SpeakerName { get; set; } = "";
    public string Text { get; set; } = "";
    public SKColor NameColor { get; set; } = UIRenderer.TextPrimary;
}
