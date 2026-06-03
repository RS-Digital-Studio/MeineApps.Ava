using HandwerkerRechner.Models;

namespace HandwerkerRechner.Services;

/// <summary>
/// Verwaltet Angebote/Rechnungen mit JSON-Persistierung und PDF-Export.
/// </summary>
public interface IQuoteService
{
    /// <summary>Wird ausgelöst, wenn das Speichern fehlschlägt (z.B. Speicher voll/Schreibschutz) — die UI zeigt einen Hinweis statt stillem Datenverlust.</summary>
    event Action? SaveFailed;

    /// <summary>Alle Angebote laden (sortiert nach Erstellungsdatum, neueste zuerst)</summary>
    Task<List<Quote>> LoadAllQuotesAsync();

    /// <summary>Einzelnes Angebot laden</summary>
    Task<Quote?> LoadQuoteAsync(string quoteId);

    /// <summary>Angebot speichern (neu oder aktualisiert)</summary>
    Task SaveQuoteAsync(Quote quote);

    /// <summary>Angebot löschen</summary>
    Task DeleteQuoteAsync(string quoteId);

    /// <summary>Nächste Angebotsnummer generieren (z.B. "A-2026-001")</summary>
    Task<string> GenerateQuoteNumberAsync();
}
