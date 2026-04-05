using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Boost-Daten (Speed, XP, Rush, Soft-Cap).
/// Extrahiert aus GameState (V5) für bessere Strukturierung.
/// </summary>
public sealed class BoostData
{
    [JsonPropertyName("speedBoostEndTime")]
    public DateTime SpeedBoostEndTime { get; set; } = DateTime.MinValue;

    [JsonPropertyName("xpBoostEndTime")]
    public DateTime XpBoostEndTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Feierabend-Rush: 2h 2x-Boost, einmal täglich gratis.
    /// </summary>
    [JsonPropertyName("rushBoostEndTime")]
    public DateTime RushBoostEndTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Letztes Datum an dem der gratis Rush verwendet wurde.
    /// </summary>
    [JsonPropertyName("lastFreeRushUsed")]
    public DateTime LastFreeRushUsed { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public bool IsSpeedBoostActive => SpeedBoostEndTime > DateTime.UtcNow;

    [JsonIgnore]
    public bool IsXpBoostActive => XpBoostEndTime > DateTime.UtcNow;

    [JsonIgnore]
    public bool IsRushBoostActive => RushBoostEndTime > DateTime.UtcNow;

    /// <summary>
    /// Ob der Soft-Cap auf den Einkommens-Multiplikator aktiv ist (> 10x).
    /// Wird vom GameLoopService pro Tick gesetzt.
    /// </summary>
    [JsonIgnore]
    public bool IsSoftCapActive { get; set; }

    /// <summary>
    /// Wie viel Prozent des Einkommens durch den Soft-Cap verloren gehen (0-100).
    /// Für UI-Transparenz: Zeigt dem Spieler warum Boni gedeckelt werden.
    /// </summary>
    [JsonIgnore]
    public int SoftCapReductionPercent { get; set; }

    /// <summary>
    /// Ob der tägliche Gratis-Rush verfügbar ist (noch nicht heute verwendet).
    /// Zeitmanipulations-sicher: Wenn LastFreeRushUsed in der Zukunft liegt, ist future &lt; today false → blockiert.
    /// </summary>
    [JsonIgnore]
    public bool IsFreeRushAvailable => LastFreeRushUsed.Date < DateTime.UtcNow.Date;
}
