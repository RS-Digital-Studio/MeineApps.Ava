using BomberBlast.Graphics;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für EmptyStateRenderer (Phase 28c — PR3).
/// </summary>
public class EmptyStateRendererTests
{
    [Fact]
    public void Draw_AlleTypen_RendernOhneCrash()
    {
        var bounds = new SKRect(0, 0, 200, 200);
        foreach (EmptyStateRenderer.EmptyStateType type in Enum.GetValues<EmptyStateRenderer.EmptyStateType>())
        {
            using var bmp = new SKBitmap(200, 200);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Black);
            var act = () => EmptyStateRenderer.Draw(canvas, bounds, type, time: 0.5f);
            act.Should().NotThrow($"EmptyState {type} darf nicht crashen");
        }
    }

    [Fact]
    public void Draw_RendertSichtbarePixel()
    {
        var bounds = new SKRect(0, 0, 200, 200);
        using var bmp = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        EmptyStateRenderer.Draw(canvas, bounds, EmptyStateRenderer.EmptyStateType.Shop);

        int nonBlack = 0;
        for (int y = 50; y < 150; y++)
            for (int x = 50; x < 150; x++)
                if (bmp.GetPixel(x, y) != SKColors.Black) nonBlack++;

        nonBlack.Should().BeGreaterThan(100, "EmptyState muss eine sichtbare Illustration zeichnen");
    }

    [Fact]
    public void Draw_VerschiedeneZeiten_KeinNaN()
    {
        var bounds = new SKRect(0, 0, 100, 100);
        for (float t = 0; t <= 5f; t += 0.5f)
        {
            using var bmp = new SKBitmap(100, 100);
            using var canvas = new SKCanvas(bmp);
            var act = () => EmptyStateRenderer.Draw(canvas, bounds, EmptyStateRenderer.EmptyStateType.Cards, time: t);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void Draw_MitCustomFarben_RespektiertParameter()
    {
        var bounds = new SKRect(0, 0, 100, 100);
        using var bmp = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bmp);
        var act = () => EmptyStateRenderer.Draw(canvas, bounds, EmptyStateRenderer.EmptyStateType.Generic,
            primary: SKColors.Red, secondary: SKColors.Blue);
        act.Should().NotThrow();
    }
}
