using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace MeineApps.UI.Controls;

/// <summary>
/// Transparenter Canvas-Overlay der animierte Texte nach oben fliegen laesst.
/// CubicEaseOut Bewegung (80px nach oben), Opacity-Fadeout ab 30% Fortschritt.
/// F-24: 3-Spalten Slot-Round-Robin gegen Stack-Overlap bei vielen parallelen Events.
/// </summary>
public class FloatingTextOverlay : Canvas
{
    // F-24: Slot-Pool — bei x==0 verteilen sich Texte auf 3 horizontale Slots.
    private int _slotIndex;

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

        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(color),
            Opacity = 1.0,
            IsHitTestVisible = false
        };

        // Positionieren
        SetLeft(textBlock, x);
        SetTop(textBlock, y);
        Children.Add(textBlock);

        // Animation: Nach oben fliegen + ausblenden via DispatcherTimer (~60fps)
        var startY = y;
        var targetY = y - 80;
        var startTime = DateTime.UtcNow;
        var duration = 1.2;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            var progress = Math.Min(elapsed / duration, 1.0);

            // CubicEaseOut: schnell starten, sanft auslaufen
            var eased = 1.0 - Math.Pow(1.0 - progress, 3);
            var currentY = startY + (targetY - startY) * eased;
            SetTop(textBlock, currentY);

            // Opacity: 100% bis 30%, dann linear auf 0%
            var opacity = progress < 0.3 ? 1.0 : 1.0 - (progress - 0.3) / 0.7;
            textBlock.Opacity = Math.Max(0, opacity);

            if (progress >= 1.0)
            {
                timer.Stop();
                Children.Remove(textBlock);
            }
        };
        timer.Start();
    }
}
