using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.Icons;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für das Crafting-System (Produktionsketten mit Rezepten).
/// Zeigt verfügbare Rezepte, aktive Aufträge und Inventar.
/// </summary>
public sealed partial class CraftingViewModel : ViewModelBase, INavigable, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly ICraftingService _craftingService;
    private readonly ILocalizationService _localizationService;
    private readonly IDailyChallengeService? _dailyChallengeService;
    private readonly IWeeklyMissionService? _weeklyMissionService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private ObservableCollection<CraftingRecipeDisplay> _recipes = [];

    [ObservableProperty]
    private ObservableCollection<CraftingJobDisplay> _activeJobs = [];

    [ObservableProperty]
    private ObservableCollection<InventoryItemDisplay> _inventoryItems = [];

    /// <summary>
    /// Aktuelle Geld-Balance-Anzeige im Header.
    /// </summary>
    [ObservableProperty]
    private string _currentBalance = "";

    /// <summary>
    /// Verfügbare Workshop-Typen für die ComboBox.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<WorkshopOption> _availableWorkshops = [];

    [ObservableProperty]
    private WorkshopOption? _selectedWorkshop;

    /// <summary>
    /// Ob aktive Crafting-Aufträge vorhanden sind.
    /// </summary>
    public bool HasActiveJobs => ActiveJobs.Count > 0;

    partial void OnActiveJobsChanged(ObservableCollection<CraftingJobDisplay> value) =>
        OnPropertyChanged(nameof(HasActiveJobs));

    /// <summary>
    /// Ob Inventar-Produkte vorhanden sind.
    /// </summary>
    public bool HasInventoryItems => InventoryItems.Count > 0;

    partial void OnInventoryItemsChanged(ObservableCollection<InventoryItemDisplay> value) =>
        OnPropertyChanged(nameof(HasInventoryItems));

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public CraftingViewModel(
        IGameStateService gameStateService,
        ICraftingService craftingService,
        ILocalizationService localizationService,
        IDailyChallengeService? dailyChallengeService = null,
        IWeeklyMissionService? weeklyMissionService = null)
    {
        _gameStateService = gameStateService;
        _craftingService = craftingService;
        _localizationService = localizationService;
        _dailyChallengeService = dailyChallengeService;
        _weeklyMissionService = weeklyMissionService;

        // Auto-Refresh wenn Timer abläuft
        _craftingService.CraftingUpdated += OnCraftingUpdated;

        BuildWorkshopOptions();
        UpdateLocalizedTexts();
        RefreshCrafting();
    }

    private void OnCraftingUpdated()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshCrafting);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartCrafting(CraftingRecipeDisplay? recipe)
    {
        if (recipe == null || string.IsNullOrEmpty(recipe.Id)) return;

        _craftingService.StartCrafting(recipe.Id);
        RefreshCrafting();
    }

    [RelayCommand]
    private void CollectProduct(string? jobId)
    {
        if (string.IsNullOrEmpty(jobId)) return;

        _craftingService.CollectProduct(jobId);
        _dailyChallengeService?.OnCraftingCompleted();
        _weeklyMissionService?.OnCraftingCompleted();
        RefreshCrafting();
    }

    [RelayCommand]
    private void SellItem(InventoryItemDisplay? item)
    {
        if (item == null || string.IsNullOrEmpty(item.ProductId)) return;

        if (_craftingService.SellProduct(item.ProductId))
        {
            _dailyChallengeService?.OnItemsSold(1);
            _weeklyMissionService?.OnItemsSold(1);
        }
        RefreshCrafting();
    }

    /// <summary>
    /// Verkauft 10 Einheiten eines Produkts.
    /// </summary>
    [RelayCommand]
    private void SellItem10(InventoryItemDisplay? item)
    {
        if (item == null || string.IsNullOrEmpty(item.ProductId)) return;

        int before = _gameStateService.State.CraftingInventory.GetValueOrDefault(item.ProductId, 0);
        _craftingService.SellProducts(item.ProductId, 10);
        int sold = before - _gameStateService.State.CraftingInventory.GetValueOrDefault(item.ProductId, 0);
        if (sold > 0) { _dailyChallengeService?.OnItemsSold(sold); _weeklyMissionService?.OnItemsSold(sold); }
        RefreshCrafting();
    }

    /// <summary>
    /// Verkauft alle Einheiten eines Produkts.
    /// </summary>
    [RelayCommand]
    private void SellItemAll(InventoryItemDisplay? item)
    {
        if (item == null || string.IsNullOrEmpty(item.ProductId)) return;

        int sold = item.Quantity;
        _craftingService.SellProducts(item.ProductId, item.Quantity);
        if (sold > 0) { _dailyChallengeService?.OnItemsSold(sold); _weeklyMissionService?.OnItemsSold(sold); }
        RefreshCrafting();
    }

    /// <summary>
    /// Verkauft alle Produkte im gesamten Inventar.
    /// </summary>
    [RelayCommand]
    private void SellAll()
    {
        var state = _gameStateService.State;
        var productIds = new List<string>(state.CraftingInventory.Keys);
        int totalSold = 0;
        foreach (var productId in productIds)
        {
            int count = state.CraftingInventory.GetValueOrDefault(productId, 0);
            if (count > 0)
            {
                _craftingService.SellProducts(productId, count);
                totalSold += count;
            }
        }
        if (totalSold > 0) { _dailyChallengeService?.OnItemsSold(totalSold); _weeklyMissionService?.OnItemsSold(totalSold); }
        RefreshCrafting();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert alle Crafting-Daten aus dem State.
    /// </summary>
    public void RefreshCrafting()
    {
        var state = _gameStateService.State;
        var allProducts = CraftingProduct.GetAllProducts();

        // Geld-Balance aktualisieren
        CurrentBalance = MoneyFormatter.Format(state.Money, 0);

        // Workshop-Typ aus Auswahl ermitteln
        var workshopType = SelectedWorkshop?.Type ?? WorkshopType.Carpenter;

        // Workshop-Level für ausgewählten Typ ermitteln
        var workshop = state.Workshops.FirstOrDefault(w => w.Type == workshopType);
        int workshopLevel = workshop?.Level ?? 0;

        // Verfügbare Rezepte
        var availableRecipes = _craftingService.GetAvailableRecipes(workshopType, workshopLevel);
        var recipeItems = new ObservableCollection<CraftingRecipeDisplay>();

        foreach (var recipe in availableRecipes)
        {
            // Input-Anzeige aufbauen
            string inputsDisplay = BuildInputsDisplay(recipe, allProducts, state.CraftingInventory);

            // Prüfen ob genug Materialien vorhanden
            bool canCraft = CanCraftRecipe(recipe, state.CraftingInventory);

            // Output-Name + Icon
            string outputName = allProducts.TryGetValue(recipe.OutputProductId, out var product)
                ? _localizationService.GetString(product.NameKey) ?? product.NameKey
                : recipe.OutputProductId;
            var outputIcon = GetProductIcon(recipe.OutputProductId);

            // Dauer formatieren
            string durationDisplay = FormatDuration(recipe.DurationSeconds);

            recipeItems.Add(new CraftingRecipeDisplay
            {
                Id = recipe.Id,
                Name = _localizationService.GetString(recipe.NameKey) ?? recipe.NameKey,
                InputDisplay = inputsDisplay,
                OutputName = outputName,
                OutputIcon = outputIcon,
                DurationDisplay = durationDisplay,
                CanCraft = canCraft
            });
        }

        Recipes = recipeItems;

        // Aktive Crafting-Aufträge
        var jobItems = new ObservableCollection<CraftingJobDisplay>();
        foreach (var job in state.ActiveCraftingJobs)
        {
            // Rezept-Info für Produkt-Name
            var recipeForJob = CraftingRecipe.GetAllRecipes().FirstOrDefault(r => r.Id == job.RecipeId);
            string productName = "";
            var outputIcon = GameIconKind.PackageVariant;
            if (recipeForJob != null && allProducts.TryGetValue(recipeForJob.OutputProductId, out var outputProduct))
            {
                productName = _localizationService.GetString(outputProduct.NameKey) ?? outputProduct.NameKey;
                outputIcon = GetProductIcon(recipeForJob.OutputProductId);
            }

            var remaining = job.TimeRemaining;
            string timeDisplay = remaining > TimeSpan.Zero
                ? FormatDuration((int)remaining.TotalSeconds)
                : _localizationService.GetString("Ready") ?? "Fertig";

            jobItems.Add(new CraftingJobDisplay
            {
                JobId = job.JobId,
                OutputName = productName,
                OutputIcon = outputIcon,
                Progress = job.Progress,
                ProgressPercentDisplay = $"{job.Progress * 100:F0}%",
                ProgressBarWidth = job.Progress * 200.0,
                TimeRemainingDisplay = timeDisplay,
                IsComplete = job.IsComplete
            });
        }

        ActiveJobs = jobItems;

        // Inventar (mit skalierten Preisen)
        var inventoryItemList = new ObservableCollection<InventoryItemDisplay>();
        foreach (var (productId, count) in state.CraftingInventory)
        {
            if (count <= 0) continue;

            string name = allProducts.TryGetValue(productId, out var invProduct)
                ? _localizationService.GetString(invProduct.NameKey) ?? invProduct.NameKey
                : productId;

            // Skalierter Verkaufspreis (inkl. Level + alle Multiplikatoren)
            decimal sellPrice = _craftingService.GetSellPrice(productId);
            var icon = GetProductIcon(productId);

            inventoryItemList.Add(new InventoryItemDisplay
            {
                ProductId = productId,
                Name = name,
                Icon = icon,
                Quantity = count,
                QuantityDisplay = $"x{count}",
                ValueDisplay = MoneyFormatter.Format(sellPrice, 0),
                TotalValueDisplay = MoneyFormatter.Format(sellPrice * count, 0)
            });
        }

        InventoryItems = inventoryItemList;
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("Crafting") ?? "Handwerk";
        BuildWorkshopOptions();
        RefreshCrafting();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildWorkshopOptions()
    {
        var options = new ObservableCollection<WorkshopOption>();
        var state = _gameStateService.State;

        // Alle freigeschalteten Workshops mit Crafting-Rezepten anzeigen
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (!state.IsWorkshopUnlocked(ws.Type)) continue;

            // Nur Workshops anzeigen die mindestens 1 Rezept haben
            var recipes = CraftingRecipe.GetAllRecipes();
            bool hasRecipes = false;
            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].WorkshopType == ws.Type) { hasRecipes = true; break; }
            }
            if (!hasRecipes) continue;

            options.Add(new WorkshopOption
            {
                Type = ws.Type,
                Name = _localizationService.GetString(ws.Type.GetLocalizationKey()) ?? ws.Type.ToString(),
                IconKind = ws.Type.GetIconKind()
            });
        }

        AvailableWorkshops = options;
        SelectedWorkshop ??= options.FirstOrDefault();
    }

    partial void OnSelectedWorkshopChanged(WorkshopOption? value)
    {
        if (value != null) RefreshCrafting();
    }

    private string BuildInputsDisplay(CraftingRecipe recipe, Dictionary<string, CraftingProduct> allProducts,
        Dictionary<string, int> craftingInventory)
    {
        if (recipe.InputProducts.Count == 0)
            return _localizationService.GetString("NoInputRequired") ?? "Keine Materialien";

        var parts = new List<string>();
        foreach (var (productId, needed) in recipe.InputProducts)
        {
            string name = allProducts.TryGetValue(productId, out var p)
                ? _localizationService.GetString(p.NameKey) ?? p.NameKey
                : productId;
            int have = craftingInventory.GetValueOrDefault(productId, 0);
            parts.Add($"{name} ({have}/{needed})");
        }

        return string.Join(", ", parts);
    }

    private static bool CanCraftRecipe(CraftingRecipe recipe, Dictionary<string, int> craftingInventory)
    {
        foreach (var (productId, needed) in recipe.InputProducts)
        {
            int have = craftingInventory.GetValueOrDefault(productId, 0);
            if (have < needed) return false;
        }

        return true;
    }

    private static string FormatDuration(int totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{totalSeconds}s";
    }

    private static GameIconKind GetProductIcon(string productId) => productId switch
    {
        // Schreiner
        "planks" => GameIconKind.Forest,
        "furniture" => GameIconKind.SeatOutline,
        "luxury_furniture" => GameIconKind.Crown,
        // Klempner
        "pipes" => GameIconKind.Pipe,
        "plumbing_system" => GameIconKind.Water,
        "bathroom_installation" => GameIconKind.ShowerHead,
        // Elektriker
        "cables" => GameIconKind.CableData,
        "circuit" => GameIconKind.Chip,
        "smart_home" => GameIconKind.HomeAutomation,
        // Maler
        "paint_mix" => GameIconKind.Palette,
        "wall_design" => GameIconKind.FormatPaint,
        "artwork" => GameIconKind.Palette,
        // Dachdecker
        "roof_tiles" => GameIconKind.ViewGrid,
        "roofing_system" => GameIconKind.HomeRoof,
        "roof_structure" => GameIconKind.HomeRoof,
        // Bauunternehmer
        "concrete" => GameIconKind.Wall,
        // Architekt
        "blueprint" => GameIconKind.Compass,
        // Generalunternehmer
        "contract" => GameIconKind.FileDocumentCheck,
        // Meisterschmiede
        "fittings" => GameIconKind.Anvil,
        // Innovationslabor
        "prototype" => GameIconKind.LightbulbOnOutline,
        _ => GameIconKind.PackageVariant
    };

    // GetWorkshopIconKind entfernt - nutze WorkshopType.GetIconKind() Extension direkt

    public void Dispose()
    {
        _craftingService.CraftingUpdated -= OnCraftingUpdated;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DISPLAY MODELS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Workshop-Option für die ComboBox-Auswahl.
/// </summary>
public class WorkshopOption
{
    public WorkshopType Type { get; set; }
    public string Name { get; set; } = "";
    public string IconKind { get; set; } = "Cog";
}

/// <summary>
/// Anzeige-Modell für ein Crafting-Rezept im UI.
/// </summary>
public class CraftingRecipeDisplay
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string InputDisplay { get; set; } = "";
    public string OutputName { get; set; } = "";
    public GameIconKind OutputIcon { get; set; } = GameIconKind.PackageVariant;
    public string DurationDisplay { get; set; } = "";
    public bool CanCraft { get; set; }
}

/// <summary>
/// Anzeige-Modell für einen aktiven Crafting-Auftrag im UI.
/// </summary>
public class CraftingJobDisplay
{
    public string JobId { get; set; } = "";
    public string OutputName { get; set; } = "";
    public GameIconKind OutputIcon { get; set; } = GameIconKind.PackageVariant;
    public double Progress { get; set; }
    public string ProgressPercentDisplay { get; set; } = "";
    public double ProgressBarWidth { get; set; }
    public string TimeRemainingDisplay { get; set; } = "";
    public bool IsComplete { get; set; }
}

/// <summary>
/// Anzeige-Modell für ein Crafting-Inventar-Produkt im UI.
/// </summary>
public class InventoryItemDisplay
{
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public GameIconKind Icon { get; set; } = GameIconKind.PackageVariant;
    public int Quantity { get; set; }
    public string QuantityDisplay { get; set; } = "";
    /// <summary>Skalierter Einzelpreis (inkl. Level + Multiplikatoren).</summary>
    public string ValueDisplay { get; set; } = "";
    /// <summary>Gesamtwert (Einzelpreis × Menge).</summary>
    public string TotalValueDisplay { get; set; } = "";
}
