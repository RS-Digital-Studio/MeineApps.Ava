namespace HandwerkerRechner.Models;

/// <summary>
/// Vordefinierte oder benutzerdefinierte Projekt-Vorlage.
/// Enthält vorausgefüllte Werte für einen oder mehrere Rechner.
/// </summary>
public class ProjectTemplate
{
    /// <summary>Eindeutige ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Anzeigename der Vorlage</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>RESX-Key für den lokalisierten Namen (nur bei eingebauten Vorlagen)</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Kategorie für Icon-Zuordnung (z.B. "bathroom", "living_room", "garden")</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Beschreibung oder RESX-Key</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Material Icons Kind für die UI</summary>
    public string IconKind { get; set; } = "FileDocumentOutline";

    /// <summary>Liste der Rechner mit Standardwerten</summary>
    public List<TemplateCalculatorEntry> Calculators { get; set; } = [];

    /// <summary>true = vom Benutzer erstellt, false = eingebaut</summary>
    public bool IsCustom { get; set; }

    /// <summary>Erstellungsdatum</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Ein einzelner Rechner-Eintrag innerhalb einer Vorlage
/// mit vorausgefüllten Standardwerten.
/// </summary>
public class TemplateCalculatorEntry
{
    /// <summary>Route zum Rechner (z.B. "TileCalculatorPage")</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>Rechner-Typ für Anzeige</summary>
    public CalculatorType CalculatorType { get; set; }

    /// <summary>Vorausgefüllte Werte (Property-Name → Wert als String)</summary>
    public Dictionary<string, string> DefaultValues { get; set; } = new();
}
