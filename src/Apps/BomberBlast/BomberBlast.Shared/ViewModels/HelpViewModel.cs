using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel for the help/tutorial page.
/// Provides static help content, tutorial replay and navigation back.
/// </summary>
public partial class HelpViewModel : ObservableObject
{
    private readonly ITutorialService _tutorialService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event to request navigation. Parameter is the route string.
    /// </summary>
    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public HelpViewModel(ITutorialService tutorialService)
    {
        _tutorialService = tutorialService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tutorial zurücksetzen und Level 1 mit Tutorial starten
    /// </summary>
    [RelayCommand]
    private void ReplayTutorial()
    {
        _tutorialService.Reset();
        NavigationRequested?.Invoke("Game?mode=story&level=1");
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }
}
