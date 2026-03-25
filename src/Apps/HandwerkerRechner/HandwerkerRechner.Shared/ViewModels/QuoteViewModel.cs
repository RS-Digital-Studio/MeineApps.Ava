using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerRechner.Models;
using HandwerkerRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using System.Collections.ObjectModel;

namespace HandwerkerRechner.ViewModels;

/// <summary>
/// Angebots-/Rechnungsgenerator ViewModel.
/// Verwaltet Angebote mit Positionen, MwSt und Marge.
/// Export als PDF mit Briefkopf und Positions-Tabelle.
/// </summary>
public sealed partial class QuoteViewModel : ObservableObject
{
    private readonly IQuoteService _quoteService;
    private readonly ILocalizationService _localization;
    private readonly IMaterialExportService _exportService;
    private readonly IFileShareService _fileShareService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;

    #region Listen-Ansicht

    public ObservableCollection<Quote> Quotes { get; } = [];
    [ObservableProperty] private bool _hasQuotes;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEditing;

    #endregion

    #region Aktives Angebot

    [ObservableProperty] private Quote? _currentQuote;
    [ObservableProperty] private string _quoteNumber = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _customerAddress = "";
    [ObservableProperty] private string _projectDescription = "";
    [ObservableProperty] private double _vatPercent = 19.0;
    [ObservableProperty] private double _marginPercent = 15.0;

    // Summen bei MwSt/Marge-Änderung aktualisieren
    partial void OnVatPercentChanged(double value) => RecalculateTotals();
    partial void OnMarginPercentChanged(double value) => RecalculateTotals();

    public ObservableCollection<QuoteItem> CurrentItems { get; } = [];

    // Berechnete Werte
    [ObservableProperty] private double _subtotalNet;
    [ObservableProperty] private double _marginAmount;
    [ObservableProperty] private double _totalNet;
    [ObservableProperty] private double _vatAmount;
    [ObservableProperty] private double _totalGross;

    #endregion

    #region Neue Position

    [ObservableProperty] private string _newItemDescription = "";
    [ObservableProperty] private double _newItemQuantity = 1;
    [ObservableProperty] private double _newItemUnitPrice;
    [ObservableProperty] private string _newItemUnit = "m²";
    [ObservableProperty] private int _newItemTypeIndex; // 0=Material, 1=Arbeit, 2=Sonstiges

    #endregion

    #region Lokalisierte Texte

    public string PageTitle => _localization.GetString("Quotes") ?? "Angebote";
    public string NewQuoteText => _localization.GetString("NewQuote") ?? "Neues Angebot";
    public string QuoteNumberLabel => _localization.GetString("QuoteNumber") ?? "Angebotsnummer";
    public string CustomerNameLabel => _localization.GetString("CustomerName") ?? "Kundenname";
    public string CustomerAddressLabel => _localization.GetString("CustomerAddress") ?? "Adresse";
    public string ProjectDescriptionLabel => _localization.GetString("ProjectDescription") ?? "Projektbeschreibung";
    public string ValidUntilLabel => _localization.GetString("ValidUntil") ?? "Gültig bis";
    public string QuoteItemsLabel => _localization.GetString("QuoteItems") ?? "Positionen";
    public string AddItemLabel => _localization.GetString("AddItem") ?? "Position hinzufügen";
    public string ItemDescriptionLabel => _localization.GetString("ItemDescription") ?? "Bezeichnung";
    public string ItemUnitLabel => _localization.GetString("ItemUnit") ?? "Einheit";
    public string ItemQuantityLabel => _localization.GetString("ItemQuantity") ?? "Menge";
    public string ItemUnitPriceLabel => _localization.GetString("ItemUnitPrice") ?? "Einzelpreis (€)";
    public string SubtotalNetLabel => _localization.GetString("SubtotalNet") ?? "Zwischensumme netto";
    public string MarginLabel => _localization.GetString("MarginPercent") ?? "Marge (%)";
    public string TotalNetLabel => _localization.GetString("TotalNet") ?? "Gesamt netto";
    public string VatLabel => _localization.GetString("VatPercent") ?? "MwSt (%)";
    public string TotalGrossLabel => _localization.GetString("TotalGross") ?? "Gesamt brutto";
    public string ExportQuoteText => _localization.GetString("ExportQuote") ?? "Angebot exportieren";

    public List<string> UnitOptions => ["m²", "m", "Stück", "kg", "l", "h", "pauschal"];
    public List<string> ItemTypeOptions =>
    [
        _localization.GetString("ItemTypeMaterial") ?? "Material",
        _localization.GetString("ItemTypeLabor") ?? "Arbeit",
        _localization.GetString("ItemTypeOther") ?? "Sonstiges"
    ];

    #endregion

    public QuoteViewModel(
        IQuoteService quoteService,
        ILocalizationService localization,
        IMaterialExportService exportService,
        IFileShareService fileShareService)
    {
        _quoteService = quoteService;
        _localization = localization;
        _exportService = exportService;
        _fileShareService = fileShareService;
    }

    [RelayCommand]
    public async Task LoadQuotesAsync()
    {
        IsLoading = true;
        try
        {
            var quotes = await _quoteService.LoadAllQuotesAsync();
            Quotes.Clear();
            foreach (var q in quotes)
                Quotes.Add(q);
            HasQuotes = Quotes.Count > 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateNewQuoteAsync()
    {
        var number = await _quoteService.GenerateQuoteNumberAsync();
        CurrentQuote = new Quote { QuoteNumber = number };
        QuoteNumber = number;
        CustomerName = "";
        CustomerAddress = "";
        ProjectDescription = "";
        VatPercent = 19.0;
        MarginPercent = 15.0;
        CurrentItems.Clear();
        RecalculateTotals();
        IsEditing = true;
    }

    [RelayCommand]
    private void EditQuote(Quote? quote)
    {
        if (quote == null) return;
        CurrentQuote = quote;
        QuoteNumber = quote.QuoteNumber;
        CustomerName = quote.CustomerName;
        CustomerAddress = quote.CustomerAddress;
        ProjectDescription = quote.ProjectDescription;
        VatPercent = quote.VatPercent;
        MarginPercent = quote.MarginPercent;

        CurrentItems.Clear();
        foreach (var item in quote.Items)
            CurrentItems.Add(item);

        RecalculateTotals();
        IsEditing = true;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (string.IsNullOrWhiteSpace(NewItemDescription) || NewItemQuantity <= 0) return;

        CurrentItems.Add(new QuoteItem
        {
            Description = NewItemDescription.Trim(),
            Unit = NewItemUnit,
            Quantity = NewItemQuantity,
            UnitPrice = NewItemUnitPrice,
            ItemType = (QuoteItemType)NewItemTypeIndex
        });

        NewItemDescription = "";
        NewItemQuantity = 1;
        NewItemUnitPrice = 0;
        RecalculateTotals();
    }

    [RelayCommand]
    private void RemoveItem(QuoteItem? item)
    {
        if (item == null) return;
        CurrentItems.Remove(item);
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        SubtotalNet = CurrentItems.Sum(i => i.Total);
        MarginAmount = SubtotalNet * MarginPercent / 100.0;
        TotalNet = SubtotalNet + MarginAmount;
        VatAmount = TotalNet * VatPercent / 100.0;
        TotalGross = TotalNet + VatAmount;
    }

    [RelayCommand]
    private async Task SaveQuoteAsync()
    {
        if (CurrentQuote == null) return;

        CurrentQuote.QuoteNumber = QuoteNumber;
        CurrentQuote.CustomerName = CustomerName.Trim();
        CurrentQuote.CustomerAddress = CustomerAddress.Trim();
        CurrentQuote.ProjectDescription = ProjectDescription.Trim();
        CurrentQuote.VatPercent = VatPercent;
        CurrentQuote.MarginPercent = MarginPercent;
        CurrentQuote.Items = [.. CurrentItems];

        await _quoteService.SaveQuoteAsync(CurrentQuote);
        IsEditing = false;

        FloatingTextRequested?.Invoke(
            _localization.GetString("QuoteSaved") ?? "Angebot gespeichert!", "success");

        await LoadQuotesAsync();
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task DeleteQuoteAsync(Quote? quote)
    {
        if (quote == null) return;
        await _quoteService.DeleteQuoteAsync(quote.Id);
        await LoadQuotesAsync();
        FloatingTextRequested?.Invoke(
            _localization.GetString("QuoteDeleted") ?? "Angebot gelöscht", "info");
    }

    [RelayCommand]
    private async Task ExportQuoteAsync()
    {
        if (CurrentQuote == null || CurrentItems.Count == 0) return;

        try
        {
            // Angebot zuerst speichern
            await SaveQuoteAsync();

            // Als PDF exportieren über den vorhandenen ExportService
            var inputs = new Dictionary<string, string>
            {
                [QuoteNumberLabel] = QuoteNumber,
                [CustomerNameLabel] = CustomerName,
                [CustomerAddressLabel] = CustomerAddress,
                [ProjectDescriptionLabel] = ProjectDescription,
                [ValidUntilLabel] = CurrentQuote.ValidUntil.ToLocalTime().ToString("d")
            };

            var results = new Dictionary<string, string>();
            var pos = 1;
            foreach (var item in CurrentItems)
            {
                results[$"Pos. {pos}: {item.Description}"] = $"{item.Quantity:F2} {item.Unit} × {item.UnitPrice:F2} € = {item.Total:F2} €";
                pos++;
            }
            results["---"] = "---";
            results[SubtotalNetLabel] = $"{SubtotalNet:F2} €";
            results[$"{MarginLabel} ({MarginPercent:F1}%)"] = $"{MarginAmount:F2} €";
            results[TotalNetLabel] = $"{TotalNet:F2} €";
            results[$"{VatLabel} ({VatPercent:F1}%)"] = $"{VatAmount:F2} €";
            results[TotalGrossLabel] = $"{TotalGross:F2} €";

            var path = await _exportService.ExportToPdfAsync(
                $"{PageTitle} {QuoteNumber}", inputs, results);
            await _fileShareService.ShareFileAsync(path, PageTitle, "application/pdf");

            FloatingTextRequested?.Invoke(
                _localization.GetString("QuoteExported") ?? "Angebot exportiert!", "success");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Fehler", ex.Message);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (IsEditing)
        {
            IsEditing = false;
            return;
        }
        NavigationRequested?.Invoke("..");
    }

    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(string.Empty);
    }
}
