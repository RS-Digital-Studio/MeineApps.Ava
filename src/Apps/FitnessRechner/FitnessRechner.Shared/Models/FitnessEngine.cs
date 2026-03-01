namespace FitnessRechner.Models;

/// <summary>
/// Berechnungs-Engine für alle Fitness-Rechner
/// </summary>
public class FitnessEngine : IFitnessEngine
{
    #region BMI (Body Mass Index)

    /// <summary>
    /// Berechnet den Body Mass Index
    /// </summary>
    /// <param name="weightKg">Gewicht in kg</param>
    /// <param name="heightCm">Größe in cm</param>
    /// <returns>BMI-Wert und Kategorie</returns>
    public BmiResult CalculateBmi(double weightKg, double heightCm)
    {
        var heightM = heightCm / 100;
        var bmi = weightKg / (heightM * heightM);
        var category = GetBmiCategory(bmi);

        return new BmiResult
        {
            Weight = weightKg,
            Height = heightCm,
            Bmi = bmi,
            Category = category,
            MinHealthyWeight = 18.5 * heightM * heightM,
            MaxHealthyWeight = 24.9 * heightM * heightM
        };
    }

    private BmiCategory GetBmiCategory(double bmi)
    {
        return bmi switch
        {
            < 16 => BmiCategory.SevereUnderweight,
            < 17 => BmiCategory.ModerateUnderweight,
            < 18.5 => BmiCategory.MildUnderweight,
            < 25 => BmiCategory.Normal,
            < 30 => BmiCategory.Overweight,
            < 35 => BmiCategory.ObeseClass1,
            < 40 => BmiCategory.ObeseClass2,
            _ => BmiCategory.ObeseClass3
        };
    }

    #endregion

    #region Kalorienbedarf (Mifflin-St Jeor)

    /// <summary>
    /// Berechnet den täglichen Kalorienbedarf nach Mifflin-St Jeor
    /// </summary>
    /// <param name="weightKg">Gewicht in kg</param>
    /// <param name="heightCm">Größe in cm</param>
    /// <param name="ageYears">Alter in Jahren</param>
    /// <param name="isMale">true = männlich, false = weiblich</param>
    /// <param name="activityLevel">Aktivitätslevel (1.2 - 1.9)</param>
    /// <returns>Grundumsatz und Gesamtbedarf</returns>
    public CaloriesResult CalculateCalories(
        double weightKg,
        double heightCm,
        int ageYears,
        bool isMale,
        double activityLevel)
    {
        // Mifflin-St Jeor Formel
        double bmr;
        if (isMale)
        {
            bmr = (10 * weightKg) + (6.25 * heightCm) - (5 * ageYears) + 5;
        }
        else
        {
            bmr = (10 * weightKg) + (6.25 * heightCm) - (5 * ageYears) - 161;
        }

        var tdee = bmr * activityLevel;
        var weightLoss = tdee - 500; // 0.5kg/Woche
        var weightGain = tdee + 500; // 0.5kg/Woche

        return new CaloriesResult
        {
            Weight = weightKg,
            Height = heightCm,
            Age = ageYears,
            IsMale = isMale,
            ActivityLevel = activityLevel,
            Bmr = bmr,
            Tdee = tdee,
            WeightLossCalories = weightLoss,
            WeightGainCalories = weightGain
        };
    }

    #endregion

    #region Wasserbedarf

    /// <summary>
    /// Berechnet den täglichen Wasserbedarf
    /// </summary>
    /// <param name="weightKg">Gewicht in kg</param>
    /// <param name="activityMinutes">Sportminuten pro Tag</param>
    /// <param name="isHotWeather">Heißes Wetter?</param>
    /// <returns>Wasserbedarf in Litern</returns>
    public WaterResult CalculateWater(double weightKg, int activityMinutes, bool isHotWeather)
    {
        // Basis: 30-35ml pro kg Körpergewicht
        var baseWater = weightKg * 0.033;

        // Sport: +0.35L pro 30 Minuten
        var activityWater = (activityMinutes / 30.0) * 0.35;

        // Hitze: +0.5L
        var heatWater = isHotWeather ? 0.5 : 0;

        var totalWater = baseWater + activityWater + heatWater;
        var glasses = (int)Math.Ceiling(totalWater / 0.25); // 250ml Gläser

        return new WaterResult
        {
            Weight = weightKg,
            ActivityMinutes = activityMinutes,
            IsHotWeather = isHotWeather,
            BaseWater = baseWater,
            ActivityWater = activityWater,
            HeatWater = heatWater,
            TotalLiters = totalWater,
            Glasses = glasses
        };
    }

    #endregion

    #region Idealgewicht

    /// <summary>
    /// Berechnet das Idealgewicht nach verschiedenen Formeln
    /// </summary>
    /// <param name="heightCm">Größe in cm</param>
    /// <param name="isMale">true = männlich, false = weiblich</param>
    /// <param name="ageYears">Alter in Jahren</param>
    /// <returns>Idealgewicht nach Broca und Creff</returns>
    public IdealWeightResult CalculateIdealWeight(double heightCm, bool isMale, int ageYears)
    {
        // Broca-Formel (einfach)
        var broca = heightCm - 100;
        if (!isMale)
        {
            broca *= 0.85; // Frauen: 15% weniger
        }

        // Creff-Formel (berücksichtigt Alter)
        var heightFactor = heightCm - 100 + (ageYears / 10.0);
        var creff = heightFactor * 0.9;
        if (!isMale)
        {
            creff *= 0.9; // Frauen: 10% weniger
        }

        // BMI-basierter Bereich (18.5-24.9)
        var heightM = heightCm / 100;
        var minBmi = 18.5 * heightM * heightM;
        var maxBmi = 24.9 * heightM * heightM;

        return new IdealWeightResult
        {
            Height = heightCm,
            IsMale = isMale,
            Age = ageYears,
            BrocaWeight = broca,
            CreffWeight = creff,
            MinHealthyWeight = minBmi,
            MaxHealthyWeight = maxBmi,
            AverageIdeal = (broca + creff) / 2
        };
    }

    #endregion

    #region Körperfett (Navy-Methode)

    /// <summary>
    /// Berechnet den Körperfettanteil nach der Navy-Methode
    /// </summary>
    /// <param name="heightCm">Größe in cm</param>
    /// <param name="neckCm">Halsumfang in cm</param>
    /// <param name="waistCm">Taillenumfang in cm</param>
    /// <param name="hipCm">Hüftumfang in cm (nur für Frauen)</param>
    /// <param name="isMale">true = männlich, false = weiblich</param>
    /// <returns>Körperfettanteil und Kategorie</returns>
    public BodyFatResult CalculateBodyFat(
        double heightCm,
        double neckCm,
        double waistCm,
        double hipCm,
        bool isMale)
    {
        // Navy-Methode: Differenz muss positiv sein für Log10
        var diff = isMale ? waistCm - neckCm : waistCm + hipCm - neckCm;
        if (diff <= 0)
        {
            return new BodyFatResult
            {
                Height = heightCm, Neck = neckCm, Waist = waistCm,
                Hip = hipCm, IsMale = isMale,
                BodyFatPercent = 0, Category = BodyFatCategory.Essential
            };
        }

        double bodyFat;

        if (isMale)
        {
            // Männer: 86.010 × log10(waist - neck) - 70.041 × log10(height) + 36.76
            bodyFat = 86.010 * Math.Log10(waistCm - neckCm) -
                     70.041 * Math.Log10(heightCm) + 36.76;
        }
        else
        {
            // Frauen: 163.205 × log10(waist + hip - neck) - 97.684 × log10(height) - 78.387
            bodyFat = 163.205 * Math.Log10(waistCm + hipCm - neckCm) -
                     97.684 * Math.Log10(heightCm) - 78.387;
        }

        bodyFat = Math.Max(0, Math.Min(60, bodyFat)); // Begrenzen auf 0-60%
        var category = GetBodyFatCategory(bodyFat, isMale);

        return new BodyFatResult
        {
            Height = heightCm,
            Neck = neckCm,
            Waist = waistCm,
            Hip = hipCm,
            IsMale = isMale,
            BodyFatPercent = bodyFat,
            Category = category
        };
    }

    private BodyFatCategory GetBodyFatCategory(double bodyFat, bool isMale)
    {
        if (isMale)
        {
            return bodyFat switch
            {
                < 6 => BodyFatCategory.Essential,
                < 14 => BodyFatCategory.Athletes,
                < 18 => BodyFatCategory.Fitness,
                < 25 => BodyFatCategory.Average,
                _ => BodyFatCategory.Obese
            };
        }
        else
        {
            return bodyFat switch
            {
                < 14 => BodyFatCategory.Essential,
                < 21 => BodyFatCategory.Athletes,
                < 25 => BodyFatCategory.Fitness,
                < 32 => BodyFatCategory.Average,
                _ => BodyFatCategory.Obese
            };
        }
    }

    #endregion
}

#region Enums

public enum BmiCategory
{
    SevereUnderweight,
    ModerateUnderweight,
    MildUnderweight,
    Normal,
    Overweight,
    ObeseClass1,
    ObeseClass2,
    ObeseClass3
}

public enum BodyFatCategory
{
    Essential,
    Athletes,
    Fitness,
    Average,
    Obese
}

#endregion

#region Result Types

public record BmiResult
{
    public double Weight { get; init; }
    public double Height { get; init; }
    public double Bmi { get; init; }
    public BmiCategory Category { get; init; }
    public double MinHealthyWeight { get; init; }
    public double MaxHealthyWeight { get; init; }
}

public record CaloriesResult
{
    public double Weight { get; init; }
    public double Height { get; init; }
    public int Age { get; init; }
    public bool IsMale { get; init; }
    public double ActivityLevel { get; init; }
    public double Bmr { get; init; }
    public double Tdee { get; init; }
    public double WeightLossCalories { get; init; }
    public double WeightGainCalories { get; init; }
}

public record WaterResult
{
    public double Weight { get; init; }
    public int ActivityMinutes { get; init; }
    public bool IsHotWeather { get; init; }
    public double BaseWater { get; init; }
    public double ActivityWater { get; init; }
    public double HeatWater { get; init; }
    public double TotalLiters { get; init; }
    public int Glasses { get; init; }
}

public record IdealWeightResult
{
    public double Height { get; init; }
    public bool IsMale { get; init; }
    public int Age { get; init; }
    public double BrocaWeight { get; init; }
    public double CreffWeight { get; init; }
    public double MinHealthyWeight { get; init; }
    public double MaxHealthyWeight { get; init; }
    public double AverageIdeal { get; init; }
}

public record BodyFatResult
{
    public double Height { get; init; }
    public double Neck { get; init; }
    public double Waist { get; init; }
    public double Hip { get; init; }
    public bool IsMale { get; init; }
    public double BodyFatPercent { get; init; }
    public BodyFatCategory Category { get; init; }
}

#endregion
