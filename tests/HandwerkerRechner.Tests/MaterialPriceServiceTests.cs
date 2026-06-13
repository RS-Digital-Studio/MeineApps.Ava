using FluentAssertions;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using Xunit;

namespace HandwerkerRechner.Tests;

/// <summary>
/// Tests für MaterialPriceService: decimal-Preise, nullable CustomPrice
/// (Altdaten-Sentinel -1 → null) und Persistenz-Roundtrip der Overrides.
/// </summary>
public sealed class MaterialPriceServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "HwrTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best Effort */ }
    }

    [Fact]
    public void EffectivePrice_OhneOverride_LiefertDefaultPrice()
    {
        var price = new MaterialPrice { DefaultPrice = 25.0m };

        price.CustomPrice.Should().BeNull();
        price.EffectivePrice.Should().Be(25.0m);
    }

    [Fact]
    public void EffectivePrice_MitOverride_LiefertCustomPrice()
    {
        var price = new MaterialPrice { DefaultPrice = 25.0m, CustomPrice = 19.99m };

        price.EffectivePrice.Should().Be(19.99m);
    }

    [Fact]
    public void LoadCustomPrices_AltesMinusEinsSentinel_WirdZuNullNormalisiert()
    {
        // Altdaten: -1 war früher der "nicht überschrieben"-Sentinel
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "material_prices.json"),
            """{ "tile_standard": -1, "paint_standard": 15.5 }""");

        var service = new MaterialPriceService(_tempDir);

        service.GetPrice("tile_standard")!.CustomPrice.Should().BeNull();
        service.GetPrice("tile_standard")!.EffectivePrice.Should().Be(25.0m); // Default greift
        service.GetPrice("paint_standard")!.CustomPrice.Should().Be(15.5m);
        service.GetPrice("paint_standard")!.EffectivePrice.Should().Be(15.5m);
    }

    [Fact]
    public async Task SetCustomPriceAsync_PersistiertDecimalUndLaedtVerlustfrei()
    {
        var service = new MaterialPriceService(_tempDir);

        await service.SetCustomPriceAsync("grout_standard", 3.33m);

        // Neue Service-Instanz auf demselben Verzeichnis → liest persistierten Override
        var reloaded = new MaterialPriceService(_tempDir);
        reloaded.GetPrice("grout_standard")!.CustomPrice.Should().Be(3.33m);
    }

    [Fact]
    public async Task ResetToDefaultAsync_EntferntOverrideAuchAusPersistenz()
    {
        var service = new MaterialPriceService(_tempDir);
        await service.SetCustomPriceAsync("cable_1_5mm", 2.22m);

        await service.ResetToDefaultAsync("cable_1_5mm");

        service.GetPrice("cable_1_5mm")!.CustomPrice.Should().BeNull();
        var reloaded = new MaterialPriceService(_tempDir);
        reloaded.GetPrice("cable_1_5mm")!.CustomPrice.Should().BeNull();
        reloaded.GetPrice("cable_1_5mm")!.EffectivePrice.Should().Be(1.20m);
    }

    [Fact]
    public async Task ResetAllToDefaultAsync_SetztAlleOverridesZurueck()
    {
        var service = new MaterialPriceService(_tempDir);
        await service.SetCustomPriceAsync("tile_standard", 30m);
        await service.SetCustomPriceAsync("paint_standard", 14m);

        await service.ResetAllToDefaultAsync();

        service.GetAllPrices().Should().OnlyContain(p => p.CustomPrice == null);
    }

    [Fact]
    public void GetAllPrices_LiefertKopieNichtDieGecachteListe()
    {
        var service = new MaterialPriceService(_tempDir);

        var first = service.GetAllPrices();
        first.Clear();

        service.GetAllPrices().Should().NotBeEmpty();
    }
}
