using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>
/// V7 (Phase 4 Ressourcen-Plan, Plan Section 3.9): ViewModel fuer den Mega-Projekt-Bauplatz
/// in der GuildView. Zeigt Material-Anforderungen, Fortschritt, Top-Spender-Leaderboard
/// und Spende-UI fuer einzelne Materialien.
/// </summary>
public sealed partial class GuildMegaProjectViewModel : ViewModelBase, IDisposable
{
    private readonly IGuildMegaProjectService _megaService;
    private readonly IGameStateService _gameState;
    private readonly IWarehouseService _warehouse;
    private readonly ILocalizationService _localization;
    private readonly DispatcherTimer _refreshTimer;
    private bool _disposed;

    [ObservableProperty]
    private GuildMegaProject? _currentProject;

    [ObservableProperty]
    private bool _hasActiveProject;

    [ObservableProperty]
    private string _projectName = "";

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private double _progressRatio;

    [ObservableProperty]
    private ObservableCollection<MegaProjectRequirementDisplay> _requirements = new();

    [ObservableProperty]
    private ObservableCollection<MegaProjectDonationDisplay> _topDonors = new();

    [ObservableProperty]
    private bool _isStarting;

    [ObservableProperty]
    private string _bonusText = "";

    public GuildMegaProjectViewModel(
        IGuildMegaProjectService megaService,
        IGameStateService gameState,
        IWarehouseService warehouse,
        ILocalizationService localization)
    {
        _megaService = megaService;
        _gameState = gameState;
        _warehouse = warehouse;
        _localization = localization;

        _megaService.ProjectUpdated += OnProjectUpdated;
        _megaService.ProjectCompleted += OnProjectCompleted;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(false);
    }

    public void Start()
    {
        if (_disposed) return;
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    public void Stop()
    {
        _refreshTimer.Stop();
    }

    private void OnProjectUpdated(GuildMegaProject project)
        => Dispatcher.UIThread.Post(() => ApplyProject(project));

    private void OnProjectCompleted(GuildMegaProject project)
        => Dispatcher.UIThread.Post(() => ApplyProject(project));

    public async Task RefreshAsync()
    {
        var project = await _megaService.GetActiveProjectAsync().ConfigureAwait(false);
        Dispatcher.UIThread.Post(() => ApplyProject(project));
    }

    private void ApplyProject(GuildMegaProject? project)
    {
        CurrentProject = project;
        HasActiveProject = project != null;

        if (project == null)
        {
            ProjectName = "";
            ProgressText = "";
            ProgressRatio = 0;
            Requirements = new ObservableCollection<MegaProjectRequirementDisplay>();
            TopDonors = new ObservableCollection<MegaProjectDonationDisplay>();
            BonusText = "";
            return;
        }

        var nameKey = GuildMegaProjectTemplates.GetNameKey(project.Type);
        ProjectName = _localization.GetString(nameKey) ?? nameKey;

        // Material-Anforderungen
        var requirements = GuildMegaProjectTemplates.GetRequirements(project.Type);
        var allProducts = CraftingProduct.GetAllProducts();
        var reqList = new ObservableCollection<MegaProjectRequirementDisplay>();
        int totalNeeded = 0;
        int totalDonated = 0;
        foreach (var (productId, needed) in requirements)
        {
            int donated = project.Contributions.GetValueOrDefault(productId, 0);
            int available = _warehouse.GetAvailable(productId);
            string name = allProducts.TryGetValue(productId, out var p)
                ? _localization.GetString(p.NameKey) ?? p.NameKey
                : productId;
            reqList.Add(new MegaProjectRequirementDisplay
            {
                ProductId = productId,
                Name = name,
                Icon = GetProductIcon(productId),
                Tier = p?.Tier ?? 0,
                Needed = needed,
                Donated = donated,
                Available = available,
                ProgressText = $"{donated} / {needed}",
                ProgressRatio = Math.Clamp((double)donated / needed, 0, 1),
                CanDonate = !project.IsCompleted && available > 0 && donated < needed
            });
            totalNeeded += needed;
            totalDonated += Math.Min(donated, needed);
        }
        Requirements = reqList;
        ProgressRatio = totalNeeded > 0 ? Math.Clamp((double)totalDonated / totalNeeded, 0, 1) : 0;
        ProgressText = $"{(int)(ProgressRatio * 100)}%";

        // Top-Spender (sortiert nach TotalValue absteigend, Top 5)
        var topDonors = new ObservableCollection<MegaProjectDonationDisplay>();
        var sortedDonors = project.Donations
            .OrderByDescending(kv => kv.Value.TotalValue)
            .Take(5);
        int rank = 1;
        foreach (var (playerId, donation) in sortedDonors)
        {
            topDonors.Add(new MegaProjectDonationDisplay
            {
                Rank = rank++,
                PlayerName = donation.PlayerName,
                ValueDisplay = MoneyFormatter.Format(donation.TotalValue, 0),
                ItemCount = donation.ItemCount
            });
        }
        TopDonors = topDonors;

        // Bonus-Vorschau
        var reward = GuildMegaProjectTemplates.GetReward(project.Type);
        BonusText = string.Format(
            _localization.GetString("MegaProjectRewardFormat") ?? "+{0}% craft speed, +{1}% auto-sell, +{2} slots",
            (int)(reward.CraftingSpeedBonus * 100),
            (int)(reward.AutoSellPriceBonus * 100),
            reward.BonusWarehouseSlots);
    }

    [RelayCommand]
    private async Task StartCathedralAsync()
    {
        if (IsStarting) return;
        try
        {
            IsStarting = true;
            await _megaService.StartProjectAsync(GuildMegaProjectType.Cathedral).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally { IsStarting = false; }
    }

    [RelayCommand]
    private async Task StartHeadquartersAsync()
    {
        if (IsStarting) return;
        try
        {
            IsStarting = true;
            await _megaService.StartProjectAsync(GuildMegaProjectType.Headquarters).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally { IsStarting = false; }
    }

    [RelayCommand]
    private async Task Donate1Async(MegaProjectRequirementDisplay? req)
    {
        if (req == null) return;
        await _megaService.DonateAsync(req.ProductId, 1).ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task Donate10Async(MegaProjectRequirementDisplay? req)
    {
        if (req == null) return;
        await _megaService.DonateAsync(req.ProductId, 10).ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DonateAllAsync(MegaProjectRequirementDisplay? req)
    {
        if (req == null) return;
        int amount = Math.Min(req.Available, req.Needed - req.Donated);
        if (amount > 0)
            await _megaService.DonateAsync(req.ProductId, amount).ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ClaimRewardAsync()
    {
        await _megaService.TryClaimRewardAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    private static GameIconKind GetProductIcon(string productId) => productId switch
    {
        "planks" => GameIconKind.Forest,
        "furniture" => GameIconKind.SeatOutline,
        "luxury_furniture" => GameIconKind.Crown,
        "pipes" => GameIconKind.Pipe,
        "plumbing_system" => GameIconKind.Water,
        "bathroom_installation" => GameIconKind.ShowerHead,
        "cables" => GameIconKind.CableData,
        "circuit" => GameIconKind.Chip,
        "smart_home" => GameIconKind.HomeAutomation,
        "paint_mix" => GameIconKind.Palette,
        "wall_design" => GameIconKind.FormatPaint,
        "artwork" => GameIconKind.Palette,
        "roof_tiles" => GameIconKind.ViewGrid,
        "roofing_system" => GameIconKind.HomeRoof,
        "roof_structure" => GameIconKind.HomeRoof,
        "concrete" => GameIconKind.Wall,
        "concrete_foundation" => GameIconKind.OfficeBuildingOutline,
        "skyscraper_frame" => GameIconKind.OfficeBuilding,
        "blueprint" => GameIconKind.Compass,
        "framework" => GameIconKind.DomainPlus,
        "master_blueprint" => GameIconKind.City,
        "contract" => GameIconKind.FileDocumentCheck,
        "contract_complex" => GameIconKind.FileDocumentCheck,
        "general_contract" => GameIconKind.Bank,
        "fittings" => GameIconKind.Anvil,
        "master_fittings" => GameIconKind.HammerWrench,
        "masterpiece_fittings" => GameIconKind.Trophy,
        "prototype" => GameIconKind.LightbulbOnOutline,
        "innovation" => GameIconKind.LightbulbOn,
        "patent" => GameIconKind.StarFourPoints,
        "villa" => GameIconKind.HomeCity,
        "skyscraper" => GameIconKind.OfficeBuilding,
        "imperium_hq" => GameIconKind.Bank,
        _ => GameIconKind.PackageVariant
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Stop();
        _megaService.ProjectUpdated -= OnProjectUpdated;
        _megaService.ProjectCompleted -= OnProjectCompleted;
    }
}

public class MegaProjectRequirementDisplay
{
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public GameIconKind Icon { get; set; } = GameIconKind.PackageVariant;
    public int Tier { get; set; }
    public int Needed { get; set; }
    public int Donated { get; set; }
    public int Available { get; set; }
    public string ProgressText { get; set; } = "";
    public double ProgressRatio { get; set; }
    public bool CanDonate { get; set; }
}

public class MegaProjectDonationDisplay
{
    public int Rank { get; set; }
    public string PlayerName { get; set; } = "";
    public string ValueDisplay { get; set; } = "";
    public int ItemCount { get; set; }
}
