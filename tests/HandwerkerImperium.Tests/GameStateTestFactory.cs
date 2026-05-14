using HandwerkerImperium.Models;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Test-Hilfen fuer Services, die <see cref="HandwerkerImperium.Services.Interfaces.IGameStateService"/>
/// konsumieren. Liefert die ECHTE <see cref="GameStateService"/>-Implementierung statt eines Mocks.
///
/// Hintergrund (v2.1.1, Audit C-C01..C-C03 / B-C02 / B-M03 / B-M05): Die State-mutierenden
/// Services kapseln ihre Mutationen jetzt in <c>IGameStateService.ExecuteWithLock(...)</c>.
/// Ein NSubstitute-Mock fuehrt die uebergebene Action/Func nicht aus — Tests wuerden
/// faelschlich "keine Mutation" sehen. <see cref="GameStateService"/> ist ein reiner
/// In-Memory-Container ohne externe Dependencies (keine DB, kein Netzwerk, kein Dateisystem),
/// daher ist die echte Instanz hier die korrekte und robusteste Wahl. Die Default-Interface-Member
/// (Prestige/Settings/Statistics/...) leiten automatisch korrekt an den State weiter.
/// </summary>
internal static class GameStateTestFactory
{
    /// <summary>Erstellt einen echten, initialisierten <see cref="GameStateService"/> um den uebergebenen State.</summary>
    public static GameStateService Create(GameState state)
    {
        var service = new GameStateService();
        service.Initialize(state);
        return service;
    }

    /// <summary>Erstellt einen echten <see cref="GameStateService"/> mit frischem Standard-State.</summary>
    public static GameStateService Create() => Create(GameState.CreateNew());
}
