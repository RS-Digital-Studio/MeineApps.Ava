using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IContentFacade"/> — Service-Container für die fünf
/// spielbaren Content-Pipelines. Singleton, kein State.
/// </summary>
public sealed class ContentFacade : IContentFacade
{
    public IResearchService Research { get; }
    public ICraftingService Crafting { get; }
    public IEquipmentService Equipment { get; }
    public IBuildingService Building { get; }
    public IManagerService Manager { get; }

    public ContentFacade(
        IResearchService research,
        ICraftingService crafting,
        IEquipmentService equipment,
        IBuildingService building,
        IManagerService manager)
    {
        Research = research;
        Crafting = crafting;
        Equipment = equipment;
        Building = building;
        Manager = manager;
    }
}
