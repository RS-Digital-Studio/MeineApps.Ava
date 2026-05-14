using System.Threading.Tasks;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Orchestriert die Spielstart-Sequenz: Spielstand laden, Cloud-Save-Abgleich,
/// Sprach-Sync, Order-/Mission-Initialisierung, Welcome-Flow, GameLoop-Start sowie
/// verzögerte WhatsNew- und Analytics-Consent-Dialoge.
/// Aus MainViewModel.Init.cs extrahiert — wird von der LoadingPipeline über
/// MainViewModel.InitializeAsync() angestossen.
/// </summary>
public interface IGameStartupCoordinator
{
    /// <summary>Verbindet die schmale Host-Bruecke (MainViewModel) — einmalig im MainViewModel-Ctor.</summary>
    void AttachHost(IStartupHost host);

    /// <summary>Führt die komplette Startup-Sequenz aus. Idempotent gegen Doppel-Initialisierung.</summary>
    Task RunAsync();
}
