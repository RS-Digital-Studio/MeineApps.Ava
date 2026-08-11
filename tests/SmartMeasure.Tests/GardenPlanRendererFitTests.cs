using SkiaSharp;
using SmartMeasure.Shared.Graphics;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

/// <summary>Tests fuer die stabile Einpassung des GardenPlanRenderers. Vorher rechnete jeder
/// Frame Zentrum und Skalierung neu aus der aktuellen Bounding-Box — ein einzelner neuer
/// Messpunkt verschob damit die ganze Szene, alle bereits gesetzten Punkte wanderten auf dem
/// Schirm. Geprueft wird ueber die veroeffentlichten LastCenterX/LastCenterY/LastScale.</summary>
public class GardenPlanRendererFitTests
{
    private const int CanvasSize = 400;

    private static (GardenPlanRenderer renderer, SKSurface surface) Make()
    {
        var gardenPlan = Substitute.For<IGardenPlanService>();
        var surface = SKSurface.Create(new SKImageInfo(CanvasSize, CanvasSize));
        return (new GardenPlanRenderer(gardenPlan), surface);
    }

    private static void Render(GardenPlanRenderer renderer, SKSurface surface,
        double[] x, double[] y)
    {
        var z = new double[x.Length];
        renderer.Render(surface.Canvas, new SKRect(0, 0, CanvasSize, CanvasSize),
            x, y, z, labels: null, elements: null, mesh: null);
    }

    [Fact]
    public void NeuerPunktInnerhalbDerEinpassung_LaesstZentrumUndSkalierungStehen()
    {
        var (renderer, surface) = Make();
        using (surface)
        {
            // Zwei Punkte spannen 10 m x 10 m auf → Zentrum (5, 5).
            Render(renderer, surface, [0, 10], [0, 10]);
            var centerX = renderer.LastCenterX;
            var centerY = renderer.LastCenterY;
            var scale = renderer.LastScale;

            // Dritter Punkt LIEGT INNERHALB der Box — die Ansicht darf sich nicht ruehren.
            Render(renderer, surface, [0, 10, 2], [0, 10, 8]);

            renderer.LastCenterX.Should().Be(centerX);
            renderer.LastCenterY.Should().Be(centerY);
            renderer.LastScale.Should().Be(scale);
        }
    }

    [Fact]
    public void PunktAusserhalbDerEinpassung_PasstNeuEin()
    {
        var (renderer, surface) = Make();
        using (surface)
        {
            Render(renderer, surface, [0, 10], [0, 10]);
            var centerX = renderer.LastCenterX;

            // Punkt weit ausserhalb: neu einpassen ist richtig, sonst waere er unsichtbar.
            Render(renderer, surface, [0, 10, 100], [0, 10, 0]);

            renderer.LastCenterX.Should().NotBe(centerX);
        }
    }

    [Fact]
    public void ResetFit_PasstBeimNaechstenRenderNeuEin()
    {
        var (renderer, surface) = Make();
        using (surface)
        {
            Render(renderer, surface, [0, 10], [0, 10]);
            var scale = renderer.LastScale;

            // Datensatz-Wechsel: ein kleineres Grundstueck soll den Ausschnitt wieder fuellen.
            renderer.ResetFit();
            Render(renderer, surface, [0, 2], [0, 2]);

            renderer.LastScale.Should().BeGreaterThan(scale);
            renderer.LastCenterX.Should().Be(1.0);
        }
    }

    [Fact]
    public void OhneResetFit_BleibtDerAusschnittNachDatenwechselStehen()
    {
        var (renderer, surface) = Make();
        using (surface)
        {
            Render(renderer, surface, [0, 10], [0, 10]);
            var scale = renderer.LastScale;
            var centerX = renderer.LastCenterX;

            // Kleinerer Datensatz, aber innerhalb der bestehenden Einpassung: ohne ResetFit
            // bleibt der Ausschnitt bewusst stehen (kein Zoom-Sprung bei Punkt-Loeschung).
            Render(renderer, surface, [0, 2], [0, 2]);

            renderer.LastScale.Should().Be(scale);
            renderer.LastCenterX.Should().Be(centerX);
        }
    }
}
