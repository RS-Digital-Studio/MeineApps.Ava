namespace RebornSaga.Models;

using RebornSaga.Models.Enums;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Ein Knoten in der Story-Struktur. Kann Dialog, Kampf, Auswahl etc. sein.
/// Wird aus JSON deserialisiert.
/// </summary>
public class StoryNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public NodeType Type { get; set; }

    [JsonPropertyName("backgroundKey")]
    public string? BackgroundKey { get; set; }

    [JsonPropertyName("musicKey")]
    public string? MusicKey { get; set; }

    [JsonPropertyName("speakers")]
    public List<SpeakerLine>? Speakers { get; set; }

    [JsonPropertyName("options")]
    public List<ChoiceOption>? Options { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("enemyId")]
    public string? EnemyId { get; set; }

    [JsonPropertyName("enemies")]
    public List<string>? Enemies { get; set; }

    [JsonPropertyName("effects")]
    public StoryEffects? Effects { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>
    /// Manga-Panel-Modus: "off" (Standard), "dual" (2 Panels), "triple" (3 Panels).
    /// </summary>
    [JsonPropertyName("mangaPanel")]
    public string? MangaPanel { get; set; }

    /// <summary>
    /// Visuelle Effekte die beim Betreten des Knotens ausgelöst werden (z.B. "glitch", "shake").
    /// </summary>
    [JsonPropertyName("visualEffects")]
    public List<string>? VisualEffects { get; set; }
}

/// <summary>
/// Eine Sprecher-Zeile innerhalb eines Dialog-Knotens.
/// </summary>
public class SpeakerLine
{
    [JsonPropertyName("character")]
    public string Character { get; set; } = "";

    [JsonPropertyName("emotion")]
    public string Emotion { get; set; } = "neutral";

    [JsonPropertyName("textKey")]
    public string TextKey { get; set; } = "";

    [JsonPropertyName("typewriterSpeed")]
    public float TypewriterSpeed { get; set; } = 30f;

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("pose")]
    public string Pose { get; set; } = "standing";
}

/// <summary>
/// Eine Auswahl-Option bei Choice-Knoten.
/// </summary>
public class ChoiceOption
{
    [JsonPropertyName("textKey")]
    public string TextKey { get; set; } = "";

    [JsonPropertyName("next")]
    public string Next { get; set; } = "";

    [JsonPropertyName("effects")]
    public StoryEffects? Effects { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }
}

/// <summary>
/// Effekte die beim Durchlaufen eines Knotens oder Wählen einer Option ausgelöst werden.
/// </summary>
public class StoryEffects
{
    [JsonPropertyName("karma")]
    public int Karma { get; set; }

    [JsonPropertyName("exp")]
    public int Exp { get; set; }

    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("affinity")]
    public Dictionary<string, int>? Affinity { get; set; }

    [JsonPropertyName("addItems")]
    public List<string>? AddItems { get; set; }

    [JsonPropertyName("removeItems")]
    public List<string>? RemoveItems { get; set; }

    [JsonPropertyName("fateChanged")]
    public string? FateChanged { get; set; }

    [JsonPropertyName("setFlags")]
    public List<string>? SetFlags { get; set; }

    [JsonPropertyName("removeFlags")]
    public List<string>? RemoveFlags { get; set; }
}
