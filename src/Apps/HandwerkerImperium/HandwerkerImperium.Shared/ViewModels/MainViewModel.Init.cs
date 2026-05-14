using System.Threading.Tasks;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Spielstart. Die komplette Initialisierungs-Sequenz (Spielstand laden,
// Cloud-Save, Sprach-Sync, Order-/Mission-Init, Welcome-Flow, GameLoop-Start, verzoegerte
// Dialoge) liegt im GameStartupCoordinator — hier nur der Forwarder fuer die LoadingPipeline.
public sealed partial class MainViewModel
{
    /// <summary>
    /// Wird von der HandwerkerImperiumLoadingPipeline aufgerufen. Delegiert die komplette
    /// Startup-Sequenz an den <see cref="Services.Interfaces.IGameStartupCoordinator"/>.
    /// </summary>
    public Task InitializeAsync() => _startupCoordinator.RunAsync();
}
