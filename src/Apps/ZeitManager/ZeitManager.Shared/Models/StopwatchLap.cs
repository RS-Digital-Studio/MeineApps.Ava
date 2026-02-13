using ZeitManager.Audio;

namespace ZeitManager.Models;

public record StopwatchLap(
    int LapNumber,
    TimeSpan LapTime,
    TimeSpan TotalTime,
    DateTime Timestamp)
{
    public string LapTimeFormatted => TimeFormatHelper.Format(LapTime);
    public string TotalTimeFormatted => TimeFormatHelper.Format(TotalTime);

    /// <summary>Markierung: Beste (schnellste) Runde.</summary>
    public bool IsBestLap { get; set; }

    /// <summary>Markierung: Schlechteste (langsamste) Runde.</summary>
    public bool IsWorstLap { get; set; }

    /// <summary>Delta zur vorherigen Runde (positiv = langsamer).</summary>
    public TimeSpan? DeltaToPrevious { get; set; }

    /// <summary>Formatiertes Delta mit Vorzeichen.</summary>
    public string DeltaFormatted => DeltaToPrevious switch
    {
        null => "",
        { TotalMilliseconds: 0 } => "0.00",
        { TotalMilliseconds: > 0 } d => $"+{TimeFormatHelper.Format(d)}",
        { } d => $"-{TimeFormatHelper.Format(d.Negate())}"
    };

    /// <summary>Farbe für Runden-Hervorhebung (Best=Grün, Worst=Rot, sonst Transparent).</summary>
    public string LapHighlightColor => IsBestLap ? "#22C55E" : IsWorstLap ? "#EF4444" : "Transparent";
}
