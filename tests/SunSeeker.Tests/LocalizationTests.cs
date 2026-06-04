using System.Collections;
using System.Globalization;
using System.Resources;
using FluentAssertions;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using NSubstitute;
using SunSeeker.Shared.Services;
using Xunit;

namespace SunSeeker.Tests;

/// <summary>
/// Verifiziert, dass die AppStrings-RESX korrekt eingebettet sind und in allen 6 Sprachen
/// vollstaendig uebersetzt wurden (kein Key faellt auf den rohen Key-Namen zurueck).
/// </summary>
public class LocalizationTests
{
    private static readonly string[] Languages = ["de", "en", "es", "fr", "it", "pt"];

    private static ResourceManager Manager() =>
        new("SunSeeker.Shared.Resources.Strings.AppStrings", typeof(SolarPositionService).Assembly);

    private static LocalizationService MakeService()
    {
        var prefs = Substitute.For<IPreferencesService>();
        return new LocalizationService(Manager(), prefs);
    }

    private static IReadOnlyList<string> AllKeys()
    {
        var set = Manager().GetResourceSet(CultureInfo.InvariantCulture, true, true)!;
        return set.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToList();
    }

    [Theory]
    [InlineData("de", "TabAlign", "Ausrichten")]
    [InlineData("en", "TabAlign", "Align")]
    [InlineData("es", "TabAlign", "Orientar")]
    [InlineData("fr", "TabAlign", "Orienter")]
    [InlineData("it", "TabAlign", "Orienta")]
    [InlineData("pt", "TabAlign", "Alinhar")]
    public void GetString_LiefertUebersetzung(string lang, string key, string expected)
    {
        var svc = MakeService();
        svc.SetLanguage(lang);
        svc.GetString(key).Should().Be(expected);
    }

    [Fact]
    public void EchteUmlaute_KeineAsciiErsatzformen()
    {
        // Deutscher Schluessel mit Umlaut muss echte Umlaute enthalten (Robert-Regel).
        var svc = MakeService();
        svc.SetLanguage("de");
        svc.GetString("LabelElevation").Should().Contain("ö"); // "Höhe (Elevation)"
        svc.GetString("LabelSolarNoon").Should().Be("Höchststand");
    }

    [Fact]
    public void AlleSchluessel_InAllenSprachen_Uebersetzt()
    {
        var keys = AllKeys();
        keys.Should().NotBeEmpty("die RESX muessen eingebettet sein");

        var svc = MakeService();
        foreach (var lang in Languages)
        {
            svc.SetLanguage(lang);
            foreach (var key in keys)
            {
                svc.GetString(key).Should().NotBe(key,
                    $"Schluessel '{key}' fehlt/leer in Sprache '{lang}'");
            }
        }
    }

    [Fact]
    public void Platzhalter_BleibenInAllenSprachen_Erhalten()
    {
        // Format-Strings muessen ihren {0}-Platzhalter ueber alle Sprachen behalten.
        var svc = MakeService();
        foreach (var lang in Languages)
        {
            svc.SetLanguage(lang);
            svc.GetString("GuidanceTurnWest").Should().Contain("{0}", $"Sprache '{lang}'");
            svc.GetString("BifacialGain").Should().Contain("{0}").And.Contain("{1}");
        }
    }
}
