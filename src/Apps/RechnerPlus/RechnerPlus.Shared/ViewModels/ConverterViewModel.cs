using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;

namespace RechnerPlus.ViewModels;

public partial class ConverterViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private CategoryItem? _selectedCategory;

    [ObservableProperty]
    private string _inputValue = "1";

    [ObservableProperty]
    private string _outputValue = "0";

    [ObservableProperty]
    private UnitItem? _fromUnit;

    [ObservableProperty]
    private UnitItem? _toUnit;

    [ObservableProperty]
    private List<UnitItem> _availableUnits = [];

    [ObservableProperty]
    private List<CategoryItem> _categories = [];

    // Localized strings for view bindings
    public string TitleText => _localization.GetString("ConverterTitle");
    public string CategoryText => _localization.GetString("ConverterCategory");
    public string FromText => _localization.GetString("ConverterFrom");
    public string ToText => _localization.GetString("ConverterTo");
    public string EnterValueText => _localization.GetString("ConverterEnterValue");
    public string SwapUnitsText => _localization.GetString("ConverterSwapUnits");
    public string InvalidInputText => _localization.GetString("ConverterInvalidInput");

    /// <summary>Event zum Kopieren des Converter-Ergebnisses. View handhabt die Clipboard-API.</summary>
    public event Func<string, Task>? ClipboardCopyRequested;

    public ConverterViewModel(ILocalizationService localization)
    {
        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;

        RebuildCategories();
        SelectedCategory = Categories.FirstOrDefault();
    }

    partial void OnSelectedCategoryChanged(CategoryItem? value)
    {
        if (value == null) return;
        RebuildUnits(value.Category);
        FromUnit = AvailableUnits.FirstOrDefault();
        ToUnit = AvailableUnits.Skip(1).FirstOrDefault();
        Convert();
    }

    partial void OnInputValueChanged(string value) => Convert();
    partial void OnFromUnitChanged(UnitItem? value) { if (!_suppressConvert) Convert(); }
    partial void OnToUnitChanged(UnitItem? value) { if (!_suppressConvert) Convert(); }

    private bool _suppressConvert;

    [RelayCommand]
    private void SwapUnits()
    {
        _suppressConvert = true;
        (FromUnit, ToUnit) = (ToUnit, FromUnit);
        _suppressConvert = false;
        Convert();
    }

    [RelayCommand]
    private async Task CopyResult()
    {
        if (!string.IsNullOrEmpty(OutputValue) && OutputValue != InvalidInputText)
        {
            if (ClipboardCopyRequested != null)
                await ClipboardCopyRequested.Invoke(OutputValue);
        }
    }

    private void Convert()
    {
        if (FromUnit == null || ToUnit == null) return;

        // Leere Eingabe → OutputValue leeren statt Fehlertext anzeigen
        if (string.IsNullOrWhiteSpace(InputValue))
        {
            OutputValue = "";
            return;
        }

        // EU-Format unterstützen: Komma als Dezimaltrenner akzeptieren
        var input = InputValue.Replace(',', '.');
        if (!double.TryParse(input, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            OutputValue = InvalidInputText;
            return;
        }

        // Convert to base unit (using offset for temperature)
        var baseValue = value * FromUnit.ToBase + FromUnit.Offset;
        // Convert from base to target unit
        var result = (baseValue - ToUnit.Offset) / ToUnit.ToBase;

        OutputValue = FormatResult(result);
    }

    private string FormatResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return _localization.GetString("Error");
        if ((Math.Abs(value) < 0.0001 && value != 0) || Math.Abs(value) >= 1_000_000)
            return value.ToString("E4", System.Globalization.CultureInfo.InvariantCulture);
        return value.ToString("N6", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Remember current selections
        var currentCategory = SelectedCategory?.Category;
        var currentFromKey = FromUnit?.NameKey;
        var currentToKey = ToUnit?.NameKey;

        // Rebuild with new language
        RebuildCategories();
        SelectedCategory = Categories.FirstOrDefault(c => c.Category == currentCategory)
                           ?? Categories.FirstOrDefault();

        if (currentFromKey != null)
            FromUnit = AvailableUnits.FirstOrDefault(u => u.NameKey == currentFromKey);
        if (currentToKey != null)
            ToUnit = AvailableUnits.FirstOrDefault(u => u.NameKey == currentToKey);

        // Refresh localized string properties
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(CategoryText));
        OnPropertyChanged(nameof(FromText));
        OnPropertyChanged(nameof(ToText));
        OnPropertyChanged(nameof(EnterValueText));
        OnPropertyChanged(nameof(SwapUnitsText));
        OnPropertyChanged(nameof(InvalidInputText));

        Convert();
    }

    private void RebuildCategories()
    {
        Categories =
        [
            new(UnitCategory.Length, _localization.GetString("CategoryLength")),
            new(UnitCategory.Mass, _localization.GetString("CategoryWeight")),
            new(UnitCategory.Temperature, _localization.GetString("CategoryTemperature")),
            new(UnitCategory.Time, _localization.GetString("CategoryTime")),
            new(UnitCategory.Volume, _localization.GetString("CategoryVolume")),
            new(UnitCategory.Area, _localization.GetString("CategoryArea")),
            new(UnitCategory.Speed, _localization.GetString("CategorySpeed")),
            new(UnitCategory.Data, _localization.GetString("CategoryData")),
            new(UnitCategory.Energy, _localization.GetString("CategoryEnergy")),
            new(UnitCategory.Pressure, _localization.GetString("CategoryPressure")),
            new(UnitCategory.Angle, _localization.GetString("CategoryAngle"))
        ];
    }

    private void RebuildUnits(UnitCategory category)
    {
        AvailableUnits = category switch
        {
            UnitCategory.Length =>
            [
                new("UnitMeter", "m", 1, _localization),
                new("UnitKilometer", "km", 1000, _localization),
                new("UnitCentimeter", "cm", 0.01, _localization),
                new("UnitMillimeter", "mm", 0.001, _localization),
                new("UnitMile", "mi", 1609.344, _localization),
                new("UnitYard", "yd", 0.9144, _localization),
                new("UnitFoot", "ft", 0.3048, _localization),
                new("UnitInch", "in", 0.0254, _localization),
                new("UnitNauticalMile", "nmi", 1852, _localization),
                new("UnitMicrometer", "\u00b5m", 0.000001, _localization)
            ],
            UnitCategory.Mass =>
            [
                new("UnitKilogram", "kg", 1, _localization),
                new("UnitGram", "g", 0.001, _localization),
                new("UnitMilligram", "mg", 0.000001, _localization),
                new("UnitPound", "lb", 0.453592, _localization),
                new("UnitOunce", "oz", 0.0283495, _localization),
                new("UnitTonne", "t", 1000, _localization),
                new("UnitStone", "st", 6.35029, _localization)
            ],
            UnitCategory.Temperature =>
            [
                // Temperature uses Offset for non-linear conversion
                // Base unit: Celsius
                // Formula: baseValue = value * ToBase + Offset
                // Reverse: value = (baseValue - Offset) / ToBase
                new("UnitCelsius", "\u00b0C", 1, _localization, 0),
                new("UnitFahrenheit", "\u00b0F", 5.0 / 9.0, _localization, -32 * 5.0 / 9.0),
                new("UnitKelvin", "K", 1, _localization, -273.15)
            ],
            UnitCategory.Time =>
            [
                new("UnitSeconds", "s", 1, _localization),
                new("UnitMilliseconds", "ms", 0.001, _localization),
                new("UnitMinutes", "min", 60, _localization),
                new("UnitHours", "h", 3600, _localization),
                new("UnitDays", "d", 86400, _localization),
                new("UnitWeeks", "wk", 604800, _localization)
            ],
            UnitCategory.Volume =>
            [
                new("UnitLiter", "L", 1, _localization),
                new("UnitMilliliter", "mL", 0.001, _localization),
                new("UnitCubicMeter", "m\u00b3", 1000, _localization),
                new("UnitGallonUS", "gal", 3.78541, _localization),
                new("UnitQuartUS", "qt", 0.946353, _localization),
                new("UnitPintUS", "pt", 0.473176, _localization),
                new("UnitFluidOunceUS", "fl oz", 0.0295735, _localization),
                new("UnitCup", "cup", 0.236588, _localization),
                new("UnitTablespoon", "tbsp", 0.0147868, _localization),
                new("UnitTeaspoon", "tsp", 0.00492892, _localization)
            ],
            UnitCategory.Area =>
            [
                new("UnitSquareMeter", "m\u00b2", 1, _localization),
                new("UnitSquareKilometer", "km\u00b2", 1_000_000, _localization),
                new("UnitHectare", "ha", 10000, _localization),
                new("UnitSquareFoot", "ft\u00b2", 0.092903, _localization),
                new("UnitAcre", "ac", 4046.86, _localization)
            ],
            UnitCategory.Speed =>
            [
                new("UnitMetersPerSecond", "m/s", 1, _localization),
                new("UnitKilometersPerHour", "km/h", 1.0 / 3.6, _localization),
                new("UnitMilesPerHour", "mph", 0.44704, _localization),
                new("UnitKnots", "kn", 0.514444, _localization)
            ],
            UnitCategory.Data =>
            [
                new("UnitByte", "B", 1, _localization),
                new("UnitKilobyte", "KB", 1024, _localization),
                new("UnitMegabyte", "MB", 1048576, _localization),
                new("UnitGigabyte", "GB", 1073741824, _localization),
                new("UnitTerabyte", "TB", 1099511627776, _localization),
                new("UnitBit", "bit", 0.125, _localization)
            ],
            UnitCategory.Energy =>
            [
                new("UnitJoule", "J", 1, _localization),
                new("UnitKilojoule", "kJ", 1000, _localization),
                new("UnitCalorie", "cal", 4.184, _localization),
                new("UnitKilocalorie", "kcal", 4184, _localization),
                new("UnitWattHour", "Wh", 3600, _localization),
                new("UnitKilowattHour", "kWh", 3_600_000, _localization),
                new("UnitBTU", "BTU", 1055.06, _localization)
            ],
            UnitCategory.Pressure =>
            [
                new("UnitPascal", "Pa", 1, _localization),
                new("UnitKilopascal", "kPa", 1000, _localization),
                new("UnitBar", "bar", 100_000, _localization),
                new("UnitAtmosphere", "atm", 101_325, _localization),
                new("UnitPSI", "psi", 6894.76, _localization),
                new("UnitMillimeterMercury", "mmHg", 133.322, _localization)
            ],
            UnitCategory.Angle =>
            [
                new("UnitDegree", "\u00b0", 1, _localization),
                new("UnitRadian", "rad", 180.0 / Math.PI, _localization),
                new("UnitGradian", "gon", 0.9, _localization),
                new("UnitTurn", "tr", 360, _localization),
                new("UnitArcminute", "\u2032", 1.0 / 60.0, _localization),
                new("UnitArcsecond", "\u2033", 1.0 / 3600.0, _localization)
            ],
            _ => []
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _localization.LanguageChanged -= OnLanguageChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public enum UnitCategory
{
    Length,
    Mass,
    Temperature,
    Time,
    Volume,
    Area,
    Speed,
    Data,
    Energy,
    Pressure,
    Angle
}

/// <summary>
/// Category item with localized display name for ComboBox
/// </summary>
public record CategoryItem(UnitCategory Category, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Unit item with localized display name, symbol, and conversion factors
/// </summary>
public class UnitItem
{
    public string NameKey { get; }
    public string Symbol { get; }
    public double ToBase { get; }
    public double Offset { get; }
    public string DisplayName { get; }

    public UnitItem(string nameKey, string symbol, double toBase,
                    ILocalizationService localization, double offset = 0)
    {
        NameKey = nameKey;
        Symbol = symbol;
        ToBase = toBase;
        Offset = offset;
        DisplayName = localization.GetString(nameKey);
    }

    public override string ToString() => $"{DisplayName} ({Symbol})";
}
