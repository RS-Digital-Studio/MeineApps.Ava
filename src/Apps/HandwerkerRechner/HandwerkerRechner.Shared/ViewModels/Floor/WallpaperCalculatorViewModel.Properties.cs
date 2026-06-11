using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;
using MeineApps.Core.Ava.Services;

namespace HandwerkerRechner.ViewModels.Floor;

public sealed partial class WallpaperCalculatorViewModel
{
    #region Input Properties

    [ObservableProperty]
    private double _wallLength = 14.0;

    [ObservableProperty]
    private double _roomHeight = 2.5;

    [ObservableProperty]
    private double _rollLength = 10.05;

    [ObservableProperty]
    private double _rollWidth = 53;

    [ObservableProperty]
    private double _patternRepeat = 0;

    // Tür-/Fenster-Abzüge (optional)
    [ObservableProperty] private bool _showDeductions;
    [ObservableProperty] private int _doorCount;
    [ObservableProperty] private double _doorWidth = 0.8;
    [ObservableProperty] private double _doorHeight = 2.0;
    [ObservableProperty] private int _windowCount;
    [ObservableProperty] private double _windowWidth = 1.2;
    [ObservableProperty] private double _windowHeight = 1.0;

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnWallLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnRoomHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnRollLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnRollWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnPatternRepeatChanged(double value) => ScheduleAutoCalculate();

    // Live-Berechnung bei Abzugs-Änderungen
    partial void OnShowDeductionsChanged(bool value) => ScheduleAutoCalculate();
    partial void OnDoorCountChanged(int value) => ScheduleAutoCalculate();
    partial void OnDoorWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnDoorHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnWindowCountChanged(int value) => ScheduleAutoCalculate();
    partial void OnWindowWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnWindowHeightChanged(double value) => ScheduleAutoCalculate();

    #endregion

    #region Unit Labels

    public string LengthUnit => _unitConverter.GetLengthUnit();
    public string AreaUnit => _unitConverter.GetAreaUnit();
    public string RollWidthUnit => _unitConverter.CurrentSystem == UnitSystem.Metric ? "cm" : "in";

    private void OnUnitSystemChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(LengthUnit));
        OnPropertyChanged(nameof(AreaUnit));
        OnPropertyChanged(nameof(RollWidthUnit));
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(RollsNeededDisplay));
        OnPropertyChanged(nameof(StripsNeededDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion

    #region Cost Calculation

    [ObservableProperty]
    private double _pricePerRoll = 0;

    [ObservableProperty]
    private bool _showCost = false;

    public string TotalCostDisplay => (Result != null && ShowCost && PricePerRoll > 0)
        ? $"{(Result.RollsNeeded * PricePerRoll):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string PricePerDisplay => ShowCost ? $"{_localization.GetString("PricePerRoll")}: {PricePerRoll:F2} {_localization.GetString("CurrencySymbol")}" : "";

    partial void OnPricePerRollChanged(double value)
    {
        ShowCost = value > 0;
        OnPropertyChanged(nameof(TotalCostDisplay));
        OnPropertyChanged(nameof(PricePerDisplay));
        ScheduleAutoCalculate();
    }

    #endregion

    #region Result Properties

    [ObservableProperty]
    private WallpaperResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string _deductedAreaDisplay = "";

    public string AreaDisplay => Result != null
        ? _unitConverter.FormatArea(Result.WallArea)
        : "";

    public string RollsNeededDisplay => Result != null
        ? $"{Result.RollsNeeded} {_localization.GetString("UnitRolls")}"
        : "";

    public string StripsNeededDisplay => Result != null
        ? $"{Result.StripsNeeded} {_localization.GetString("UnitStrips")}"
        : "";

    partial void OnResultChanged(WallpaperResult? value)
    {
        OnPropertyChanged(nameof(AreaDisplay));
        OnPropertyChanged(nameof(RollsNeededDisplay));
        OnPropertyChanged(nameof(StripsNeededDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    #endregion
}
