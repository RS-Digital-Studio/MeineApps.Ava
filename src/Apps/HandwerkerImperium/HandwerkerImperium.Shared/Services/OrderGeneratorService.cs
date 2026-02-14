using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Generates new orders/contracts for the player.
/// </summary>
public class OrderGeneratorService : IOrderGeneratorService
{
    private readonly IGameStateService _gameStateService;
    private readonly IResearchService? _researchService;

    // Order templates per workshop type
    private static readonly Dictionary<WorkshopType, List<OrderTemplate>> _templates = new()
    {
        [WorkshopType.Carpenter] =
        [
            new("order_shelf", "Build a Shelf", MiniGameType.Sawing),
            new("order_cabinet", "Build a Cabinet", MiniGameType.Sawing, MiniGameType.Planing),
            new("order_table", "Build a Table", MiniGameType.Sawing, MiniGameType.Planing, MiniGameType.Sawing),
            new("order_deck", "Build a Deck", MiniGameType.Measuring, MiniGameType.Sawing, MiniGameType.Sawing),
            new("order_shed", "Build a Garden Shed", MiniGameType.Sawing, MiniGameType.Sawing, MiniGameType.Sawing)
        ],
        [WorkshopType.Plumber] =
        [
            new("order_faucet", "Replace Faucet", MiniGameType.PipePuzzle),
            new("order_toilet", "Install Toilet", MiniGameType.PipePuzzle, MiniGameType.PipePuzzle),
            new("order_shower", "Install Shower", MiniGameType.PipePuzzle, MiniGameType.PipePuzzle),
            new("order_bathroom", "Renovate Bathroom", MiniGameType.PipePuzzle, MiniGameType.PipePuzzle, MiniGameType.PipePuzzle)
        ],
        [WorkshopType.Electrician] =
        [
            new("order_outlet", "Install Outlet", MiniGameType.WiringGame),
            new("order_light", "Install Light Fixture", MiniGameType.WiringGame),
            new("order_panel", "Upgrade Electrical Panel", MiniGameType.WiringGame, MiniGameType.WiringGame),
            new("order_smart_home", "Smart Home Setup", MiniGameType.WiringGame, MiniGameType.WiringGame, MiniGameType.WiringGame)
        ],
        [WorkshopType.Painter] =
        [
            new("order_room", "Paint a Room", MiniGameType.PaintingGame),
            new("order_exterior", "Paint Exterior", MiniGameType.PaintingGame, MiniGameType.PaintingGame),
            new("order_house", "Paint Entire House", MiniGameType.PaintingGame, MiniGameType.PaintingGame, MiniGameType.PaintingGame)
        ],
        [WorkshopType.Roofer] =
        [
            new("order_repair_roof", "Repair Roof Section", MiniGameType.RoofTiling),
            new("order_new_roof", "Install New Roof", MiniGameType.RoofTiling, MiniGameType.RoofTiling),
            new("order_roof_complete", "Complete Roof Replacement", MiniGameType.RoofTiling, MiniGameType.TileLaying, MiniGameType.RoofTiling)
        ],
        [WorkshopType.Contractor] =
        [
            new("order_renovation", "Home Renovation", MiniGameType.Blueprint, MiniGameType.Sawing),
            new("order_addition", "Build Addition", MiniGameType.Blueprint, MiniGameType.Sawing, MiniGameType.WiringGame),
            new("order_multi_unit", "Multi-Unit Project", MiniGameType.Blueprint, MiniGameType.Blueprint, MiniGameType.PipePuzzle, MiniGameType.WiringGame)
        ],
        [WorkshopType.Architect] =
        [
            new("order_blueprint", "Design Blueprint", MiniGameType.DesignPuzzle),
            new("order_floor_plan", "Create Floor Plan", MiniGameType.DesignPuzzle, MiniGameType.DesignPuzzle),
            new("order_full_design", "Complete Building Design", MiniGameType.DesignPuzzle, MiniGameType.Blueprint, MiniGameType.DesignPuzzle)
        ],
        [WorkshopType.GeneralContractor] =
        [
            new("order_house_build", "Build House", MiniGameType.Inspection, MiniGameType.Sawing, MiniGameType.PipePuzzle),
            new("order_commercial", "Commercial Build", MiniGameType.Inspection, MiniGameType.Blueprint, MiniGameType.WiringGame),
            new("order_luxury_villa", "Luxury Villa Project", MiniGameType.Inspection, MiniGameType.Inspection, MiniGameType.RoofTiling, MiniGameType.DesignPuzzle)
        ]
    };

    public OrderGeneratorService(IGameStateService gameStateService, IResearchService? researchService = null)
    {
        _gameStateService = gameStateService;
        _researchService = researchService;
    }

    public Order GenerateOrder(WorkshopType workshopType, int workshopLevel)
    {
        var templates = _templates.GetValueOrDefault(workshopType, _templates[WorkshopType.Carpenter]);

        // Select a template based on level (higher levels get harder orders)
        int maxTemplateIndex = Math.Min(templates.Count - 1, (workshopLevel - 1) / 2);
        var template = templates[Random.Shared.Next(0, maxTemplateIndex + 1)];

        // Determine difficulty based on PLAYER level (not workshop level)
        // This provides progressive challenge as players advance
        int playerLevel = _gameStateService.State.PlayerLevel;
        var difficulty = GetDifficultyForPlayerLevel(playerLevel);

        // Basis-Belohnung (Difficulty wird nur in Order.FinalReward angewendet)
        decimal baseReward = 300m * workshopLevel * workshopType.GetBaseIncomeMultiplier();

        // Calculate base XP
        int baseXp = 25 * workshopLevel;

        // Create the order
        var order = new Order
        {
            TitleKey = template.TitleKey,
            TitleFallback = template.TitleFallback,
            WorkshopType = workshopType,
            Difficulty = difficulty,
            BaseReward = Math.Round(baseReward),
            BaseXp = baseXp,
            RequiredLevel = Math.Max(1, workshopLevel - 1),
            Tasks = template.GameTypes.Select(gt => new OrderTask
            {
                GameType = gt,
                DescriptionKey = $"task_{gt.ToString().ToLower()}",
                DescriptionFallback = gt.GetLocalizationKey()
            }).ToList()
        };

        return order;
    }

    public List<Order> GenerateAvailableOrders(int count = 3)
    {
        var orders = new List<Order>();
        var state = _gameStateService.State;

        // ExtraOrderSlots aus Office-Gebäude + Research
        var office = state.GetBuilding(BuildingType.Office);
        int extraFromBuilding = office?.ExtraOrderSlots ?? 0;
        int extraFromResearch = _researchService?.GetTotalEffects()?.ExtraOrderSlots ?? 0;
        int totalCount = count + extraFromBuilding + extraFromResearch;

        // Get all unlocked workshops
        var unlockedWorkshops = state.Workshops
            .Where(w => state.IsWorkshopUnlocked(w.Type))
            .ToList();

        if (unlockedWorkshops.Count == 0)
        {
            // No workshops yet, generate a carpenter order
            orders.Add(GenerateOrder(WorkshopType.Carpenter, 1));
            return orders;
        }

        // Generate orders for different workshops
        for (int i = 0; i < totalCount; i++)
        {
            var workshop = unlockedWorkshops[Random.Shared.Next(unlockedWorkshops.Count)];
            orders.Add(GenerateOrder(workshop.Type, workshop.Level));
        }

        return orders;
    }

    public void RefreshOrders()
    {
        var state = _gameStateService.State;

        // Clear old orders
        state.AvailableOrders.Clear();

        // Generate new orders
        var newOrders = GenerateAvailableOrders(3);
        state.AvailableOrders.AddRange(newOrders);

        _gameStateService.MarkDirty();
    }

    /// <summary>
    /// Gets order difficulty based on player level for progressive challenge.
    /// Level 1-10:  80% Easy, 20% Medium
    /// Level 11-20: 50% Easy, 40% Medium, 10% Hard
    /// Level 21+:   20% Easy, 40% Medium, 40% Hard
    /// </summary>
    private OrderDifficulty GetDifficultyForPlayerLevel(int playerLevel)
    {
        int roll = Random.Shared.Next(100);

        return playerLevel switch
        {
            <= 10 => roll < 80 ? OrderDifficulty.Easy : OrderDifficulty.Medium,
            <= 20 => roll < 50 ? OrderDifficulty.Easy
                   : roll < 90 ? OrderDifficulty.Medium
                   : OrderDifficulty.Hard,
            _ => roll < 20 ? OrderDifficulty.Easy
               : roll < 60 ? OrderDifficulty.Medium
               : OrderDifficulty.Hard
        };
    }

    /// <summary>
    /// Template for order generation.
    /// </summary>
    private record OrderTemplate(string TitleKey, string TitleFallback, params MiniGameType[] GameTypes);
}
