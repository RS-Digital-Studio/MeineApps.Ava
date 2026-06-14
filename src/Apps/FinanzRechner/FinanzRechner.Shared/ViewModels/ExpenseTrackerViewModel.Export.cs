using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FinanzRechner.ViewModels;

// Partial: CSV-Export mit Rewarded-Ad-Gate + Status-Anzeige.
// Free-User sehen vor dem Export ein Ad-Overlay (Placement "export_csv"),
// Premium-User exportieren direkt. Status-Toast blendet sich nach 4 s aus.
public sealed partial class ExpenseTrackerViewModel
{
    #region Export Status

    [ObservableProperty]
    private string? _exportStatusMessage;

    [ObservableProperty]
    private bool _isExportStatusVisible;

    private CancellationTokenSource? _statusCts;

    private async Task ShowExportStatusAsync(string message)
    {
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = new CancellationTokenSource();
        var token = _statusCts.Token;

        ExportStatusMessage = message;
        IsExportStatusVisible = true;

        try
        {
            await Task.Delay(4000, token);
            IsExportStatusVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    #endregion

    #region CSV Export Ad Gate

    [ObservableProperty]
    private bool _showCsvExportAdOverlay;

    /// <summary>
    /// Merkt sich welcher CSV-Export angefragt wurde ("month" oder "all").
    /// </summary>
    private string _pendingCsvExportType = "";

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (IsLoading) return;

        if (_purchaseService.IsPremium)
        {
            await DoExportToCsvAsync();
            return;
        }

        _pendingCsvExportType = "month";
        ShowCsvExportAdOverlay = true;
    }

    [RelayCommand]
    private async Task ExportAllToCsvAsync()
    {
        if (IsLoading) return;

        if (_purchaseService.IsPremium)
        {
            await DoExportAllToCsvAsync();
            return;
        }

        _pendingCsvExportType = "all";
        ShowCsvExportAdOverlay = true;
    }

    [RelayCommand]
    private async Task ConfirmCsvExportAdAsync()
    {
        ShowCsvExportAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("export_csv");
        if (success)
        {
            if (_pendingCsvExportType == "month")
                await DoExportToCsvAsync();
            else if (_pendingCsvExportType == "all")
                await DoExportAllToCsvAsync();
        }
        else
        {
            var msg = _localizationService.GetString("ExportAdFailed") ?? "Could not load video";
            _ = ShowExportStatusAsync(msg);
        }
        _pendingCsvExportType = "";
    }

    [RelayCommand]
    private void CancelCsvExportAd()
    {
        ShowCsvExportAdOverlay = false;
        _pendingCsvExportType = "";
    }

    private async Task DoExportToCsvAsync()
    {
        if (IsLoading) return;

        try
        {
            var monthName = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
            var suggestedName = $"transactions_{SelectedYear}_{SelectedMonth:D2}.csv";
            var title = $"{_localizationService.GetString("ExportTitle") ?? "Export"} - {monthName}";

            var targetPath = await _fileDialogService.SaveFileAsync(suggestedName, title, "CSV", "csv");
            if (targetPath == null)
            {
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, suggestedName);
            }

            IsLoading = true;
            var filePath = await _exportService.ExportToCsvAsync(SelectedYear, SelectedMonth, targetPath);

            await _fileShareService.ShareFileAsync(filePath, title, "text/csv");

            var successMsg = _localizationService.GetString("ExportSuccess") ?? "Export successful";
            _ = ShowExportStatusAsync($"{successMsg}: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            var errorMsg = $"{_localizationService.GetString("ExportError") ?? "Export failed"}: {ex.Message}";
            _ = ShowExportStatusAsync(errorMsg);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DoExportAllToCsvAsync()
    {
        if (IsLoading) return;

        try
        {
            var title = _localizationService.GetString("ExportAllTitle") ?? "Export all transactions";

            var targetPath = await _fileDialogService.SaveFileAsync("transactions_all.csv", title, "CSV", "csv");
            if (targetPath == null)
            {
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, "transactions_all.csv");
            }

            IsLoading = true;
            var filePath = await _exportService.ExportAllToCsvAsync(targetPath);

            await _fileShareService.ShareFileAsync(filePath, title, "text/csv");

            var successMsg = _localizationService.GetString("ExportSuccess") ?? "Export successful";
            _ = ShowExportStatusAsync($"{successMsg}: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            var errorMsg = $"{_localizationService.GetString("ExportError") ?? "Export failed"}: {ex.Message}";
            _ = ShowExportStatusAsync(errorMsg);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}
