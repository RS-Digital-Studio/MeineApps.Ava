namespace RebornSaga.Models;

using RebornSaga.Models.Enums;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Ein Knoten auf der Overworld-Map. Definiert Position, Typ und Verbindungen.
/// </summary>
public class MapNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MapNodeType Type { get; set; } = MapNodeType.Story;

    /// <summary>
    /// Normalisierte Position (0-1 Bereich, wird auf Canvas-Größe skaliert).
    /// </summary>
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    /// <summary>
    /// IDs der verbundenen Knoten (für Pfad-Rendering).
    /// </summary>
    [JsonPropertyName("connections")]
    public List<string> Connections { get; set; } = new();

    /// <summary>
    /// Story-Knoten-ID die bei Betreten geladen wird.
    /// </summary>
    [JsonPropertyName("storyNodeId")]
    public string? StoryNodeId { get; set; }

    /// <summary>
    /// Gegner-ID für Boss/Dungeon-Knoten.
    /// </summary>
    [JsonPropertyName("enemyId")]
    public string? EnemyId { get; set; }

    /// <summary>
    /// Optionale Bedingung (Story-Flag) die erfüllt sein muss um den Knoten zu betreten.
    /// </summary>
    [JsonPropertyName("requiredFlag")]
    public string? RequiredFlag { get; set; }

    /// <summary>
    /// Hintergrund-Typ der Szene wenn dieser Knoten betreten wird.
    /// </summary>
    [JsonPropertyName("background")]
    public string? Background { get; set; }

    // --- Runtime-State (nicht serialisiert) ---

    /// <summary>Wurde dieser Knoten bereits abgeschlossen?</summary>
    [JsonIgnore]
    public bool IsCompleted { get; set; }

    /// <summary>Ist dieser Knoten der aktuelle Standort des Spielers?</summary>
    [JsonIgnore]
    public bool IsCurrent { get; set; }

    /// <summary>Kann der Spieler diesen Knoten betreten? (Pfad freigeschaltet)</summary>
    [JsonIgnore]
    public bool IsAccessible { get; set; }

    /// <summary>Ist der Knoten sichtbar oder noch im Nebel?</summary>
    [JsonIgnore]
    public bool IsRevealed { get; set; } = true;
}

/// <summary>
/// Map-Definition für ein Kapitel. Enthält alle Knoten und den Start-Knoten.
/// </summary>
public class ChapterMap
{
    [JsonPropertyName("chapterId")]
    public string ChapterId { get; set; } = "";

    [JsonPropertyName("chapterNameKey")]
    public string ChapterNameKey { get; set; } = "";

    [JsonPropertyName("startNodeId")]
    public string StartNodeId { get; set; } = "";

    [JsonPropertyName("nodes")]
    public List<MapNode> Nodes { get; set; } = new();
}
