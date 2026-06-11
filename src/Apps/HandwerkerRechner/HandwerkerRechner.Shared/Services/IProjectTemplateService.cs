using HandwerkerRechner.Models;

namespace HandwerkerRechner.Services;

/// <summary>
/// Verwaltet Projekt-Vorlagen (eingebaute + benutzerdefinierte).
/// Eingebaute Vorlagen sind hardcodiert, eigene werden als JSON persistiert.
/// </summary>
public interface IProjectTemplateService
{
    /// <summary>Wird ausgelöst, wenn das Speichern fehlschlägt (z.B. Speicher voll/Schreibschutz).</summary>
    event Action? SaveFailed;

    /// <summary>Alle Vorlagen (eingebaut + benutzerdefiniert)</summary>
    Task<List<ProjectTemplate>> GetAllTemplatesAsync();

    /// <summary>Nur eingebaute Vorlagen</summary>
    List<ProjectTemplate> GetBuiltinTemplates();

    /// <summary>Benutzerdefinierte Vorlage speichern</summary>
    Task SaveCustomTemplateAsync(ProjectTemplate template);

    /// <summary>Benutzerdefinierte Vorlage löschen</summary>
    Task DeleteCustomTemplateAsync(string templateId);
}
