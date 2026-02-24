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
        ],
        [WorkshopType.MasterSmith] =
        [
            new("order_forge_blade", "Forge Blade", MiniGameType.ForgeGame),
            new("order_master_tools", "Forge Master Tools", MiniGameType.ForgeGame, MiniGameType.ForgeGame),
            new("order_forge_artifact", "Forge Artifact", MiniGameType.ForgeGame, MiniGameType.ForgeGame, MiniGameType.ForgeGame)
        ],
        [WorkshopType.InnovationLab] =
        [
            new("order_prototype", "Build Prototype", MiniGameType.InventGame),
            new("order_invention", "Create Invention", MiniGameType.InventGame, MiniGameType.InventGame),
            new("order_breakthrough", "Revolutionary Breakthrough", MiniGameType.InventGame, MiniGameType.InventGame, MiniGameType.InventGame)
        ]
    };

    // Kundennamen für Aufträge
    private static readonly string[] _firstNames =
    {
        "Hans", "Klaus", "Werner", "Petra", "Sabine", "Ingrid", "Thomas", "Michael",
        "Monika", "Helga", "Stefan", "Andreas", "Brigitte", "Ursula", "Frank",
        "Jürgen", "Renate", "Dieter", "Gabriele", "Gerhard", "Manfred", "Erika",
        "Wolfgang", "Heike", "Ralf", "Ulrike", "Heinz", "Karin", "Bernd", "Martina"
    };
    private static readonly string[] _lastNames =
    {
        "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner",
        "Becker", "Schulz", "Hoffmann", "Schäfer", "Koch", "Bauer", "Richter",
        "Klein", "Wolf", "Schröder", "Neumann", "Schwarz", "Zimmermann", "Braun",
        "Krüger", "Hartmann", "Lange", "Schmitt", "Werner", "Krause", "Meier",
        "Lehmann", "Schmid"
    };

    public OrderGeneratorService(IGameStateService gameStateService, IResearchService? researchService = null)
    {
        _gameStateService = gameStateService;
        _researchService = researchService;
    }

    /// <summary>
    /// Generiert einen Kundennamen deterministisch aus einem Seed.
    /// </summary>
    private static string GenerateCustomerName(int seed)
    {
        var rng = new Random(seed);
        return $"{_firstNames[rng.Next(_firstNames.Length)]} {_lastNames[rng.Next(_lastNames.Length)]}";
    }

    /// <summary>
    /// Bestimmt den OrderType basierend auf Spieler-Level und freigeschalteten Workshops.
    /// Höhere Level schalten Large/Cooperation/Weekly frei.
    /// </summary>
    private OrderType DetermineOrderType(int workshopLevel, int playerLevel)
    {
        var state = _gameStateService.State;
        int unlockedWorkshops = state.Workshops.Count(w => state.IsWorkshopUnlocked(w.Type));
        int roll = Random.Shared.Next(100);

        // Reputation-Bonus + Gilden-Forschung: Senkt Standard-Wahrscheinlichkeit
        decimal reputationBonus = state.Reputation.OrderQualityBonus;
        decimal guildOrderQuality = state.GuildMembership?.ResearchOrderQualityBonus ?? 0m;
        int adjustedRoll = Math.Clamp((int)(roll - (reputationBonus + guildOrderQuality) * 100), 0, 100);

        return playerLevel switch
        {
            < 10 => OrderType.Standard,
            < 15 => adjustedRoll < 70 ? OrderType.Standard : OrderType.Large,
            < 20 => unlockedWorkshops >= 2
                ? adjustedRoll < 55 ? OrderType.Standard
                    : adjustedRoll < 80 ? OrderType.Large
                    : OrderType.Cooperation
                : adjustedRoll < 70 ? OrderType.Standard : OrderType.Large,
            _ => unlockedWorkshops >= 2
                ? adjustedRoll < 45 ? OrderType.Standard
                    : adjustedRoll < 70 ? OrderType.Large
                    : adjustedRoll < 85 ? OrderType.Cooperation
                    : OrderType.Weekly
                : adjustedRoll < 55 ? OrderType.Standard
                    : adjustedRoll < 80 ? OrderType.Large
                    : OrderType.Weekly
        };
    }

    public Order GenerateOrder(WorkshopType workshopType, int workshopLevel)
    {
        var state = _gameStateService.State;
        var templates = _templates.GetValueOrDefault(workshopType, _templates[WorkshopType.Carpenter]);

        // Select a template based on level (higher levels get harder orders)
        int maxTemplateIndex = Math.Min(templates.Count - 1, (workshopLevel - 1) / 2);
        var template = templates[Random.Shared.Next(0, maxTemplateIndex + 1)];

        // Schwierigkeit basiert auf Workshop-Level + Prestige-Stufe
        int prestigeCount = state.Prestige?.TotalPrestigeCount ?? 0;
        var difficulty = GetDifficulty(workshopLevel, prestigeCount);

        int playerLevel = state.PlayerLevel;

        // OrderType bestimmen (Standard, Large, Weekly, Cooperation)
        var orderType = DetermineOrderType(workshopLevel, playerLevel);

        // Tasks erstellen basierend auf OrderType
        var (minTasks, maxTasks) = orderType.GetTaskCount();
        int targetTaskCount = Random.Shared.Next(minTasks, maxTasks + 1);
        var tasks = new List<OrderTask>();

        if (orderType == OrderType.Cooperation)
        {
            // Cooperation: Tasks aus 2 verschiedenen Workshop-Typen mischen
            var unlockedWorkshops = state.Workshops
                .Where(w => state.IsWorkshopUnlocked(w.Type) && w.Type != workshopType)
                .ToList();
            var secondType = unlockedWorkshops.Count > 0
                ? unlockedWorkshops[Random.Shared.Next(unlockedWorkshops.Count)].Type
                : workshopType;
            var secondTemplates = _templates.GetValueOrDefault(secondType, templates);
            var secondTemplate = secondTemplates[Random.Shared.Next(Math.Min(secondTemplates.Count, maxTemplateIndex + 1))];

            // Tasks abwechselnd mischen
            for (int i = 0; i < targetTaskCount; i++)
            {
                var src = i % 2 == 0 ? template : secondTemplate;
                int idx = i / 2 % src.GameTypes.Length;
                tasks.Add(new OrderTask
                {
                    GameType = src.GameTypes[idx],
                    DescriptionKey = $"task_{src.GameTypes[idx].ToString().ToLower()}",
                    DescriptionFallback = src.GameTypes[idx].GetLocalizationKey()
                });
            }
        }
        else
        {
            // Standard/Large/Weekly: Template-Tasks wiederholen/mischen
            for (int i = 0; i < targetTaskCount; i++)
            {
                var gt = template.GameTypes[i % template.GameTypes.Length];
                tasks.Add(new OrderTask
                {
                    GameType = gt,
                    DescriptionKey = $"task_{gt.ToString().ToLower()}",
                    DescriptionFallback = gt.GetLocalizationKey()
                });
            }
        }

        // Basis-Belohnung skaliert mit aktuellem Einkommen (wie QuickJobs)
        var netIncomePerSecond = Math.Max(0m, state.NetIncomePerSecond);
        int taskCount = tasks.Count;
        // Pro Aufgabe ~5 Minuten Einkommen (300s), Mindestens Level*100 pro Aufgabe
        var perTaskReward = Math.Max(100m + playerLevel * 100m, netIncomePerSecond * 300m);
        // Aufgaben-Anzahl als Multiplikator (mit Bonus: mehr Tasks = überproportional mehr)
        decimal taskMultiplier = taskCount * (1.0m + (taskCount - 1) * 0.15m);
        decimal baseReward = perTaskReward * taskMultiplier * workshopType.GetBaseIncomeMultiplier();

        // Gilden-Forschung: Auftragsbelohnungen-Bonus (+30%)
        var guildRewardBonus = state.GuildMembership?.ResearchRewardBonus ?? 0m;
        if (guildRewardBonus > 0)
            baseReward *= (1m + guildRewardBonus);

        // Calculate base XP (skaliert mit Aufgaben-Anzahl)
        int baseXp = 25 * workshopLevel * taskCount;

        // Gilden-Forschung: XP-Bonus (+10%)
        var guildXpBonus = state.GuildMembership?.ResearchXpBonus ?? 0m;
        if (guildXpBonus > 0)
            baseXp = (int)(baseXp * (1m + guildXpBonus));

        // Kundennamen generieren
        int nameSeed = (int)(DateTime.UtcNow.Ticks % int.MaxValue) ^ Random.Shared.Next();
        string customerName = GenerateCustomerName(nameSeed);

        // Create the order
        var order = new Order
        {
            TitleKey = template.TitleKey,
            TitleFallback = template.TitleFallback,
            WorkshopType = workshopType,
            OrderType = orderType,
            Difficulty = difficulty,
            BaseReward = Math.Round(baseReward),
            BaseXp = baseXp,
            RequiredLevel = Math.Max(1, workshopLevel - 1),
            CustomerName = customerName,
            CustomerAvatarSeed = nameSeed.ToString("X8"),
            Tasks = tasks
        };

        // Deadline für Weekly-Orders
        if (orderType.HasDeadline())
            order.Deadline = DateTime.UtcNow + orderType.GetDeadline();

        // Cooperation: Benötigte Workshop-Typen setzen
        if (orderType == OrderType.Cooperation)
        {
            var requiredTypes = tasks.Select(t => t.GameType)
                .Distinct()
                .Select(gt => workshopType) // Vereinfacht: Haupt-Workshop
                .Distinct()
                .ToList();
            order.RequiredWorkshops = requiredTypes;
        }

        // Stammkunden-Zuordnung (20% Chance wenn Stammkunden vorhanden)
        var regulars = state.Reputation.RegularCustomers.Where(c => c.IsRegular).ToList();
        if (regulars.Count > 0 && Random.Shared.NextDouble() < 0.20)
        {
            var customer = regulars[Random.Shared.Next(regulars.Count)];
            order.CustomerId = customer.Id;
            order.CustomerName = customer.Name;
            order.CustomerAvatarSeed = customer.AvatarSeed;
        }

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
        int extraFromReputation = state.Reputation.ExtraOrderSlots;
        int extraFromGuildResearch = state.GuildMembership?.ResearchOrderSlotBonus ?? 0;
        int totalCount = count + extraFromBuilding + extraFromResearch + extraFromReputation + extraFromGuildResearch;

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
    /// Bestimmt Auftrags-Schwierigkeit basierend auf Workshop-Level und Prestige-Stufe.
    /// Höheres Workshop-Level → mehr Hard/Expert. Prestige schaltet Expert frei.
    /// </summary>
    private static OrderDifficulty GetDifficulty(int workshopLevel, int prestigeCount)
    {
        int roll = Random.Shared.Next(100);

        // Prestige-Stufen: 0=Kein, 1+=Bronze, 2+=Silver, 3+=Gold
        return (workshopLevel, prestigeCount) switch
        {
            // WS-Level 1-25
            (<= 25, 0)    => roll < 80 ? OrderDifficulty.Easy : OrderDifficulty.Medium,
            (<= 25, 1)    => roll < 65 ? OrderDifficulty.Easy : roll < 90 ? OrderDifficulty.Medium : roll < 100 - 5 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 25, 2)    => roll < 50 ? OrderDifficulty.Easy : roll < 80 ? OrderDifficulty.Medium : roll < 95 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 25, >= 3) => roll < 40 ? OrderDifficulty.Easy : roll < 70 ? OrderDifficulty.Medium : roll < 90 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

            // WS-Level 26-100
            (<= 100, 0)    => roll < 45 ? OrderDifficulty.Easy : roll < 90 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
            (<= 100, 1)    => roll < 25 ? OrderDifficulty.Easy : roll < 65 ? OrderDifficulty.Medium : roll < 90 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 100, 2)    => roll < 15 ? OrderDifficulty.Easy : roll < 45 ? OrderDifficulty.Medium : roll < 80 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 100, >= 3) => roll < 5  ? OrderDifficulty.Easy : roll < 30 ? OrderDifficulty.Medium : roll < 65 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

            // WS-Level 101-300
            (<= 300, 0)    => roll < 15 ? OrderDifficulty.Easy : roll < 60 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
            (<= 300, 1)    => roll < 5  ? OrderDifficulty.Easy : roll < 30 ? OrderDifficulty.Medium : roll < 75 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 300, 2)    => roll < 15 ? OrderDifficulty.Medium : roll < 60 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 300, >= 3) => roll < 10 ? OrderDifficulty.Medium : roll < 50 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

            // WS-Level 301-700
            (<= 700, 0)    => roll < 5  ? OrderDifficulty.Easy : roll < 35 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
            (<= 700, 1)    => roll < 10 ? OrderDifficulty.Medium : roll < 60 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 700, 2)    => roll < 5  ? OrderDifficulty.Medium : roll < 45 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (<= 700, >= 3) => roll < 30 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

            // WS-Level 701+
            (_, 0)    => roll < 20 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
            (_, 1)    => roll < 5  ? OrderDifficulty.Medium : roll < 45 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (_, 2)    => roll < 30 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
            (_, >= 3) => roll < 20 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

            _ => OrderDifficulty.Easy
        };
    }

    /// <summary>
    /// Template for order generation.
    /// </summary>
    private record OrderTemplate(string TitleKey, string TitleFallback, params MiniGameType[] GameTypes);
}
