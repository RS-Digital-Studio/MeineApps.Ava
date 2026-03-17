using HandwerkerImperium.ViewModels.MiniGames;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Container für alle 10 MiniGame-ViewModels.
/// Reduziert die Constructor-Parameter des MainViewModel.
/// </summary>
public sealed class MiniGameViewModels
{
    public SawingGameViewModel Sawing { get; }
    public PipePuzzleViewModel PipePuzzle { get; }
    public WiringGameViewModel Wiring { get; }
    public PaintingGameViewModel Painting { get; }
    public RoofTilingGameViewModel RoofTiling { get; }
    public BlueprintGameViewModel Blueprint { get; }
    public DesignPuzzleGameViewModel DesignPuzzle { get; }
    public InspectionGameViewModel Inspection { get; }
    public ForgeGameViewModel Forge { get; }
    public InventGameViewModel Invent { get; }

    public MiniGameViewModels(
        SawingGameViewModel sawing,
        PipePuzzleViewModel pipePuzzle,
        WiringGameViewModel wiring,
        PaintingGameViewModel painting,
        RoofTilingGameViewModel roofTiling,
        BlueprintGameViewModel blueprint,
        DesignPuzzleGameViewModel designPuzzle,
        InspectionGameViewModel inspection,
        ForgeGameViewModel forge,
        InventGameViewModel invent)
    {
        Sawing = sawing;
        PipePuzzle = pipePuzzle;
        Wiring = wiring;
        Painting = painting;
        RoofTiling = roofTiling;
        Blueprint = blueprint;
        DesignPuzzle = designPuzzle;
        Inspection = inspection;
        Forge = forge;
        Invent = invent;
    }
}
