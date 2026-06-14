using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace MeineApps.UI.Controls;

/// <summary>
/// Transparenter Canvas-Overlay der animierte Texte nach oben fliegen laesst.
/// CubicEaseOut Bewegung (80px nach oben), Opacity-Fadeout ab 30% Fortschritt.
/// F-24: 3-Spalten Slot-Round-Robin gegen Stack-Overlap bei vielen parallelen Events.
///
/// <para>Akku/GC: EIN geteilter 60fps-Timer treibt alle gleichzeitig aktiven Texte (statt eines
/// DispatcherTimer pro Aufruf), Brushes werden pro Farbe gecacht und TextBlocks aus einem Pool
/// wiederverwendet — wichtig bei Bursts (z.B. HandwerkerImperium AutoCraft+AutoCollect+AutoAccept).
/// Der Timer stoppt sich selbst, sobald keine Animation mehr laeuft.</para>
/// </summary>
public class FloatingTextOverlay : Canvas
{
    private const double DurationSeconds = 1.2;
    private const double FlyDistance = 80.0;
    private const double FrameSeconds = 0.016; // ~60fps
    private const int PoolCap = 16;

    // F-24: Slot-Pool — bei x==0 verteilen sich Texte auf 3 horizontale Slots.
    private int _slotIndex;

    // Ein geteilter Timer fuer ALLE aktiven Texte.
    private DispatcherTimer? _timer;
    private readonly List<FloatingItem> _active = [];
    private readonly Stack<TextBlock> _pool = new();
    private readonly Dictionary<uint, SolidColorBrush> _brushCache = [];

    private sealed class FloatingItem
    {
        public required TextBlock Block;
        public double StartY;
        public double Elapsed;
    }

    public FloatingTextOverlay()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    /// <summary>
    /// Zeigt einen animierten Text der nach oben fliegt und verblasst.
    /// F-24: Wenn x == 0 (Default-Center), wird der Text automatisch in einen von drei
    /// Slot-Spalten round-robin verteilt — verhindert Overlap bei AutoCraft+AutoCollect+
    /// AutoAccept-Bursts.
    /// </summary>
    public void ShowFloatingText(string text, double x, double y, Color color, double fontSize = 16)
    {
        // F-24: Slot-Position berechnen wenn keine explizite x gesetzt wurde (x near 0).
        if (Math.Abs(x) < 1.0 && Bounds.Width > 0)
        {
            // 3 Slots auf ca. 25%/50%/75% der Breite.
            var slotFraction = (_slotIndex % 3) switch { 0 => 0.25, 1 => 0.5, _ => 0.75 };
            x = Bounds.Width * slotFraction;
            _slotIndex++;
        }

        var textBlock = RentTextBlock();
        textBlock.Text = text;
        textBlock.FontSize = fontSize;
        textBlock.Foreground = GetBrush(color);
        textBlock.Opacity = 1.0;

        // Positionieren
        SetLeft(textBlock, x);
        SetTop(textBlock, y);
        Children.Add(textBlock);

        _active.Add(new FloatingItem { Block = textBlock, StartY = y, Elapsed = 0 });
        EnsureTimer();
    }

    private void EnsureTimer()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var item = _active[i];
            item.Elapsed += FrameSeconds;
            var progress = Math.Min(item.Elapsed / DurationSeconds, 1.0);

            // CubicEaseOut: schnell starten, sanft auslaufen
            var eased = 1.0 - Math.Pow(1.0 - progress, 3);
            SetTop(item.Block, item.StartY - FlyDistance * eased);

            // Opacity: 100% bis 30%, dann linear auf 0%
            var opacity = progress < 0.3 ? 1.0 : 1.0 - (progress - 0.3) / 0.7;
            item.Block.Opacity = Math.Max(0, opacity);

            if (progress >= 1.0)
            {
                Children.Remove(item.Block);
                ReturnTextBlock(item.Block);
                _active.RemoveAt(i);
            }
        }

        if (_active.Count == 0)
            StopTimer();
    }

    private void StopTimer()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private TextBlock RentTextBlock()
    {
        if (_pool.Count > 0) return _pool.Pop();
        return new TextBlock
        {
            FontWeight = FontWeight.Bold,
            IsHitTestVisible = false
        };
    }

    private void ReturnTextBlock(TextBlock tb)
    {
        tb.Text = null;
        if (_pool.Count < PoolCap) _pool.Push(tb);
    }

    private SolidColorBrush GetBrush(Color color)
    {
        var key = color.ToUInt32();
        if (_brushCache.TryGetValue(key, out var brush)) return brush;
        brush = new SolidColorBrush(color);
        _brushCache[key] = brush;
        return brush;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Timer stoppen und Zustand freigeben — der DispatcherTimer ist nicht an den Visual-Tree
        // gebunden und liefe sonst nach dem Entfernen des Overlays weiter.
        StopTimer();
        _active.Clear();
        Children.Clear();
        _pool.Clear();
    }
}
