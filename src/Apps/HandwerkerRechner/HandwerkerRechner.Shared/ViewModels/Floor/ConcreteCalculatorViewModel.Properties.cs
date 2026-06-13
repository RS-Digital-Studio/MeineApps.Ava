using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Floor;

public sealed partial class ConcreteCalculatorViewModel
{
    #region Sub-Rechner Auswahl

    [ObservableProperty]
    private int _selectedCalculator;

    partial void OnSelectedCalculatorChanged(int value)
    {
        // Kosten zurücksetzen beim Wechsel, um State-Leaking zu vermeiden
        PricePerBag = 0;
        PricePerCubicMeter = 0;
        // Ergebnisse zurücksetzen
        HasResult = false;
        // Sichtbarkeit der Sub-Rechner aktualisieren
        OnPropertyChanged(nameof(IsSlabSelected));
        OnPropertyChanged(nameof(IsStripSelected));
        OnPropertyChanged(nameof(IsColumnSelected));
    }

    public List<string> Calculators =>
    [
        _localization.GetString("ConcreteSlab"),
        _localization.GetString("StripFoundation"),
        _localization.GetString("ConcreteColumn")
    ];

    /// <summary>Sub-Rechner Sichtbarkeit: Platte</summary>
    public bool IsSlabSelected => SelectedCalculator == 0;

    /// <summary>Sub-Rechner Sichtbarkeit: Streifenfundament</summary>
    public bool IsStripSelected => SelectedCalculator == 1;

    /// <summary>Sub-Rechner Sichtbarkeit: Säule</summary>
    public bool IsColumnSelected => SelectedCalculator == 2;

    /// <summary>Verfügbare Sackgewichte für Fertigbeton</summary>
    public List<double> BagWeights => [25, 40];

    #endregion

    #region Input Properties

    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnSlabLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnSlabWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnSlabHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnStripLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnStripWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnStripDepthChanged(double value) => ScheduleAutoCalculate();
    partial void OnColumnDiameterChanged(double value) => ScheduleAutoCalculate();
    partial void OnColumnHeightChanged(double value) => ScheduleAutoCalculate();
    partial void OnBagWeightChanged(double value) => ScheduleAutoCalculate();

    // Platte (Länge m, Breite m, Höhe cm)
    [ObservableProperty]
    private double _slabLength = 4;

    [ObservableProperty]
    private double _slabWidth = 3;

    [ObservableProperty]
    private double _slabHeight = 15;

    // Streifenfundament (Gesamtlänge m, Breite cm, Tiefe cm)
    [ObservableProperty]
    private double _stripLength = 20;

    [ObservableProperty]
    private double _stripWidth = 30;

    [ObservableProperty]
    private double _stripDepth = 80;

    // Säule (Durchmesser cm, Höhe cm)
    [ObservableProperty]
    private double _columnDiameter = 30;

    [ObservableProperty]
    private double _columnHeight = 250;

    // Gemeinsam: Sackgewicht (25 oder 40 kg Fertigbeton)
    [ObservableProperty]
    private double _bagWeight = 25;

    #endregion

    #region Unit Labels

    public string LengthUnit => _unitConverter.GetLengthUnit();

    private void OnUnitSystemChanged(object? sender, EventArgs e)
    {
        // Alle einheitenabhängigen Anzeige-Properties aktualisieren
        OnPropertyChanged(nameof(LengthUnit));
        OnPropertyChanged(nameof(VolumeDisplay));
        OnPropertyChanged(nameof(CementDisplay));
        OnPropertyChanged(nameof(SandDisplay));
        OnPropertyChanged(nameof(GravelDisplay));
        OnPropertyChanged(nameof(WaterDisplay));
        OnPropertyChanged(nameof(BagsDisplay));
        OnPropertyChanged(nameof(BagCostDisplay));
        OnPropertyChanged(nameof(CubicMeterCostDisplay));
    }

    #endregion

    #region Cost Calculation

    // Preis pro Sack Fertigbeton
    [ObservableProperty]
    private double _pricePerBag = 0;

    [ObservableProperty]
    private bool _showBagCost = false;

    // Preis pro m³ Fertigbeton
    [ObservableProperty]
    private double _pricePerCubicMeter = 0;

    [ObservableProperty]
    private bool _showCubicMeterCost = false;

    public string BagCostDisplay => (ShowBagCost && PricePerBag > 0 && Result != null && Result.BagsNeeded > 0)
        ? $"{(Result.BagsNeeded * PricePerBag):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string CubicMeterCostDisplay => (ShowCubicMeterCost && PricePerCubicMeter > 0 && Result != null && Result.VolumeM3 > 0)
        ? $"{(Result.VolumeM3 * PricePerCubicMeter):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerBagChanged(double value)
    {
        ShowBagCost = value > 0;
        OnPropertyChanged(nameof(BagCostDisplay));
        ScheduleAutoCalculate();
    }

    partial void OnPricePerCubicMeterChanged(double value)
    {
        ShowCubicMeterCost = value > 0;
        OnPropertyChanged(nameof(CubicMeterCostDisplay));
        ScheduleAutoCalculate();
    }

    #endregion

    #region Result Properties

    [ObservableProperty]
    private ConcreteResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _isExporting;

    public string VolumeDisplay => Result != null
        ? $"{Result.VolumeM3:F2} m³"
        : "";

    public string CementDisplay => Result != null
        ? $"{Result.CementKg:F1} kg"
        : "";

    public string SandDisplay => Result != null
        ? $"{Result.SandKg:F1} kg"
        : "";

    public string GravelDisplay => Result != null
        ? $"{Result.GravelKg:F1} kg"
        : "";

    public string WaterDisplay => Result != null
        ? $"{Result.WaterLiters:F1} L"
        : "";

    public string BagsDisplay => Result != null
        ? $"{Result.BagsNeeded} × {Result.BagWeight} kg"
        : "";

    partial void OnResultChanged(ConcreteResult? value)
    {
        OnPropertyChanged(nameof(VolumeDisplay));
        OnPropertyChanged(nameof(CementDisplay));
        OnPropertyChanged(nameof(SandDisplay));
        OnPropertyChanged(nameof(GravelDisplay));
        OnPropertyChanged(nameof(WaterDisplay));
        OnPropertyChanged(nameof(BagsDisplay));
        OnPropertyChanged(nameof(BagCostDisplay));
        OnPropertyChanged(nameof(CubicMeterCostDisplay));
    }

    #endregion
}
