namespace RebornSaga.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Manifest für Asset-Delivery: Beschreibt alle verfügbaren Asset-Packs und deren Dateien.
/// Wird von Firebase Storage heruntergeladen und lokal gecacht.
/// </summary>
public sealed class AssetManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("minAppVersion")]
    public string MinAppVersion { get; set; } = "1.0.0";

    [JsonPropertyName("packs")]
    public Dictionary<string, AssetPack> Packs { get; set; } = new();
}

/// <summary>
/// Ein Asset-Pack (z.B. "characters", "backgrounds", "ui").
/// Required-Packs werden beim ersten Start heruntergeladen.
/// </summary>
public sealed class AssetPack
{
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("files")]
    public List<AssetFile> Files { get; set; } = new();
}

/// <summary>
/// Einzelne Datei innerhalb eines Asset-Packs.
/// Hash dient zur Delta-Update-Erkennung (SHA256).
/// </summary>
public sealed class AssetFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
