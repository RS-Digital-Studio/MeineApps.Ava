using System.Text.Json;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.Services;

/// <summary>
/// JSON-basierte Angebotsverwaltung mit Thread-Safety.
/// Angebote werden im AppData-Verzeichnis gespeichert.
/// </summary>
public sealed class QuoteService : IQuoteService
{
    private readonly string _quotesFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<Quote>? _cachedQuotes;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public QuoteService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeineApps", "HandwerkerRechner");
        Directory.CreateDirectory(appDataPath);
        _quotesFilePath = Path.Combine(appDataPath, "quotes.json");
    }

    public async Task<List<Quote>> LoadAllQuotesAsync()
    {
        await EnsureLoadedAsync();
        await _semaphore.WaitAsync();
        try
        {
            return _cachedQuotes!.OrderByDescending(q => q.CreatedDate).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Quote?> LoadQuoteAsync(string quoteId)
    {
        await EnsureLoadedAsync();
        await _semaphore.WaitAsync();
        try
        {
            return _cachedQuotes!.FirstOrDefault(q => q.Id == quoteId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveQuoteAsync(Quote quote)
    {
        await EnsureLoadedAsync();
        await _semaphore.WaitAsync();
        try
        {
            var existing = _cachedQuotes!.FindIndex(q => q.Id == quote.Id);
            if (existing >= 0)
                _cachedQuotes[existing] = quote;
            else
                _cachedQuotes.Add(quote);

            await SaveToFileAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteQuoteAsync(string quoteId)
    {
        await EnsureLoadedAsync();
        await _semaphore.WaitAsync();
        try
        {
            _cachedQuotes!.RemoveAll(q => q.Id == quoteId);
            await SaveToFileAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GenerateQuoteNumberAsync()
    {
        await EnsureLoadedAsync();
        var year = DateTime.UtcNow.Year;
        var prefix = $"A-{year}-";

        await _semaphore.WaitAsync();
        try
        {
            var maxNumber = 0;
            foreach (var q in _cachedQuotes!)
            {
                if (!q.QuoteNumber.StartsWith(prefix)) continue;
                if (int.TryParse(q.QuoteNumber[prefix.Length..], out var num) && num > maxNumber)
                    maxNumber = num;
            }
            return $"{prefix}{(maxNumber + 1):D3}";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_cachedQuotes != null) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_cachedQuotes != null) return;

            if (File.Exists(_quotesFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_quotesFilePath);
                    _cachedQuotes = JsonSerializer.Deserialize<List<Quote>>(json) ?? [];
                }
                catch
                {
                    _cachedQuotes = [];
                }
            }
            else
            {
                _cachedQuotes = [];
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveToFileAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cachedQuotes, JsonOptions);
            await File.WriteAllTextAsync(_quotesFilePath, json);
        }
        catch
        {
            // Fehler beim Speichern - Daten bleiben im Cache
        }
    }
}
