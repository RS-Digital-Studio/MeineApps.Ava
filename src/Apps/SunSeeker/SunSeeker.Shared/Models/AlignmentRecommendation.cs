namespace SunSeeker.Shared.Models;

/// <summary>
/// Empfohlene Soll-Ausrichtung. <see cref="TargetAzimuth"/> geografisch (true north),
/// 180 = Sued (Nordhalbkugel). <see cref="TargetTilt"/> ist der ideale Neigungswinkel gegen
/// die Horizontale; <see cref="RecommendedKickstandTilt"/> der naechste am Panel real
/// einstellbare Wert (bei festem Kickstand). Die Erklaerung wird vom UI-Layer lokalisiert
/// (aus <see cref="Goal"/> + Tageslicht), damit die Engine sprachneutral bleibt.
/// </summary>
public readonly record struct AlignmentRecommendation(
    AlignmentGoal Goal,
    double TargetAzimuth,
    double TargetTilt,
    double RecommendedKickstandTilt);
