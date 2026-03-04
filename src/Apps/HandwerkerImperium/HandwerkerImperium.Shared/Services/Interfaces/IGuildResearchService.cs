using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet Gilden-Forschung (18 Technologien, Timer, Effekte).
/// Extrahiert aus GuildService für bessere Trennung.
/// </summary>
public interface IGuildResearchService
{
    /// <summary>Lädt alle Gilden-Forschungen mit aktuellem Fortschritt von Firebase.</summary>
    Task<List<GuildResearchDisplay>> GetGuildResearchAsync();

    /// <summary>Leistet einen Geldbeitrag zu einer bestimmten Forschung. Gibt true bei Erfolg zurück.</summary>
    Task<bool> ContributeToResearchAsync(string researchId, long amount);

    /// <summary>Prüft ob eine laufende Forschung abgeschlossen ist (Timer abgelaufen). Gibt true zurück wenn mindestens eine abgeschlossen wurde.</summary>
    Task<bool> CheckResearchCompletionAsync();

    /// <summary>Gibt die gecachten Forschungs-Effekte zurück (kein Firebase-Request).</summary>
    GuildResearchEffects GetCachedEffects();

    /// <summary>Aktualisiert den Forschungs-Effekt-Cache von Firebase.</summary>
    Task RefreshResearchCacheAsync();
}
