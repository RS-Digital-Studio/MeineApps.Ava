using FluentAssertions;
using MeineApps.Core.Ava.Localization;
using NSubstitute;
using RechnerPlus.ViewModels;
using Xunit;

namespace RechnerPlus.Tests;

/// <summary>
/// Tests für ConverterViewModel: Einheiten-Konvertierung, Kategorie-Wechsel,
/// Swap-Funktion, ungültige Eingaben, Temperature-Offset-Konvertierung.
/// </summary>
public class ConverterViewModelTests
{
    private static ConverterViewModel ErstelleViewModel()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString(Arg.Any<string>()).Returns(x => (string)x[0]);
        return new ConverterViewModel(localization);
    }

    #region Initialisierung

    [Fact]
    public void Konstruktor_KategorienWerdenGeladen()
    {
        var sut = ErstelleViewModel();
        sut.Categories.Should().NotBeEmpty();
        sut.Categories.Should().HaveCount(11); // Length, Mass, Temp, Time, Vol, Area, Speed, Data, Energy, Pressure, Angle
    }

    [Fact]
    public void Konstruktor_ErsteKategorieWirdGewaehlt()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory.Should().NotBeNull();
    }

    [Fact]
    public void Konstruktor_EinheitenFuerErsteKategorieGeladen()
    {
        var sut = ErstelleViewModel();
        sut.AvailableUnits.Should().NotBeEmpty();
        sut.FromUnit.Should().NotBeNull();
        sut.ToUnit.Should().NotBeNull();
    }

    #endregion

    #region Laengen-Konvertierung

    [Fact]
    public void Convert_1MeterInKilometer_GibtEinTausendstel()
    {
        var sut = ErstelleViewModel();
        // Erste Kategorie = Length (m → km)
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Length);

        // Meter → Kilometer
        var meter = sut.AvailableUnits.First(u => u.Symbol == "m");
        var kilometer = sut.AvailableUnits.First(u => u.Symbol == "km");
        sut.FromUnit = meter;
        sut.ToUnit = kilometer;
        sut.InputValue = "1000";

        // 1000m = 1km
        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void Convert_1MileInMeter_GibtKorrektenWert()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Length);

        var miles = sut.AvailableUnits.First(u => u.Symbol == "mi");
        var meter = sut.AvailableUnits.First(u => u.Symbol == "m");
        sut.FromUnit = miles;
        sut.ToUnit = meter;
        sut.InputValue = "1";

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(1609.344, 0.001);
    }

    #endregion

    #region Temperatur-Konvertierung (Offset-basiert)

    [Fact]
    public void Convert_0GradCelsiusInFahrenheit_Gibt32()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Temperature);

        var celsius = sut.AvailableUnits.First(u => u.Symbol == "°C");
        var fahrenheit = sut.AvailableUnits.First(u => u.Symbol == "°F");
        sut.FromUnit = celsius;
        sut.ToUnit = fahrenheit;
        sut.InputValue = "0";

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(32, 0.001);
    }

    [Fact]
    public void Convert_100GradCelsiusInFahrenheit_Gibt212()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Temperature);

        var celsius = sut.AvailableUnits.First(u => u.Symbol == "°C");
        var fahrenheit = sut.AvailableUnits.First(u => u.Symbol == "°F");
        sut.FromUnit = celsius;
        sut.ToUnit = fahrenheit;
        sut.InputValue = "100";

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(212, 0.001);
    }

    [Fact]
    public void Convert_0GradCelsiusInKelvin_Gibt27315()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Temperature);

        var celsius = sut.AvailableUnits.First(u => u.Symbol == "°C");
        var kelvin = sut.AvailableUnits.First(u => u.Symbol == "K");
        sut.FromUnit = celsius;
        sut.ToUnit = kelvin;
        sut.InputValue = "0";

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(273.15, 0.001);
    }

    [Fact]
    public void Convert_GleicheEinheit_GibtSelbenWertZurueck()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Temperature);

        var celsius = sut.AvailableUnits.First(u => u.Symbol == "°C");
        sut.FromUnit = celsius;
        sut.ToUnit = celsius; // gleiche Einheit
        sut.InputValue = "42";

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(42, 0.001);
    }

    #endregion

    #region Massen-Konvertierung

    [Fact]
    public void Convert_1KilogrammInGramm_Gibt1000()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Mass);

        var kg = sut.AvailableUnits.First(u => u.Symbol == "kg");
        var g = sut.AvailableUnits.First(u => u.Symbol == "g");
        sut.FromUnit = kg;
        sut.ToUnit = g;
        sut.InputValue = "1";

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(1000, 0.001);
    }

    #endregion

    #region Swap-Funktion

    [Fact]
    public void SwapUnits_TauschtFromUndToEinheit()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Length);

        var meter = sut.AvailableUnits.First(u => u.Symbol == "m");
        var km = sut.AvailableUnits.First(u => u.Symbol == "km");
        sut.FromUnit = meter;
        sut.ToUnit = km;

        sut.SwapUnitsCommand.Execute(null);

        sut.FromUnit!.Symbol.Should().Be("km");
        sut.ToUnit!.Symbol.Should().Be("m");
    }

    [Fact]
    public void SwapUnits_BerechnetNeuNachTausch()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Length);

        var meter = sut.AvailableUnits.First(u => u.Symbol == "m");
        var km = sut.AvailableUnits.First(u => u.Symbol == "km");
        sut.FromUnit = meter;
        sut.ToUnit = km;
        sut.InputValue = "1000"; // 1000m = 1km

        sut.SwapUnitsCommand.Execute(null); // jetzt km → m
        sut.InputValue = "1"; // 1km

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(1000, 0.001);
    }

    #endregion

    #region Eingabe-Validierung

    [Fact]
    public void Convert_LeereEingabe_OutputLeer()
    {
        var sut = ErstelleViewModel();
        sut.InputValue = "";
        sut.OutputValue.Should().BeEmpty();
    }

    [Fact]
    public void Convert_NurLeerzeichen_OutputLeer()
    {
        var sut = ErstelleViewModel();
        sut.InputValue = "   ";
        sut.OutputValue.Should().BeEmpty();
    }

    [Fact]
    public void Convert_UngueltigeZahl_ZeigtFehlertext()
    {
        var sut = ErstelleViewModel();
        sut.InputValue = "abc";
        // OutputValue soll Fehler-Lokalisierungskey sein (InvalidInputText)
        sut.OutputValue.Should().Be(sut.InvalidInputText);
    }

    [Fact]
    public void Convert_KommaAlsDezimatrenner_WirdAkzeptiert()
    {
        var sut = ErstelleViewModel();
        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Length);

        var meter = sut.AvailableUnits.First(u => u.Symbol == "m");
        var cm = sut.AvailableUnits.First(u => u.Symbol == "cm");
        sut.FromUnit = meter;
        sut.ToUnit = cm;
        sut.InputValue = "1,5"; // Komma als EU-Dezimaltrenner

        double.TryParse(sut.OutputValue, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var ausgabe).Should().BeTrue();
        ausgabe.Should().BeApproximately(150, 0.001);
    }

    #endregion

    #region Kategorie-Wechsel

    [Fact]
    public void KategorieWechsel_LaedsNeueEinheiten()
    {
        var sut = ErstelleViewModel();
        var laengenkategorien = sut.AvailableUnits.ToList();

        sut.SelectedCategory = sut.Categories.First(c => c.Category == UnitCategory.Mass);
        var masseEinheiten = sut.AvailableUnits.ToList();

        masseEinheiten.Should().NotBeEquivalentTo(laengenkategorien);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_KeineException()
    {
        var sut = ErstelleViewModel();
        var action = () => sut.Dispose();
        action.Should().NotThrow();
    }

    #endregion
}
