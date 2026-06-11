using CommunityToolkit.Mvvm.Input;

namespace HandwerkerRechner.ViewModels.Floor;

public sealed partial class ConcreteCalculatorViewModel
{
    [RelayCommand]
    private void ShareResult()
    {
        if (!HasResult || Result == null) return;

        var shapeName = Calculators[SelectedCalculator];
        var text = $"{shapeName}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("ResultVolume") ?? "Volume"}: {VolumeDisplay}\n" +
                   $"{_localization.GetString("ResultBags") ?? "Bags"}: {BagsDisplay}\n" +
                   $"\n{_localization.GetString("SelfMixing") ?? "Self-Mixing"}:\n" +
                   $"{_localization.GetString("ResultCite") ?? "Cement"}: {CementDisplay}\n" +
                   $"{_localization.GetString("ResultSand") ?? "Sand"}: {SandDisplay}\n" +
                   $"{_localization.GetString("ResultGravel") ?? "Gravel"}: {GravelDisplay}\n" +
                   $"{_localization.GetString("ResultWater") ?? "Water"}: {WaterDisplay}";

        if (ShowBagCost && PricePerBag > 0)
            text += $"\n{_localization.GetString("CostBags") ?? "Bag Cost"}: {BagCostDisplay}";
        if (ShowCubicMeterCost && PricePerCubicMeter > 0)
            text += $"\n{_localization.GetString("CostCubicMeter") ?? "Cost/m³"}: {CubicMeterCostDisplay}";

        ClipboardRequested?.Invoke(text);
        FloatingTextRequested?.Invoke(_localization.GetString("CopiedToClipboard") ?? "Copied!", "success");
    }

    [RelayCommand]
    private async Task ExportMaterialList()
    {
        if (!HasResult || Result == null) return;
        if (IsExporting) return;

        try
        {
            IsExporting = true;

            var calcType = Calculators[SelectedCalculator];
            var inputs = new Dictionary<string, string>();
            var results = new Dictionary<string, string>();

            switch (SelectedCalculator)
            {
                case 0: // Platte
                    inputs[_localization.GetString("SlabLength") ?? "Length"] = $"{SlabLength:F1} m";
                    inputs[_localization.GetString("SlabWidth") ?? "Width"] = $"{SlabWidth:F1} m";
                    inputs[_localization.GetString("SlabHeight") ?? "Height"] = $"{SlabHeight} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;

                case 1: // Streifenfundament
                    inputs[_localization.GetString("StripLength") ?? "Length"] = $"{StripLength:F1} m";
                    inputs[_localization.GetString("StripWidth") ?? "Width"] = $"{StripWidth} cm";
                    inputs[_localization.GetString("StripDepth") ?? "Depth"] = $"{StripDepth} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;

                case 2: // Säule
                    inputs[_localization.GetString("ColumnDiameter") ?? "Diameter"] = $"{ColumnDiameter} cm";
                    inputs[_localization.GetString("ColumnHeight") ?? "Height"] = $"{ColumnHeight} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;
            }

            // Ergebnisse (gleich fuer alle Sub-Rechner)
            results[_localization.GetString("ResultVolume") ?? "Volume"] = VolumeDisplay;
            results[_localization.GetString("ResultCite") ?? "Cement"] = CementDisplay;
            results[_localization.GetString("ResultSand") ?? "Sand"] = SandDisplay;
            results[_localization.GetString("ResultGravel") ?? "Gravel"] = GravelDisplay;
            results[_localization.GetString("ResultWater") ?? "Water"] = WaterDisplay;
            results[_localization.GetString("ResultBags") ?? "Bags"] = BagsDisplay;

            if (ShowBagCost && PricePerBag > 0)
                results[_localization.GetString("PricePerBag") ?? "Bag cost"] = BagCostDisplay;
            if (ShowCubicMeterCost && PricePerCubicMeter > 0)
                results[_localization.GetString("PricePerCubicMeter") ?? "m³ cost"] = CubicMeterCostDisplay;

            var path = await _exportService.ExportToPdfAsync(calcType, inputs, results);
            await _fileShareService.ShareFileAsync(path, _localization.GetString("ShareMaterialList") ?? "Share", "application/pdf");
            MessageRequested?.Invoke(_localization.GetString("Success") ?? "Success", _localization.GetString("PdfExportSuccess") ?? "PDF exported!");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Error", _localization.GetString("PdfExportFailed") ?? "Export failed.");
        }
        finally
        {
            IsExporting = false;
        }
    }


    [RelayCommand]
    private async Task ExportCsv()
    {
        if (!HasResult || Result == null) return;
        if (IsExporting) return;

        try
        {
            IsExporting = true;

            var calcType = Calculators[SelectedCalculator];
            var inputs = new Dictionary<string, string>();
            var results = new Dictionary<string, string>();

            switch (SelectedCalculator)
            {
                case 0: // Platte
                    inputs[_localization.GetString("SlabLength") ?? "Length"] = $"{SlabLength:F1} m";
                    inputs[_localization.GetString("SlabWidth") ?? "Width"] = $"{SlabWidth:F1} m";
                    inputs[_localization.GetString("SlabHeight") ?? "Height"] = $"{SlabHeight} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;

                case 1: // Streifenfundament
                    inputs[_localization.GetString("StripLength") ?? "Length"] = $"{StripLength:F1} m";
                    inputs[_localization.GetString("StripWidth") ?? "Width"] = $"{StripWidth} cm";
                    inputs[_localization.GetString("StripDepth") ?? "Depth"] = $"{StripDepth} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;

                case 2: // Säule
                    inputs[_localization.GetString("ColumnDiameter") ?? "Diameter"] = $"{ColumnDiameter} cm";
                    inputs[_localization.GetString("ColumnHeight") ?? "Height"] = $"{ColumnHeight} cm";
                    inputs[_localization.GetString("BagWeight") ?? "Bag weight"] = $"{BagWeight} kg";
                    break;
            }

            // Ergebnisse (gleich fuer alle Sub-Rechner)
            results[_localization.GetString("ResultVolume") ?? "Volume"] = VolumeDisplay;
            results[_localization.GetString("ResultCite") ?? "Cement"] = CementDisplay;
            results[_localization.GetString("ResultSand") ?? "Sand"] = SandDisplay;
            results[_localization.GetString("ResultGravel") ?? "Gravel"] = GravelDisplay;
            results[_localization.GetString("ResultWater") ?? "Water"] = WaterDisplay;
            results[_localization.GetString("ResultBags") ?? "Bags"] = BagsDisplay;

            if (ShowBagCost && PricePerBag > 0)
                results[_localization.GetString("PricePerBag") ?? "Bag cost"] = BagCostDisplay;
            if (ShowCubicMeterCost && PricePerCubicMeter > 0)
                results[_localization.GetString("PricePerCubicMeter") ?? "m³ cost"] = CubicMeterCostDisplay;

            var path = await _exportService.ExportToCsvAsync(calcType, inputs, results);
            await _fileShareService.ShareFileAsync(path, _localization.GetString("ShareMaterialList") ?? "Share", "text/csv");
            MessageRequested?.Invoke(_localization.GetString("Success") ?? "Success", _localization.GetString("CsvExportSuccess") ?? "CSV exported!");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(_localization.GetString("Error") ?? "Error", _localization.GetString("CsvExportFailed") ?? "Export failed.");
        }
        finally
        {
            IsExporting = false;
        }
    }
}
