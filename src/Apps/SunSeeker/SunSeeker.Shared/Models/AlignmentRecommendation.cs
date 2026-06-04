namespace SunSeeker.Shared.Models;

/// <summary>
/// Empfohlene Soll-Ausrichtung. <see cref="TargetAzimuth"/> geografisch (true north),
/// 180 = Sued (Nordhalbkugel). <see cref="TargetTilt"/> ist der ideale Neigungswinkel gegen
/// die Horizontale; <see cref="RecommendedKickstandTilt"/> der naechste am Panel real
/// einstellbare Wert (bei festem Kickstand).
/// </summary>
public readonly record struct AlignmentRecommendation(
    AlignmentGoal Goal,
    double TargetAzimuth,
    double TargetTilt,
    double RecommendedKickstandTilt,
    string Explanation);
