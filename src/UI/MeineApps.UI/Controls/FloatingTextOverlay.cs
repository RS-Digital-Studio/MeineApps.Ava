using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace MeineApps.UI.Controls;

/// <summary>
/// Transparenter Canvas-Overlay der animierte Texte nach oben fliegen laesst.
/// CubicEaseOut Bewegung (80px nach oben), Opacity-Fadeout ab 30% Fortschritt.
/// </summary>
public class FloatingTextOverlay : Canvas
{
    public FloatingTextOverlay()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    /// <summary>
    /// Zeigt einen animierten Text der nach oben fliegt und verblasst.
    /// </summary>
    public void ShowFloatingText(string text, double x, double y, Color color, double fontSize = 16)
    {
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
        var duration = 1.5;

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
