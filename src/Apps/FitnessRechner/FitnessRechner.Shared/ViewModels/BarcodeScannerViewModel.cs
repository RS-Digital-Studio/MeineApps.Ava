using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;

namespace FitnessRechner.ViewModels;

public partial class BarcodeScannerViewModel : ObservableObject
{
    private readonly IBarcodeLookupService _barcodeLookupService;
    private CancellationTokenSource? _delayCancellation;
    private const int RETRY_DELAY_MS = 2000;

    /// <summary>
    /// Raised when the VM wants to navigate.
    /// The string parameter is the route/page name.
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Raised when a food item has been selected and should be passed back to FoodSearchViewModel
    /// </summary>
    public event Action<FoodItem>? FoodSelected;

    public BarcodeScannerViewModel(IBarcodeLookupService barcodeLookupService)
    {
        _barcodeLookupService = barcodeLookupService;
    }

    [ObservableProperty] private bool _isScanning = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private FoodItem? _foundFood;
    [ObservableProperty] private bool _showResult;
    [ObservableProperty] private bool _hasIncompleteData;
    [ObservableProperty] private string _dataWarningMessage = "";
    [ObservableProperty] private string _manualBarcodeInput = "";

    partial void OnIsScanningChanged(bool value)
    {
        if (value && string.IsNullOrEmpty(StatusMessage))
        {
            StatusMessage = AppStrings.ScanBarcode;
        }
    }

    /// <summary>
    /// Process a detected barcode value
    /// </summary>
    public async Task OnBarcodeDetected(string barcodeValue)
    {
        if (IsBusy || !IsScanning) return;
        if (string.IsNullOrEmpty(barcodeValue)) return;

        // Cancel any pending delay
        _delayCancellation?.Cancel();
        _delayCancellation = new CancellationTokenSource();

        IsBusy = true;
        IsScanning = false;
        StatusMessage = string.Format(AppStrings.SearchingBarcode, barcodeValue);

        try
        {
            var food = await _barcodeLookupService.LookupByBarcodeAsync(barcodeValue);

            if (food != null)
            {
                FoundFood = food;
                ShowResult = true;

                // Check if nutrition data is complete
                if (food.CaloriesPer100g == 0 && food.ProteinPer100g == 0 && food.CarbsPer100g == 0 && food.FatPer100g == 0)
                {
                    HasIncompleteData = true;
                    DataWarningMessage = AppStrings.NutritionDataIncomplete;
                    StatusMessage = AppStrings.ProductFoundIncomplete;
                }
                else if (food.CaloriesPer100g == 0)
                {
                    HasIncompleteData = true;
                    DataWarningMessage = AppStrings.CalorieDataMissing;
                    StatusMessage = AppStrings.ProductFoundSomeDataMissing;
                }
                else
                {
                    HasIncompleteData = false;
                    DataWarningMessage = "";
                    StatusMessage = AppStrings.ProductFound;
                }
            }
            else
            {
                StatusMessage = AppStrings.ProductNotFound;
                await DelayAndResetAsync(_delayCancellation.Token);
            }
        }
        catch (Exception)
        {
            StatusMessage = AppStrings.SearchError;
            await DelayAndResetAsync(_delayCancellation.Token);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DelayAndResetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RETRY_DELAY_MS, cancellationToken).ConfigureAwait(false);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    IsScanning = true;
                    StatusMessage = AppStrings.ScanBarcode;
                }
            });
        }
        catch (TaskCanceledException)
        {
            // Expected when cancelled
        }
    }

    [RelayCommand]
    private void UseFood()
    {
        if (FoundFood == null) return;

        // Notify listeners about the selected food
        FoodSelected?.Invoke(FoundFood);

        // Navigate back
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void ScanAgain()
    {
        _delayCancellation?.Cancel();
        FoundFood = null;
        ShowResult = false;
        IsScanning = true;
        HasIncompleteData = false;
        DataWarningMessage = "";
        StatusMessage = AppStrings.ScanBarcode;
    }

    [RelayCommand]
    private async Task ScanManually()
    {
        var barcode = ManualBarcodeInput?.Trim();
        if (string.IsNullOrEmpty(barcode)) return;

        await OnBarcodeDetected(barcode);
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    /// <summary>
    /// Cleanup method to release resources and avoid battery drain
    /// </summary>
    public void Cleanup()
    {
        _delayCancellation?.Cancel();
        IsScanning = false;
        IsBusy = false;
        FoundFood = null;
        ShowResult = false;
        HasIncompleteData = false;
        DataWarningMessage = "";
        StatusMessage = AppStrings.ScanBarcode;
    }

    // NOTE: No Finalizer to avoid GC-Thread SIGSEGV crashes
}
