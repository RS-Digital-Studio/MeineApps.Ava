using CommunityToolkit.Mvvm.Input;

namespace HandwerkerRechner.ViewModels.Floor;

public sealed partial class WallpaperCalculatorViewModel
{
    [RelayCommand]
    private void ShareResult()
    {
        if (!HasResult || Result == null) return;

        var title = _localization.GetString("CalcWallpaper") ?? "Wallpaper";
        var text = $"{title}\n" +
                   $"─────────────\n" +
                   $"{_localization.GetString("WallArea") ?? "Wall area"}: {AreaDisplay}\n" +
                   $"{_localization.GetString("StripsNeeded") ?? "Strips"}: {StripsNeededDisplay}\n" +
                   $"{_localization.GetString("RollsNeeded") ?? "Rolls"}: {RollsNeededDisplay}";

        var deduction = CalculateDeductionArea();
        if (deduction > 0)
            text += $"\n{_localization.GetString("DeductedArea") ?? "Deducted area"}: -{deduction:F1} m²";

        if (ShowCost && PricePerRoll > 0)
            text += $"\n{_localization.GetString("TotalCost") ?? "Total cost"}: {TotalCostDisplay}";

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

            var calcType = _localization.GetString("CalcWallpaper") ?? "Wallpaper";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("RoomPerimeter") ?? "Room perimeter"] = $"{WallLength:F1} m",
                [_localization.GetString("RoomHeight") ?? "Room height"] = $"{RoomHeight:F1} m",
                [_localization.GetString("RollLength") ?? "Roll length"] = $"{RollLength:F2} m",
                [_localization.GetString("RollWidth") ?? "Roll width"] = $"{RollWidth} cm",
                [_localization.GetString("PatternRepeat") ?? "Pattern repeat"] = $"{PatternRepeat} cm"
            };

            var deduction = CalculateDeductionArea();
            if (deduction > 0)
                inputs[_localization.GetString("DeductedArea") ?? "Deducted area"] = $"-{deduction:F1} m²";

            var results = new Dictionary<string, string>
            {
                [_localization.GetString("WallArea") ?? "Wall area"] = AreaDisplay,
                [_localization.GetString("RollsNeeded") ?? "Rolls needed"] = RollsNeededDisplay
            };
            if (ShowCost && PricePerRoll > 0)
                results[_localization.GetString("TotalCost") ?? "Total cost"] = TotalCostDisplay;

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

            var calcType = _localization.GetString("CalcWallpaper") ?? "Wallpaper";
            var inputs = new Dictionary<string, string>
            {
                [_localization.GetString("RoomPerimeter") ?? "Room perimeter"] = $"{WallLength:F1} m",
                [_localization.GetString("RoomHeight") ?? "Room height"] = $"{RoomHeight:F1} m",
                [_localization.GetString("RollLength") ?? "Roll length"] = $"{RollLength:F2} m",
                [_localization.GetString("RollWidth") ?? "Roll width"] = $"{RollWidth} cm",
                [_localization.GetString("PatternRepeat") ?? "Pattern repeat"] = $"{PatternRepeat} cm"
            };

            var deduction = CalculateDeductionArea();
            if (deduction > 0)
                inputs[_localization.GetString("DeductedArea") ?? "Deducted area"] = $"-{deduction:F1} m²";

            var results = new Dictionary<string, string>
            {
                [_localization.GetString("WallArea") ?? "Wall area"] = AreaDisplay,
                [_localization.GetString("RollsNeeded") ?? "Rolls needed"] = RollsNeededDisplay
            };
            if (ShowCost && PricePerRoll > 0)
                results[_localization.GetString("TotalCost") ?? "Total cost"] = TotalCostDisplay;

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
