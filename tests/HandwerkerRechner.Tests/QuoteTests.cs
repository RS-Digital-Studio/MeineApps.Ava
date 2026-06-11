using System.Text.Json;
using FluentAssertions;
using HandwerkerRechner.Models;
using Xunit;

namespace HandwerkerRechner.Tests;

/// <summary>
/// Tests für die decimal-Geldbeträge in Quote/QuoteItem:
/// kaufmännische Rundung (AwayFromZero) der Summen und JSON-Roundtrip
/// von Altdaten (double-persistiert) nach decimal.
/// </summary>
public class QuoteTests
{
    #region Berechnete Summen (Rundung)

    [Fact]
    public void QuoteItem_Total_RundetKaufmaennischAwayFromZero()
    {
        // 3 × 0,335 € = 1,005 € → kaufmännisch 1,01 € (Banker's Rounding ergäbe 1,00 €)
        var item = new QuoteItem { Quantity = 3, UnitPrice = 0.335m };

        item.Total.Should().Be(1.01m);
    }

    [Fact]
    public void Quote_VatAmount_RundetMidpointAwayFromZero()
    {
        // Subtotal 10,00 € + 15% Marge = 11,50 € netto; 19% MwSt = 2,185 € → 2,19 € (ToEven ergäbe 2,18 €)
        var quote = new Quote
        {
            Items = [new QuoteItem { Quantity = 1, UnitPrice = 10.00m }],
            MarginPercent = 15.0m,
            VatPercent = 19.0m
        };

        quote.SubtotalNet.Should().Be(10.00m);
        quote.MarginAmount.Should().Be(1.50m);
        quote.TotalNet.Should().Be(11.50m);
        quote.VatAmount.Should().Be(2.19m);
        quote.TotalGross.Should().Be(13.69m);
    }

    [Fact]
    public void Quote_Summen_KeineDoubleArtefakte()
    {
        // 0,1 + 0,2 = 0,3 exakt in decimal (in double: 0.30000000000000004)
        var quote = new Quote
        {
            Items =
            [
                new QuoteItem { Quantity = 1, UnitPrice = 0.1m },
                new QuoteItem { Quantity = 1, UnitPrice = 0.2m }
            ],
            MarginPercent = 0m,
            VatPercent = 0m
        };

        quote.SubtotalNet.Should().Be(0.30m);
        quote.TotalGross.Should().Be(0.30m);
    }

    [Fact]
    public void Quote_TotalGross_IstSummeDerGerundetenTeilbetraege()
    {
        var quote = new Quote
        {
            Items = [new QuoteItem { Quantity = 2.5, UnitPrice = 13.33m }],
            MarginPercent = 7.7m,
            VatPercent = 19.0m
        };

        // Endbeträge sind definiert gerundet — Gesamtbrutto = Netto + MwSt (beide bereits 2 Nachkommastellen)
        quote.TotalNet.Should().Be(quote.SubtotalNet + quote.MarginAmount);
        quote.TotalGross.Should().Be(quote.TotalNet + quote.VatAmount);
        decimal.Round(quote.TotalGross, 2).Should().Be(quote.TotalGross);
    }

    [Fact]
    public void Quote_OhnePositionen_AlleSummenNull()
    {
        var quote = new Quote();

        quote.SubtotalNet.Should().Be(0m);
        quote.MarginAmount.Should().Be(0m);
        quote.TotalNet.Should().Be(0m);
        quote.VatAmount.Should().Be(0m);
        quote.TotalGross.Should().Be(0m);
    }

    #endregion

    #region JSON-Roundtrip (Altdaten double → decimal)

    [Fact]
    public void Quote_AltesDoubleJson_LaedtVerlustfreiInDecimal()
    {
        // Altdaten: von der double-Version persistiertes JSON mit Nachkommawerten
        const string oldJson = """
        {
            "Id": "abc-123",
            "QuoteNumber": "A-2025-007",
            "CustomerName": "Max Mustermann",
            "Items": [
                { "Description": "Fliesen", "Unit": "m²", "Quantity": 12.5, "UnitPrice": 25.99, "ItemType": 0 },
                { "Description": "Arbeit", "Unit": "h", "Quantity": 3.25, "UnitPrice": 48.5, "ItemType": 1 }
            ],
            "VatPercent": 19.0,
            "MarginPercent": 12.5,
            "CreatedDate": "2025-11-03T08:15:00.0000000Z",
            "ValidUntil": "2025-12-03T08:15:00.0000000Z",
            "Status": 1
        }
        """;

        var quote = JsonSerializer.Deserialize<Quote>(oldJson);

        quote.Should().NotBeNull();
        quote!.VatPercent.Should().Be(19.0m);
        quote.MarginPercent.Should().Be(12.5m);
        quote.Items.Should().HaveCount(2);
        quote.Items[0].UnitPrice.Should().Be(25.99m);
        quote.Items[0].Quantity.Should().Be(12.5);
        quote.Items[1].UnitPrice.Should().Be(48.5m);

        // 12,5 × 25,99 = 324,875 → 324,88 | 3,25 × 48,50 = 157,625 → 157,63
        quote.Items[0].Total.Should().Be(324.88m);
        quote.Items[1].Total.Should().Be(157.63m);
        quote.SubtotalNet.Should().Be(482.51m);
    }

    [Fact]
    public void Quote_SerialisierenUndLaden_ErhaeltDecimalWerte()
    {
        var original = new Quote
        {
            QuoteNumber = "A-2026-001",
            Items = [new QuoteItem { Description = "Putz", Unit = "m²", Quantity = 7.75, UnitPrice = 11.11m }],
            VatPercent = 7.0m,
            MarginPercent = 3.33m
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Quote>(json);

        restored.Should().NotBeNull();
        restored!.VatPercent.Should().Be(7.0m);
        restored.MarginPercent.Should().Be(3.33m);
        restored.Items[0].UnitPrice.Should().Be(11.11m);
        restored.TotalGross.Should().Be(original.TotalGross);
    }

    #endregion
}
