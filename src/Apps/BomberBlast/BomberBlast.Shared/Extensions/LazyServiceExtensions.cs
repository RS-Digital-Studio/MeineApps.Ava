using Microsoft.Extensions.DependencyInjection;

namespace BomberBlast.Extensions;

/// <summary>
/// Registriert Lazy&lt;T&gt; als auflösbaren Typ im DI-Container.
/// Microsoft.Extensions.DependencyInjection unterstützt Lazy&lt;T&gt; nicht nativ -
/// diese Extension fügt eine generische Factory hinzu, die Lazy&lt;T&gt; auf
/// sp.GetRequiredService&lt;T&gt;() mapped. Löst zirkuläre Abhängigkeiten
/// ohne manuelle SetXxxService()-Verdrahtung.
/// </summary>
public static class LazyServiceExtensions
{
    /// <summary>
    /// Registriert eine offene generische Lazy&lt;T&gt;-Factory im DI-Container.
    /// Danach kann jeder Service Lazy&lt;IXxxService&gt; als Konstruktor-Parameter verwenden.
    /// </summary>
    public static IServiceCollection AddLazyResolution(this IServiceCollection services)
    {
        services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));
        return services;
    }

    /// <summary>
    /// Interne Implementierung: Wraps GetRequiredService in Lazy&lt;T&gt;
    /// </summary>
    private class LazyService<T> : Lazy<T> where T : notnull
    {
        public LazyService(IServiceProvider serviceProvider)
            : base(() => serviceProvider.GetRequiredService<T>())
        {
        }
    }
}
