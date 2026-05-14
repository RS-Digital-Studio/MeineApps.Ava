namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Schmale Host-Facade für <see cref="HandwerkerImperium.ViewModels.WelcomeFlowViewModel"/>.
/// Kapselt die wenigen MainViewModel-Zugriffe, die der Welcome-Flow noch braucht —
/// der Rest der Logik lebt vollständig im WelcomeFlowViewModel.
/// </summary>
public interface IWelcomeFlowHost
{
    /// <summary>True während Hold-to-Upgrade — unterdrückt verzögerte Story-Dialoge.</summary>
    bool IsHoldingUpgrade { get; }

    /// <summary>Navigiert zum Shop (wird bei Annahme des Starter-Offers aufgerufen).</summary>
    void NavigateToShop();
}
