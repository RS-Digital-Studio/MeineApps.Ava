namespace ZeitManager.Models;

/// <summary>
/// Repräsentiert einen Sound (eingebaut, System-Ringtone oder benutzerdefiniert).
/// Uri ist null für eingebaute Töne, enthält content:// oder Dateipfad für externe Sounds.
/// </summary>
public record SoundItem(string Id, string Name, bool IsSystem = true, string? Uri = null);
