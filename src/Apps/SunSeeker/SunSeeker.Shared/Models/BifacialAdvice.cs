namespace SunSeeker.Shared.Models;

/// <summary>
/// Bifazial-Empfehlung für einen gewählten Untergrund. Der Mehrertrag wird als BEREICH
/// angegeben (nicht als Punktwert), weil er von Albedo, Bodenabstand, Diffusanteil und
/// Sonnenstand abhängt — eine Punktzahl wäre Pseudo-Genauigkeit. Werte 0..1 (Anteil).
/// <see cref="TiltBonusDegrees"/> ist der empfohlene Steilwinkel-Zuschlag (bifaziale Panels
/// profitieren bei hoher Albedo von steilerer Neigung, weil die Rückseite mehr Boden sieht).
/// </summary>
public readonly record struct BifacialAdvice(
    GroundType Ground,
    double Albedo,
    double EstimatedGainLow,
    double EstimatedGainHigh,
    double TiltBonusDegrees,
    IReadOnlyList<string> Tips);
