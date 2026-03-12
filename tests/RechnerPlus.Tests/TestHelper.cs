using MeineApps.CalcLib;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using NSubstitute;
using RechnerPlus.ViewModels;

namespace RechnerPlus.Tests;

/// <summary>
/// Hilfsmethoden für das Erstellen von Test-Objekten mit gemockten Abhängigkeiten.
/// </summary>
internal static class TestHelper
{
    /// <summary>
    /// Erstellt einen ILocalizationService-Mock, der den übergebenen Key zurückgibt.
    /// </summary>
    public static ILocalizationService ErstelleLocalizationMock()
    {
        var mock = Substitute.For<ILocalizationService>();
        // GetString gibt immer den Key zurück (einfachstes Fallback-Verhalten)
        mock.GetString(Arg.Any<string>()).Returns(x => (string)x[0]);
        return mock;
    }

    /// <summary>
    /// Erstellt einen IPreferencesService-Mock mit sinnvollen Standardwerten.
    /// </summary>
    public static IPreferencesService ErstellePreferencesMock()
    {
        var mock = Substitute.For<IPreferencesService>();
        // Standardwerte: US-Format (0), keine Dezimalstellen (-1), Basic Mode (0)
        mock.Get(Arg.Any<string>(), Arg.Any<int>()).Returns(x => (int)x[1]);
        mock.Get(Arg.Any<string>(), Arg.Any<double>()).Returns(x => (double)x[1]);
        mock.Get(Arg.Any<string>(), Arg.Any<bool>()).Returns(x => (bool)x[1]);
        mock.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(x => (string)x[1]);
        return mock;
    }

    /// <summary>
    /// Erstellt ein vollständig konfiguriertes CalculatorViewModel für Tests.
    /// </summary>
    public static CalculatorViewModel ErstelleCalculatorViewModel(
        ILocalizationService? localization = null,
        IPreferencesService? preferences = null,
        IHapticService? haptic = null,
        IHistoryService? historyService = null)
    {
        var engine = new CalculatorEngine();
        var parser = new ExpressionParser(engine);
        localization ??= ErstelleLocalizationMock();
        preferences ??= ErstellePreferencesMock();
        haptic ??= new NoOpHapticService();
        historyService ??= new HistoryService();

        return new CalculatorViewModel(engine, parser, localization, historyService, preferences, haptic);
    }
}
