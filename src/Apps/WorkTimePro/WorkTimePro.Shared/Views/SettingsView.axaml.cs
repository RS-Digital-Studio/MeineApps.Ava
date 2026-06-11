using Avalonia.Controls;
using Avalonia.Labs.Controls;
using WorkTimePro.Graphics;

namespace WorkTimePro.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Zeichnet den Stempel-QR-Code (statischer Inhalt, kein VM-State nötig).
    /// </summary>
    private void OnPaintStampQr(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        QrStampRenderer.Render(canvas, canvas.LocalClipBounds);
    }
}
