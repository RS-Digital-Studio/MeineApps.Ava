namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Schmale Host-Facade für <see cref="GameStartupCoordinator"/>. Kapselt die wenigen
/// MainViewModel-Zugriffe, die die Startup-Sequenz noch braucht — Loading-State und
/// die EconomyVM-Refresh-Hooks, die nicht als eigene DI-Singletons verfügbar sind.
/// </summary>
public interface IStartupHost
{
    /// <summary>Sichtbarkeit des Loading-Overlays (MainViewModel-State).</summary>
    bool IsLoading { get; set; }

    /// <summary>Lädt alle UI-States aus dem GameState neu (Workshops, Orders, Banner, ...).</summary>
    void RefreshFromState();

    /// <summary>Aktualisiert die Auftrags-Anzeige.</summary>
    void RefreshOrders();
}
