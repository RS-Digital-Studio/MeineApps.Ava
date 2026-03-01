namespace FitnessRechner.Models;

/// <summary>
/// Interface fuer die Berechnungs-Engine aller Fitness-Rechner.
/// Ermoeglicht Testbarkeit und lose Kopplung via DI.
/// </summary>
public interface IFitnessEngine
{
    /// <summary>
    /// Berechnet den Body Mass Index.
    /// </summary>
    /// <param name="weightKg">Gewicht in kg</param>
    /// <param name="heightCm">Groesse in cm</param>
    /// <returns>BMI-Wert und Kategorie</returns>
    BmiResult CalculateBmi(double weightKg, double heightCm);

    /// <summary>
    /// Berechnet den taeglichen Kalorienbedarf nach Mifflin-St Jeor.
    /// </summary>
    /// <param name="weightKg">Gewicht in kg</param>
    /// <param name="heightCm">Groesse in cm</param>
    /// <param name="ageYears">Alter in Jahren</param>
    /// <param name="isMale">true = maennlich, false = weiblich</param>
    /// <param name="activityLevel">Aktivitaetslevel (1.2 - 1.9)</param>
    /// <returns>Grundumsatz und Gesamtbedarf</returns>
    CaloriesResult CalculateCalories(double weightKg, double heightCm, int ageYears, bool isMale, double activityLevel);

    /// <summary>
    /// Berechnet den taeglichen Wasserbedarf.
    /// </summary>
    /// <param name="weightKg">Gewicht in kg</param>
    /// <param name="activityMinutes">Sportminuten pro Tag</param>
    /// <param name="isHotWeather">Heisses Wetter?</param>
    /// <returns>Wasserbedarf in Litern</returns>
    WaterResult CalculateWater(double weightKg, int activityMinutes, bool isHotWeather);

    /// <summary>
    /// Berechnet das Idealgewicht nach verschiedenen Formeln.
    /// </summary>
    /// <param name="heightCm">Groesse in cm</param>
    /// <param name="isMale">true = maennlich, false = weiblich</param>
    /// <param name="ageYears">Alter in Jahren</param>
    /// <returns>Idealgewicht nach Broca und Creff</returns>
    IdealWeightResult CalculateIdealWeight(double heightCm, bool isMale, int ageYears);

    /// <summary>
    /// Berechnet den Koerperfettanteil nach der Navy-Methode.
    /// </summary>
    /// <param name="heightCm">Groesse in cm</param>
    /// <param name="neckCm">Halsumfang in cm</param>
    /// <param name="waistCm">Taillenumfang in cm</param>
    /// <param name="hipCm">Hueftumfang in cm (nur fuer Frauen)</param>
    /// <param name="isMale">true = maennlich, false = weiblich</param>
    /// <returns>Koerperfettanteil und Kategorie</returns>
    BodyFatResult CalculateBodyFat(double heightCm, double neckCm, double waistCm, double hipCm, bool isMale);
}
