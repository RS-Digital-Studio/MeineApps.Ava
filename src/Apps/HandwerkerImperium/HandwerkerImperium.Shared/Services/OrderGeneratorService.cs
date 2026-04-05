using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Generates new orders/contracts for the player.
/// </summary>
public sealed class OrderGeneratorService : IOrderGeneratorService
{
    private readonly IGameStateService _gameStateService;
    private readonly IResearchService? _researchService;
    private readonly IAutoProductionService? _autoProductionService;

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

    public OrderGeneratorService(
        IGameStateService gameStateService,
        IResearchService? researchService = null,
        IAutoProductionService? autoProductionService = null)
    {
        _gameStateService = gameStateService;
        _researchService = researchService;
        _autoProductionService = autoProductionService;
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
        // For-Schleife statt LINQ Count (vermeidet Closure+Enumerator)
        int unlockedWorkshops = 0;
        for (int i = 0; i < state.Workshops.Count; i++)
            if (state.IsWorkshopUnlocked(state.Workshops[i].Type)) unlockedWorkshops++;
        int roll = Random.Shared.Next(100);

        // Reputation-Bonus + Gilden-Forschung + Research PremiumOrderChance: Senkt Standard-Wahrscheinlichkeit
        decimal reputationBonus = state.Reputation.OrderQualityBonus;
        decimal guildOrderQuality = state.GuildMembership?.ResearchOrderQualityBonus ?? 0m;
        decimal researchPremiumChance = _researchService?.GetTotalEffects()?.PremiumOrderChance ?? 0m;
        int adjustedRoll = Math.Clamp((int)(roll - (reputationBonus + guildOrderQuality + researchPremiumChance) * 100), 0, 100);

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

        // Schwierigkeit basiert auf Workshop-Level + Prestige-Stufe + Reputation
        int prestigeCount = state.Prestige?.TotalPrestigeCount ?? 0;
        int reputation = state.Reputation.ReputationScore;
        var difficulty = GetDifficulty(workshopLevel, prestigeCount, reputation);

        int playerLevel = state.PlayerLevel;

        // OrderType bestimmen (Standard, Large, Weekly, Cooperation)
        var orderType = DetermineOrderType(workshopLevel, playerLevel);

        // Tasks erstellen basierend auf OrderType
        var (minTasks, maxTasks) = orderType.GetTaskCount();
        int targetTaskCount = Random.Shared.Next(minTasks, maxTasks + 1);
        var tasks = new List<OrderTask>();
        var cooperationSecondType = workshopType; // Für Cooperation: zweiter Workshop-Typ

        if (orderType == OrderType.Cooperation)
        {
            // Cooperation: Tasks aus 2 verschiedenen Workshop-Typen mischen
            // Zufaelligen freigeschalteten Workshop waehlen (ohne LINQ/ToList)
            int eligibleCount = 0;
            for (int i = 0; i < state.Workshops.Count; i++)
            {
                var w = state.Workshops[i];
                if (state.IsWorkshopUnlocked(w.Type) && w.Type != workshopType)
                    eligibleCount++;
            }
            var secondType = workshopType;
            if (eligibleCount > 0)
            {
                int pick = Random.Shared.Next(eligibleCount);
                int seen = 0;
                for (int i = 0; i < state.Workshops.Count; i++)
                {
                    var w = state.Workshops[i];
                    if (state.IsWorkshopUnlocked(w.Type) && w.Type != workshopType)
                    {
                        if (seen == pick) { secondType = w.Type; break; }
                        seen++;
                    }
                }
            }
            cooperationSecondType = secondType;
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

        // Cooperation: Benötigte Workshop-Typen setzen (beide Workshops)
        if (orderType == OrderType.Cooperation && cooperationSecondType != workshopType)
        {
            order.RequiredWorkshops = [workshopType, cooperationSecondType];
        }

        // Stammkunden-Zuordnung (20% Chance wenn Stammkunden vorhanden, ohne LINQ/ToList)
        if (Random.Shared.NextDouble() < 0.20)
        {
            int regularCount = 0;
            var customers = state.Reputation.RegularCustomers;
            for (int i = 0; i < customers.Count; i++)
                if (customers[i].IsRegular) regularCount++;
            if (regularCount > 0)
            {
                int pick = Random.Shared.Next(regularCount);
                int seen = 0;
                for (int i = 0; i < customers.Count; i++)
                {
                    if (customers[i].IsRegular)
                    {
                        if (seen == pick)
                        {
                            order.CustomerId = customers[i].Id;
                            order.CustomerName = customers[i].Name;
                            order.CustomerAvatarSeed = customers[i].AvatarSeed;
                            break;
                        }
                        seen++;
                    }
                }
            }
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

        // Freigeschaltete Workshops zaehlen (ohne LINQ/ToList)
        int unlockedCount = 0;
        for (int i = 0; i < state.Workshops.Count; i++)
            if (state.IsWorkshopUnlocked(state.Workshops[i].Type)) unlockedCount++;

        if (unlockedCount == 0)
        {
            // No workshops yet, generate a carpenter order
            orders.Add(GenerateOrder(WorkshopType.Carpenter, 1));
            return orders;
        }

        // Auftraege fuer zufaellige freigeschaltete Workshops generieren (ohne ToList)
        for (int i = 0; i < totalCount; i++)
        {
            int pick = Random.Shared.Next(unlockedCount);
            int seen = 0;
            Workshop? picked = null;
            for (int j = 0; j < state.Workshops.Count; j++)
            {
                if (state.IsWorkshopUnlocked(state.Workshops[j].Type))
                {
                    if (seen == pick) { picked = state.Workshops[j]; break; }
                    seen++;
                }
            }
            picked ??= state.Workshops[0]; // Fallback (sollte nicht eintreten)
            orders.Add(GenerateOrder(picked.Type, picked.Level));
        }

        return orders;
    }

    public void RefreshOrders()
    {
        var state = _gameStateService.State;

        // Bestehende Lieferaufträge beibehalten (nicht bei Refresh löschen)
        var existingMaterialOrders = new List<Order>();
        for (int i = 0; i < state.AvailableOrders.Count; i++)
        {
            if (state.AvailableOrders[i].OrderType == OrderType.MaterialOrder && !state.AvailableOrders[i].IsExpired)
                existingMaterialOrders.Add(state.AvailableOrders[i]);
        }

        // Alte normale Orders entfernen
        state.AvailableOrders.Clear();

        // Bestehende Lieferaufträge zurück hinzufügen
        state.AvailableOrders.AddRange(existingMaterialOrders);

        // Neue normale Orders generieren
        var newOrders = GenerateAvailableOrders(3);
        state.AvailableOrders.AddRange(newOrders);

        // Lieferauftrag generieren (1 pro Refresh wenn unter Tageslimit)
        if (existingMaterialOrders.Count == 0)
        {
            var materialOrder = GenerateMaterialOrder();
            if (materialOrder != null)
                state.AvailableOrders.Add(materialOrder);
        }

    }

    public Order? GenerateMaterialOrder()
    {
        if (_autoProductionService == null) return null;
        var state = _gameStateService.State;

        // Tages-Reset prüfen
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (state.LastMaterialOrderReset != today)
        {
            state.Statistics.MaterialOrdersCompletedToday = 0;
            state.LastMaterialOrderReset = today;
        }

        // Tageslimit prüfen
        if (state.Statistics.MaterialOrdersCompletedToday >= GameBalanceConstants.MaterialOrdersPerDay)
            return null;

        // Qualifizierte Workshops finden (Auto-Produktion freigeschaltet)
        var qualifiedWorkshops = new List<Workshop>();
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (state.IsWorkshopUnlocked(ws.Type) && _autoProductionService.IsAutoProductionUnlocked(ws))
                qualifiedWorkshops.Add(ws);
        }
        if (qualifiedWorkshops.Count == 0) return null;

        // Haupt-Workshop auswählen
        var mainWorkshop = qualifiedWorkshops[Random.Shared.Next(qualifiedWorkshops.Count)];
        var mainProduct = _autoProductionService.GetTier1ProductId(mainWorkshop.Type);
        if (mainProduct == null) return null;

        // Benötigte Materialien generieren
        var materials = new Dictionary<string, int>();
        int playerLevel = state.PlayerLevel;

        // Basis: 5-10 Items vom Haupt-Workshop (skaliert mit Level)
        int mainCount = 5 + Math.Min(playerLevel / 50, 10);
        materials[mainProduct] = mainCount;

        // Cross-Workshop ab Level 100: 3-5 Items von einem anderen Workshop
        if (playerLevel >= GameBalanceConstants.MaterialOrderCrossWorkshopLevel && qualifiedWorkshops.Count >= 2)
        {
            Workshop? secondWorkshop = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var candidate = qualifiedWorkshops[Random.Shared.Next(qualifiedWorkshops.Count)];
                if (candidate.Type != mainWorkshop.Type) { secondWorkshop = candidate; break; }
            }
            if (secondWorkshop != null)
            {
                var secondProduct = _autoProductionService.GetTier1ProductId(secondWorkshop.Type);
                if (secondProduct != null)
                    materials[secondProduct] = 3 + Math.Min(playerLevel / 100, 5);
            }
        }

        // Belohnung berechnen (BaseReward-Formel wie normale Orders)
        var netIncome = Math.Max(0m, state.NetIncomePerSecond);
        var perItemReward = Math.Max(100m + playerLevel * 100m, netIncome * 300m);
        int totalItems = 0;
        foreach (var kv in materials) totalItems += kv.Value;
        decimal baseReward = perItemReward * (1.0m + totalItems * 0.1m) * mainWorkshop.Type.GetBaseIncomeMultiplier();

        // Gilden-Bonus
        var guildRewardBonus = state.GuildMembership?.ResearchRewardBonus ?? 0m;
        if (guildRewardBonus > 0) baseReward *= (1m + guildRewardBonus);

        int baseXp = 25 * mainWorkshop.Level * Math.Max(1, totalItems / 3);

        // Kundennamen
        int nameSeed = (int)(DateTime.UtcNow.Ticks % int.MaxValue) ^ Random.Shared.Next();
        string customerName = GenerateCustomerName(nameSeed);

        // Schwierigkeit skaliert mit Workshop-Level (beeinflusst Reward-Multiplikator)
        var difficulty = GetMaterialOrderDifficulty(mainWorkshop.Level);

        var order = new Order
        {
            TitleKey = "OrderTypeMaterialOrder",
            TitleFallback = "Delivery Order",
            WorkshopType = mainWorkshop.Type,
            OrderType = OrderType.MaterialOrder,
            Difficulty = difficulty,
            BaseReward = Math.Round(baseReward),
            BaseXp = baseXp,
            RequiredLevel = 1,
            CustomerName = customerName,
            CustomerAvatarSeed = nameSeed.ToString("X8"),
            RequiredMaterials = materials,
            Deadline = DateTime.UtcNow + OrderType.MaterialOrder.GetDeadline(),
            Tasks = [] // Keine MiniGames
        };

        return order;
    }

    /// <summary>
    /// Bestimmt MaterialOrder-Schwierigkeit basierend auf Workshop-Level.
    /// Höheres Level = mehr benötigte Items + höherer Reward-Multiplikator.
    /// Kein Expert (MaterialOrders haben kein MiniGame, Expert wäre irreführend).
    /// </summary>
    private static OrderDifficulty GetMaterialOrderDifficulty(int workshopLevel)
    {
        return workshopLevel switch
        {
            <= 75  => OrderDifficulty.Easy,
            <= 200 => OrderDifficulty.Medium,
            _      => OrderDifficulty.Hard
        };
    }

    /// <summary>
    /// Bestimmt Auftrags-Schwierigkeit basierend auf Workshop-Level, Prestige-Stufe und Reputation.
    /// Höheres Workshop-Level → mehr Hard/Expert. Prestige schaltet Expert frei.
    /// Expert erfordert Reputation 80+ (fällt auf Hard zurück wenn nicht erreicht).
    /// </summary>
    private static OrderDifficulty GetDifficulty(int workshopLevel, int prestigeCount, int reputation)
    {
        int roll = Random.Shared.Next(100);

        // Prestige-Stufen: 0=Kein, 1+=Bronze, 2+=Silver, 3+=Gold
        var result = (workshopLevel, prestigeCount) switch
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

        // Expert-Aufträge erfordern Reputation 80+ — sonst auf Hard zurückfallen
        if (result == OrderDifficulty.Expert && reputation < OrderDifficulty.Expert.GetRequiredReputation())
            return OrderDifficulty.Hard;

        return result;
    }

    /// <summary>
    /// Template for order generation.
    /// </summary>
    private record OrderTemplate(string TitleKey, string TitleFallback, params MiniGameType[] GameTypes);
}
