using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BingXBot.Tests;

// Validation-Tests fuer die DI-Container der App (Client) und des Servers.
//
// Zweck: Verhindert Regressionen wie v1.3.5 IRateLimiter-Bug, wo ein Konstruktor-Param
// von einer konkreten Klasse auf ein Interface umgestellt wurde, aber die DI-Registrierung
// nicht nachgezogen wurde — der App-Start crashte erst im Runtime.
//
// Strategie: Die echte ConfigureServices-Methode aus App.axaml.cs aufrufen (internal gemacht),
// dann BuildServiceProvider(ValidateOnBuild=true) — das prueft BEIM BAU dass alle Services
// konstruierbar sind und alle Konstruktor-Params registriert.
public class DiContainerValidationTests
{
    [Fact]
    public void ClientDiContainer_AlleServicesResolvbar()
    {
        // Arrange: ServiceCollection mit der echten Client-App-Registrierung befuellen.
        var services = new ServiceCollection();
        BingXBot.App.ConfigureServices(services);

        // Act + Assert: BuildServiceProvider mit ValidateOnBuild pruft alle Services auf Resolve-
        // Faehigkeit. Bei fehlender Registrierung eines Konstruktor-Params wird hier geworfen.
        // Scope-Validation zusaetzlich: verhindert dass Singletons versehentlich Scoped-Services halten.
        var act = () => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        act.Should().NotThrow(
            "alle registrierten Services muessen mit den tatsaechlich registrierten Dependencies " +
            "konstruierbar sein — sonst crasht die App beim Startup (siehe v1.3.5 IRateLimiter-Regression).");
    }

    [Fact]
    public void ClientDiContainer_KritischeServicesKonkretResolvbar()
    {
        // Zusaetzlicher Sanity-Check: Ein paar Kern-Services explizit resolven (nicht nur die Validation).
        // Faengt Faelle wo ValidateOnBuild leer laeuft weil ein Service gar nicht registriert ist —
        // hier merken wir das sofort.
        var services = new ServiceCollection();
        BingXBot.App.ConfigureServices(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Pruefe dass der Regression-Pfad (IRateLimiter + BingXPublicClient) tatsaechlich resolvt.
        // Genau das was in v1.3.5 gecrashed ist — wenn dieser Test grun ist, ist die Regression
        // unmoeglich wieder einzufuehren ohne dass dieser Test rot wird.
        provider.GetRequiredService<BingXBot.Exchange.IRateLimiter>().Should().NotBeNull();
        provider.GetRequiredService<BingXBot.Exchange.BingXPublicClient>().Should().NotBeNull();
        provider.GetRequiredService<BingXBot.Core.Interfaces.IPublicMarketDataClient>().Should().NotBeNull();
        provider.GetRequiredService<BingXBot.Trading.LiveTradingManager>().Should().NotBeNull();
        provider.GetRequiredService<BingXBot.Core.Configuration.BotSettings>().Should().NotBeNull();
    }
}
