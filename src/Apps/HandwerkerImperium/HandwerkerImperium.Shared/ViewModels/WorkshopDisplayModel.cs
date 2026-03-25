using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Display-Model für Workshop-Karten im Dashboard.
/// Entkoppelt die UI-Darstellung vom persistierten Workshop-Model.
/// </summary>
public partial class WorkshopDisplayModel : ObservableObject
{
    public WorkshopType Type { get; set; }
    public string Icon { get; set; } = "";
    public GameIconKind IconKind { get; set; } = GameIconKind.Wrench;
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int WorkerCount { get; set; }
    public int MaxWorkers { get; set; }
    public decimal IncomePerSecond { get; set; }
    public decimal UpgradeCost { get; set; }
    public decimal HireWorkerCost { get; set; }
    public bool IsUnlocked { get; set; }
    public int UnlockLevel { get; set; }
    public bool CanUpgrade { get; set; }
    public bool CanHireWorker { get; set; }

    [ObservableProperty]
    private bool _canAffordUpgrade;

    [ObservableProperty]
    private bool _canAffordWorker;

    public int RequiredPrestige { get; set; }
    public decimal UnlockCost { get; set; }
    public string UnlockDisplay { get; set; } = "";
    public string UnlockCostDisplay => MoneyFormatter.FormatCompact(UnlockCost);

    /// <summary>Ob das Level für die Freischaltung erreicht ist (aber noch nicht gekauft).</summary>
    public bool CanBuyUnlock { get; set; }

    /// <summary>Ob genug Geld für die Freischaltung vorhanden ist.</summary>
    [ObservableProperty]
    private bool _canAffordUnlock;

    /// <summary>Bulk-Buy Gesamtkosten (gesetzt von RefreshWorkshops basierend auf BulkBuyAmount).</summary>
    public decimal BulkUpgradeCost { get; set; }

    /// <summary>Beschriftung auf dem Upgrade-Button (z.B. "x10" oder "Max (47)").</summary>
    public string BulkUpgradeLabel { get; set; } = "";

    /// <summary>Vorschau der Einkommens-Steigerung nach Upgrade (z.B. "+1,5 €/s").</summary>
    public string UpgradeIncomePreview { get; set; } = "";

    /// <summary>Geschätzte Zeit bis zum nächsten Upgrade. Leer wenn sofort leistbar.</summary>
    public string TimeToUpgrade { get; set; } = "";

    /// <summary>Netto-Einkommen pro Sekunde (Brutto - Kosten), formatiert.</summary>
    public string NetIncomeDisplay { get; set; } = "";

    /// <summary>Ob das Netto-Einkommen negativ ist (Verlust).</summary>
    public bool IsNetNegative { get; set; }

    /// <summary>Ob der Workshop laufende Kosten hat (Worker vorhanden oder Level > 1).</summary>
    public bool HasCosts { get; set; }

    public string WorkerDisplay => $"{WorkerCount}x";
    public string IncomeDisplay => IncomePerSecond > 0 ? MoneyFormatter.FormatPerSecond(IncomePerSecond, 1) : "-";
    public string UpgradeCostDisplay => MoneyFormatter.FormatCompact(BulkUpgradeCost > 0 ? BulkUpgradeCost : UpgradeCost);
    public string HireCostDisplay => MoneyFormatter.FormatCompact(HireWorkerCost);
    public double LevelProgress => Level / (double)Workshop.MaxLevel;

    // Level-basierte Farb-Intensität für Workshop-Streifen
    public double ColorIntensity => !IsUnlocked
        ? (CanBuyUnlock ? 0.30 : 0.10)
        : Level switch
        {
            >= 1000 => 1.00,
            >= 500 => 0.85,
            >= 250 => 0.70,
            >= 100 => 0.55,
            >= 50 => 0.45,
            >= 25 => 0.35,
            _ => 0.20
        };

    public bool IsMaxLevel => Level >= Workshop.MaxLevel;

    // Dynamischer BoxShadow: Max-Level Gold-Glow, leistbar dezenter Glow, freischaltbar Craft-Glow
    public string GlowShadow => IsMaxLevel
        ? "0 0 12 0 #60FFD700"
        : CanAffordUpgrade && IsUnlocked
            ? "0 0 8 0 #40D97706"
            : CanAffordUnlock && !IsUnlocked
                ? "0 0 10 0 #50E8A00E"
                : "none";

    // "Fast geschafft" Puls wenn >= 80% des Upgrade-Preises vorhanden
    public bool IsAlmostAffordable => !CanAffordUpgrade && IsUnlocked && UpgradeCost > 0;

    // Milestone-System
    private static readonly int[] Milestones = [25, 50, 75, 100, 150, 200, 225, 250, 350, 500, 1000];

    public int NextMilestone
    {
        get
        {
            foreach (var m in Milestones)
                if (Level < m) return m;
            return 0;
        }
    }

    public double MilestoneProgress
    {
        get
        {
            int prev = 1;
            foreach (var m in Milestones)
            {
                if (Level < m)
                    return (Level - prev) / (double)(m - prev);
                prev = m;
            }
            return 1.0;
        }
    }

    public string MilestoneDisplay => NextMilestone > 0 ? $"\u2192 Lv.{NextMilestone}" : "";
    public bool ShowMilestone => IsUnlocked && NextMilestone > 0;

    /// <summary>Anzahl der Rebirth-Sterne dieses Workshops (0-5).</summary>
    public int RebirthStars { get; set; }

    /// <summary>Benachrichtigt die UI über alle Property-Änderungen nach einem In-Place-Update.</summary>
    public void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(WorkerCount));
        OnPropertyChanged(nameof(MaxWorkers));
        OnPropertyChanged(nameof(IncomePerSecond));
        OnPropertyChanged(nameof(UpgradeCost));
        OnPropertyChanged(nameof(HireWorkerCost));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(UnlockDisplay));
        OnPropertyChanged(nameof(UnlockCost));
        OnPropertyChanged(nameof(UnlockCostDisplay));
        OnPropertyChanged(nameof(CanBuyUnlock));
        OnPropertyChanged(nameof(CanAffordUnlock));
        OnPropertyChanged(nameof(CanUpgrade));
        OnPropertyChanged(nameof(CanHireWorker));
        OnPropertyChanged(nameof(WorkerDisplay));
        OnPropertyChanged(nameof(IncomeDisplay));
        OnPropertyChanged(nameof(UpgradeCostDisplay));
        OnPropertyChanged(nameof(HireCostDisplay));
        OnPropertyChanged(nameof(LevelProgress));
        OnPropertyChanged(nameof(ColorIntensity));
        OnPropertyChanged(nameof(IsMaxLevel));
        OnPropertyChanged(nameof(GlowShadow));
        OnPropertyChanged(nameof(IsAlmostAffordable));
        OnPropertyChanged(nameof(NextMilestone));
        OnPropertyChanged(nameof(MilestoneProgress));
        OnPropertyChanged(nameof(MilestoneDisplay));
        OnPropertyChanged(nameof(ShowMilestone));
        OnPropertyChanged(nameof(BulkUpgradeCost));
        OnPropertyChanged(nameof(BulkUpgradeLabel));
        OnPropertyChanged(nameof(UpgradeIncomePreview));
        OnPropertyChanged(nameof(TimeToUpgrade));
        OnPropertyChanged(nameof(NetIncomeDisplay));
        OnPropertyChanged(nameof(IsNetNegative));
        OnPropertyChanged(nameof(HasCosts));
        OnPropertyChanged(nameof(RebirthStars));
    }
}
