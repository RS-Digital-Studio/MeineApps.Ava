using BomberBlast.Graphics;
using BomberBlast.Models.Cosmetics;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für FrameRenderer (Phase 29c).
/// Validiert dass alle 33 FrameStyle-Werte ohne Crash gerendert werden können.
/// </summary>
public class FrameRendererTests
{
    [Fact]
    public void DrawFrame_AlleStyles_RenderToBitmap_OhneCrash()
    {
        var bounds = new SKRect(20, 20, 100, 100);

        foreach (var def in FrameDefinitions.All)
        {
            using var bmp = new SKBitmap(120, 120);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Transparent);

            var act = () => FrameRenderer.DrawFrame(canvas, bounds, def, time: 0.5f);
            act.Should().NotThrow($"Frame-Style {def.Style} ({def.Id}) darf nicht crashen");
        }
    }

    [Fact]
    public void DrawFrame_VerschiedeneZeitpunkte_RenderToBitmap_OhneCrash()
    {
        var bounds = new SKRect(0, 0, 80, 80);
        var def = FrameDefinitions.Crown;

        for (float t = 0; t <= 5f; t += 0.5f)
        {
            using var bmp = new SKBitmap(80, 80);
            using var canvas = new SKCanvas(bmp);
            var act = () => FrameRenderer.DrawFrame(canvas, bounds, def, time: t);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void DrawFrame_RendertSichtbarePixel()
    {
        // Sanity-Check: Mindestens ein Pixel des Frames sollte rendern (Border zeichnen)
        var bounds = new SKRect(10, 10, 70, 70);
        using var bmp = new SKBitmap(80, 80);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        FrameRenderer.DrawFrame(canvas, bounds, FrameDefinitions.Simple);

        // Zähle nicht-schwarze Pixel
        int nonBlack = 0;
        for (int y = 0; y < 80; y++)
            for (int x = 0; x < 80; x++)
                if (bmp.GetPixel(x, y) != SKColors.Black) nonBlack++;

        nonBlack.Should().BeGreaterThan(0, "FrameRenderer muss zumindest die Border zeichnen");
    }
}
