using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the worker detail/management screen.
/// Shows worker stats (tier, mood, fatigue, efficiency, XP) and allows
/// training, resting, giving bonuses, firing, and transferring workers.
/// </summary>
public partial class WorkerProfileViewModel : ObservableObject
{
    private readonly IWorkerService _workerService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;

    private string? _workerId;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event EventHandler<string>? NavigationRequested;

    /// <summary>
    /// Event to show an alert dialog. Parameters: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    /// <summary>
    /// Event to request a confirmation dialog.
    /// Parameters: title, message, acceptText, cancelText. Returns bool.
    /// </summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private Worker? _worker;

    [ObservableProperty]
    private string _workerName = string.Empty;

    [ObservableProperty]
    private string _tierDisplay = string.Empty;

    [ObservableProperty]
    private string _moodDisplay = string.Empty;

    [ObservableProperty]
    private string _fatigueDisplay = string.Empty;

    [ObservableProperty]
    private string _efficiencyDisplay = string.Empty;

    [ObservableProperty]
    private string _incomeContributionDisplay = string.Empty;

    [ObservableProperty]
    private string _xpProgress = string.Empty;

    [ObservableProperty]
    private string _personalityDisplay = string.Empty;

    [ObservableProperty]
    private string _specializationDisplay = string.Empty;

    [ObservableProperty]
    private string _wageDisplay = string.Empty;

    [ObservableProperty]
    private string _assignedWorkshopDisplay = string.Empty;

    [ObservableProperty]
    private string _statusDisplay = string.Empty;

    [ObservableProperty]
    private double _moodPercent;

    [ObservableProperty]
    private double _fatiguePercent;

    [ObservableProperty]
    private double _xpPercent;

    [ObservableProperty]
    private bool _isTraining;

    [ObservableProperty]
    private bool _isResting;

    [ObservableProperty]
    private bool _isWorking;

    [ObservableProperty]
    private bool _canStartTraining;

    [ObservableProperty]
    private bool _canStartResting;

    [ObservableProperty]
    private bool _canGiveBonus;

    [ObservableProperty]
    private List<WorkshopTransferItem> _availableWorkshops = [];

    [ObservableProperty]
    private string _trainingTimeDisplay = string.Empty;

    [ObservableProperty]
    private string _trainingCostDisplay = string.Empty;

    [ObservableProperty]
    private string _restTimeDisplay = string.Empty;

    [ObservableProperty]
    private bool _showTrainingInfo;

    [ObservableProperty]
    private bool _showRestInfo;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public WorkerProfileViewModel(
        IWorkerService workerService,
        IGameStateService gameStateService,
        ILocalizationService localizationService)
    {
        _workerService = workerService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loads a worker by ID and refreshes all display properties.
    /// </summary>
    public void SetWorker(string workerId)
    {
        _workerId = workerId;
        var worker = _workerService.GetWorker(workerId);
        if (worker == null) return;

        Worker = worker;
        RefreshDisplayProperties();
        LoadAvailableWorkshops();
    }

    /// <summary>
    /// Refreshes display properties from current worker state.
    /// Called after actions or periodically from game loop.
    /// </summary>
    public void RefreshDisplayProperties()
    {
        if (Worker == null) return;

        WorkerName = Worker.Name;
        TierDisplay = $"{Worker.Tier} - {_localizationService.GetString(Worker.Tier.GetLocalizationKey())}";
        MoodDisplay = $"{Worker.Mood:F0}%";
        FatigueDisplay = $"{Worker.Fatigue:F0}%";
        EfficiencyDisplay = $"{Worker.EffectiveEfficiency:P0}";

        // Einkommensbeitrag berechnen wenn einem Workshop zugewiesen
        if (Worker.AssignedWorkshop != null)
        {
            var workshop = _gameStateService.State.Workshops
                .FirstOrDefault(w => w.Type == Worker.AssignedWorkshop);
            if (workshop != null)
            {
                var contribution = workshop.BaseIncomePerWorker * Worker.EffectiveEfficiency;
                IncomeContributionDisplay = $"+{contribution:N2} €/s";
            }
            else
            {
                IncomeContributionDisplay = "-";
            }
        }
        else
        {
            IncomeContributionDisplay = "-";
        }
        XpProgress = $"{Worker.ExperienceXp}/{Worker.XpForNextLevel} XP (Lv.{Worker.ExperienceLevel})";
        PersonalityDisplay = $"{Worker.Personality.GetIcon()} {_localizationService.GetString(Worker.Personality.GetLocalizationKey())}";
        WageDisplay = $"{MoneyFormatter.Format(Worker.WagePerHour, 0)}/h";

        if (Worker.Specialization != null)
        {
            var specKey = Worker.Specialization.Value.GetLocalizationKey();
            SpecializationDisplay = $"{Worker.Specialization.Value.GetIcon()} {_localizationService.GetString(specKey)}";
        }
        else
        {
            SpecializationDisplay = _localizationService.GetString("NoSpecialization");
        }

        if (Worker.AssignedWorkshop != null)
        {
            var wsKey = Worker.AssignedWorkshop.Value.GetLocalizationKey();
            AssignedWorkshopDisplay = $"{Worker.AssignedWorkshop.Value.GetIcon()} {_localizationService.GetString(wsKey)}";
        }
        else
        {
            AssignedWorkshopDisplay = _localizationService.GetString("Unassigned");
        }

        // Prozentwerte fuer Progress-Bars
        MoodPercent = (double)Worker.Mood;
        FatiguePercent = (double)Worker.Fatigue;
        XpPercent = Worker.XpForNextLevel > 0
            ? (double)Worker.ExperienceXp / Worker.XpForNextLevel * 100.0
            : 0.0;

        // Status
        IsTraining = Worker.IsTraining;
        IsResting = Worker.IsResting;
        IsWorking = Worker.IsWorking;

        if (Worker.IsTraining)
            StatusDisplay = _localizationService.GetString("StatusTraining");
        else if (Worker.IsResting)
            StatusDisplay = _localizationService.GetString("StatusResting");
        else if (Worker.IsTired)
            StatusDisplay = _localizationService.GetString("StatusExhausted");
        else if (Worker.IsWorking)
            StatusDisplay = _localizationService.GetString("StatusWorking");
        else
            StatusDisplay = _localizationService.GetString("StatusIdle");

        // Button-Zustaende
        CanStartTraining = !Worker.IsTraining && !Worker.IsResting;
        CanStartResting = !Worker.IsResting && !Worker.IsTraining;
        CanGiveBonus = _gameStateService.CanAfford(Worker.WagePerHour * 24m);

        // Training-Info: Dauer bis zum nächsten Level + Kosten pro Stunde
        ShowTrainingInfo = !Worker.IsTraining && CanStartTraining;
        if (ShowTrainingInfo && Worker.ExperienceLevel < 10)
        {
            int xpRemaining = Worker.XpForNextLevel - Worker.ExperienceXp;
            decimal xpPerHour = Worker.TrainingXpPerHour * Worker.Personality.GetXpMultiplier();
            decimal hoursNeeded = xpPerHour > 0 ? xpRemaining / xpPerHour : 0;
            TrainingTimeDisplay = string.Format(
                _localizationService.GetString("TrainingDuration"),
                FormatDuration(hoursNeeded),
                Worker.ExperienceLevel + 1);
            TrainingCostDisplay = string.Format(
                _localizationService.GetString("TrainingCost"),
                MoneyFormatter.Format(Worker.TrainingCostPerHour, 0));
        }
        else
        {
            TrainingTimeDisplay = string.Empty;
            TrainingCostDisplay = string.Empty;
            ShowTrainingInfo = false;
        }

        // Rest-Info: Dauer bis vollständig erholt
        ShowRestInfo = !Worker.IsResting && Worker.Fatigue > 0;
        if (ShowRestInfo)
        {
            decimal recoveryPerHour = Worker.RestHoursNeeded > 0 ? 100m / Worker.RestHoursNeeded : 100m;
            decimal hoursNeeded = recoveryPerHour > 0 ? Worker.Fatigue / recoveryPerHour : 0;
            RestTimeDisplay = string.Format(
                _localizationService.GetString("RestDuration"),
                FormatDuration(hoursNeeded));
        }
        else
        {
            RestTimeDisplay = string.Empty;
        }
    }

    /// <summary>
    /// Formatiert Dezimal-Stunden in lesbaren Text (z.B. "2h 30min").
    /// </summary>
    private static string FormatDuration(decimal hours)
    {
        int totalMinutes = (int)Math.Ceiling(hours * 60m);
        int h = totalMinutes / 60;
        int m = totalMinutes % 60;
        if (h > 0 && m > 0) return $"{h}h {m}min";
        if (h > 0) return $"{h}h";
        return $"{m}min";
    }

    /// <summary>
    /// Updates localized texts after language change.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        RefreshDisplayProperties();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartTraining()
    {
        if (_workerId == null) return;

        bool success = _workerService.StartTraining(_workerId);
        if (success)
        {
            RefreshDisplayProperties();
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("TrainingFailed"),
                _localizationService.GetString("TrainingFailedDesc"),
                "OK");
        }
    }

    [RelayCommand]
    private void StopTraining()
    {
        if (_workerId == null) return;

        _workerService.StopTraining(_workerId);
        RefreshDisplayProperties();
    }

    [RelayCommand]
    private void StartResting()
    {
        if (_workerId == null) return;

        bool success = _workerService.StartResting(_workerId);
        if (success)
        {
            RefreshDisplayProperties();
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("RestFailed"),
                _localizationService.GetString("RestFailedDesc"),
                "OK");
        }
    }

    [RelayCommand]
    private void StopResting()
    {
        if (_workerId == null) return;

        _workerService.StopResting(_workerId);
        RefreshDisplayProperties();
    }

    [RelayCommand]
    private async Task GiveBonusAsync()
    {
        if (_workerId == null || Worker == null) return;

        var cost = Worker.WagePerHour * 24m;
        var costText = MoneyFormatter.Format(cost, 0);

        bool confirm = true;
        if (ConfirmationRequested != null)
        {
            confirm = await ConfirmationRequested.Invoke(
                _localizationService.GetString("GiveBonus"),
                string.Format(_localizationService.GetString("GiveBonusConfirmFormat"), costText),
                _localizationService.GetString("Confirm"),
                _localizationService.GetString("Cancel"));
        }

        if (!confirm) return;

        bool success = _workerService.GiveBonus(_workerId);
        if (success)
        {
            RefreshDisplayProperties();
            AlertRequested?.Invoke(
                _localizationService.GetString("BonusGiven"),
                string.Format(_localizationService.GetString("BonusGivenFormat"), Worker.Name),
                _localizationService.GetString("Great"));
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                _localizationService.GetString("NotEnoughMoneyDesc"),
                "OK");
        }
    }

    [RelayCommand]
    private async Task FireWorkerAsync()
    {
        if (_workerId == null || Worker == null) return;

        bool confirm = false;
        if (ConfirmationRequested != null)
        {
            confirm = await ConfirmationRequested.Invoke(
                _localizationService.GetString("FireWorker"),
                string.Format(_localizationService.GetString("FireWorkerConfirmFormat"), Worker.Name),
                _localizationService.GetString("Fire"),
                _localizationService.GetString("Cancel"));
        }

        if (!confirm) return;

        bool success = _workerService.FireWorker(_workerId);
        if (success)
        {
            NavigationRequested?.Invoke(this, "..");
        }
    }

    [RelayCommand]
    private void TransferWorker(WorkshopType targetWorkshop)
    {
        if (_workerId == null) return;

        bool success = _workerService.TransferWorker(_workerId, targetWorkshop);
        if (success)
        {
            RefreshDisplayProperties();
            LoadAvailableWorkshops();
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("TransferFailed"),
                _localizationService.GetString("TransferFailedDesc"),
                "OK");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(this, "..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void LoadAvailableWorkshops()
    {
        var state = _gameStateService.State;
        var workshops = state.Workshops
            .Where(w => w.IsUnlocked && (Worker == null || w.Type != Worker.AssignedWorkshop))
            .Select(w => new WorkshopTransferItem
            {
                Type = w.Type,
                Name = $"{w.Type.GetIcon()} {_localizationService.GetString(w.Type.GetLocalizationKey())}",
                WorkerCount = w.Workers.Count
            })
            .ToList();

        AvailableWorkshops = workshops;
    }
}

/// <summary>
/// Display item for workshop transfer selection.
/// </summary>
public class WorkshopTransferItem
{
    public WorkshopType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public int WorkerCount { get; set; }
}
