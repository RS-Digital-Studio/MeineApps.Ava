using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartMeasure.Shared.Models;

/// <summary>Zu absteckender Punkt. Stammt entweder direkt aus einem <see cref="SurveyPoint"/>
/// oder aus einem GardenElement (z.B. Zaunpfosten aus Linie).
///
/// `IsReached` und `BestDistance` werden während der Stakeout-Session mutiert —
/// deshalb <see cref="ObservableObject"/> + `[ObservableProperty]`, damit die Liste in der
/// UI live updated.</summary>
public partial class StakeoutTarget : ObservableObject
{
    /// <summary>Angezeigter Name: "Grenzpunkt 1", "Weg-Anfang", "Pfosten Zaun-Süd"</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Ziel-Koordinate</summary>
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }

    /// <summary>Typ zur Icon-Anzeige (Punkt, Weg-Knotenpunkt, Gebäude-Ecke, ...)</summary>
    public StakeoutTargetSource Source { get; set; }

    /// <summary>Wurde bereits abgesteckt (User hat "Markiert" angeklickt oder Toleranz erreicht)</summary>
    [ObservableProperty] private bool _isReached;

    /// <summary>Beste erreichte Distanz während der Session (in Metern)</summary>
    [ObservableProperty] private double _bestDistance = double.PositiveInfinity;
}

public enum StakeoutTargetSource
{
    /// <summary>Aus einem gespeicherten Messpunkt</summary>
    SurveyPoint,
    /// <summary>Knotenpunkt einer Garten-Kontur (Weg, Zaun, Mauer, Beet-Ecke)</summary>
    GardenElement
}
