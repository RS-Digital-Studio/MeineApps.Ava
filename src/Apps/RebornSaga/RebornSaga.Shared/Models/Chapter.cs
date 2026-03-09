namespace RebornSaga.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Ein Kapitel der Story. Enthält alle Knoten und Metadaten.
/// Wird aus JSON deserialisiert.
/// </summary>
public class Chapter
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("titleKey")]
    public string TitleKey { get; set; } = "";

    [JsonPropertyName("isProlog")]
    public bool IsProlog { get; set; }

    [JsonPropertyName("goldCost")]
    public int GoldCost { get; set; }

    [JsonPropertyName("nodes")]
    public List<StoryNode> Nodes { get; set; } = new();
}
