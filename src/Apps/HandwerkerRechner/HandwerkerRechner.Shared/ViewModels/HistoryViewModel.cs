using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels;

/// <summary>
/// ViewModel für den History-Tab - zeigt Berechnungshistorie gruppiert nach Rechner-Typ
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly ICalculationHistoryService _historyService;
    private readonly ILocalizationService _localization;
    private readonly IPremiumAccessService _premiumAccessService;
    private readonly IPurchaseService _purchaseService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    [ObservableProperty] private ObservableCollection<CalculationHistoryGroup> _groups = new();
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _emptyText = "No calculations yet";
    [ObservableProperty] private string _extendedHintText = "";
    [ObservableProperty] private bool _showExtendedHint;

    // Lokalisierte Texte
    [ObservableProperty] private string _headerText = "History";
    [ObservableProperty] private string _watchAdText = "Watch Video";

    public HistoryViewModel(
        ICalculationHistoryService historyService,
        ILocalizationService localization,
        IPremiumAccessService premiumAccessService,
        IPurchaseService purchaseService)
    {
        _historyService = historyService;
        _localization = localization;
        _premiumAccessService = premiumAccessService;
        _purchaseService = purchaseService;
        UpdateLocalizedTexts();
    }

    public void UpdateLocalizedTexts()
    {
        HeaderText = _localization.GetString("TabHistory") ?? "History";
        EmptyText = _localization.GetString("HistoryEmpty") ?? "No calculations yet. Your calculations will appear here.";
        ExtendedHintText = _localization.GetString("HistoryExtendedHint") ?? "Watch an ad to see up to 30 entries per calculator!";
        WatchAdText = _localization.GetString("WatchAdForHistory") ?? "Watch Video";
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var isPremium = _purchaseService.IsPremium;
        var hasExtended = _premiumAccessService.HasExtendedHistory;
        var maxItems = (isPremium || hasExtended) ? 30 : 5;

        ShowExtendedHint = !isPremium && !hasExtended;

        var allItems = await _historyService.GetAllHistoryAsync(maxItems);

        var grouped = allItems
            .GroupBy(h => h.CalculatorId)
            .Select(g => new CalculationHistoryGroup(
                GetCalculatorDisplayName(g.Key),
                GetCalculatorIcon(g.Key),
                g.ToList()))
            .OrderByDescending(g => g.Items.Max(i => i.CreatedAt))
            .ToList();

        // Batch-Update: neue Collection statt Clear+Add-Loop (1 statt N+1 PropertyChanged)
        Groups = new ObservableCollection<CalculationHistoryGroup>(grouped);
        OnPropertyChanged(nameof(Groups));
        IsEmpty = Groups.Count == 0;
    }

    [RelayCommand]
    private async Task DeleteItemAsync(string itemId)
    {
        await _historyService.DeleteCalculationAsync(itemId);
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private void OpenCalculation(CalculationHistoryItem item)
    {
        // Route zum entsprechenden Rechner
        var route = GetRouteForCalculator(item.CalculatorId);
        if (route != null)
            NavigationRequested?.Invoke(route);
    }

    private string GetCalculatorDisplayName(string calculatorId) => calculatorId switch
    {
        "TileCalculator" => _localization.GetString("CalcTiles") ?? "Tiles",
        "WallpaperCalculator" => _localization.GetString("CalcWallpaper") ?? "Wallpaper",
        "PaintCalculator" => _localization.GetString("CalcPaint") ?? "Paint",
        "FlooringCalculator" => _localization.GetString("CalcFlooring") ?? "Flooring",
        "ConcreteCalculator" => _localization.GetString("CalcConcrete") ?? "Concrete",
        "DrywallCalculator" => _localization.GetString("CategoryDrywall") ?? "Drywall",
        "ElectricalCalculator" => _localization.GetString("CategoryElectrical") ?? "Electrical",
        "MetalCalculator" => _localization.GetString("CategoryMetal") ?? "Metal",
        "GardenCalculator" => _localization.GetString("CategoryGarden") ?? "Garden",
        "RoofSolarCalculator" => _localization.GetString("CategoryRoofSolar") ?? "Roof & Solar",
        "StairsCalculator" => _localization.GetString("CalcStairs") ?? "Stairs",
        "PlasterCalculator" => _localization.GetString("CalcPlaster") ?? "Plaster",
        "ScreedCalculator" => _localization.GetString("CalcScreed") ?? "Screed",
        "InsulationCalculator" => _localization.GetString("CalcInsulation") ?? "Insulation",
        "CableSizingCalculator" => _localization.GetString("CalcCableSizing") ?? "Cable Sizing",
        "GroutCalculator" => _localization.GetString("CalcGrout") ?? "Grout",
        _ => calculatorId
    };

    private static string GetCalculatorIcon(string calculatorId) => calculatorId switch
    {
        "TileCalculator" => "ViewDashboardOutline",
        "WallpaperCalculator" => "WallpaperOutline",
        "PaintCalculator" => "FormatPaint",
        "FlooringCalculator" => "FloorPlan",
        "ConcreteCalculator" => "CubeOutline",
        "DrywallCalculator" => "Wall",
        "ElectricalCalculator" => "LightningBolt",
        "MetalCalculator" => "HexagonOutline",
        "GardenCalculator" => "Flower",
        "RoofSolarCalculator" => "SolarPanel",
        "StairsCalculator" => "Stairs",
        "PlasterCalculator" => "FormatPaint",
        "ScreedCalculator" => "Layers",
        "InsulationCalculator" => "Snowflake",
        "CableSizingCalculator" => "CableData",
        "GroutCalculator" => "Texture",
        _ => "Calculator"
    };

    private static string? GetRouteForCalculator(string calculatorId) => calculatorId switch
    {
        "TileCalculator" => "TileCalculatorPage",
        "WallpaperCalculator" => "WallpaperCalculatorPage",
        "PaintCalculator" => "PaintCalculatorPage",
        "FlooringCalculator" => "FlooringCalculatorPage",
        "ConcreteCalculator" => "ConcretePage",
        "DrywallCalculator" => "DrywallPage",
        "ElectricalCalculator" => "ElectricalPage",
        "MetalCalculator" => "MetalPage",
        "GardenCalculator" => "GardenPage",
        "RoofSolarCalculator" => "RoofSolarPage",
        "StairsCalculator" => "StairsPage",
        "PlasterCalculator" => "PlasterPage",
        "ScreedCalculator" => "ScreedPage",
        "InsulationCalculator" => "InsulationPage",
        "CableSizingCalculator" => "CableSizingPage",
        "GroutCalculator" => "GroutPage",
        _ => null
    };
}

/// <summary>
/// Gruppierung von History-Einträgen nach Rechner-Typ
/// </summary>
public class CalculationHistoryGroup
{
    public string DisplayName { get; }
    public string IconKind { get; }
    public List<CalculationHistoryItem> Items { get; }
    public int Count => Items.Count;

    public CalculationHistoryGroup(string displayName, string iconKind, List<CalculationHistoryItem> items)
    {
        DisplayName = displayName;
        IconKind = iconKind;
        Items = items;
    }
}
