namespace FitnessRechner.Models;

/// <summary>
/// Repräsentiert ein Lebensmittel mit Nährwertangaben
/// </summary>
public class FoodItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public FoodCategory Category { get; set; }
    public double CaloriesPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FatPer100g { get; set; }
    public double FiberPer100g { get; set; }
    public string DefaultPortion { get; set; } = "100g";
    public double DefaultPortionGrams { get; set; } = 100;
}

/// <summary>
/// Lebensmittel-Kategorien
/// </summary>
public enum FoodCategory
{
    Fruit,          // Obst
    Vegetable,      // Gemüse
    Meat,           // Fleisch
    Fish,           // Fisch
    Dairy,          // Milchprodukte
    Grain,          // Getreide/Brot
    Beverage,       // Getränke
    Snack,          // Snacks
    FastFood,       // Fast Food
    Sweet,          // Süßigkeiten
    Nut,            // Nüsse
    Legume,         // Hülsenfrüchte
    Other           // Sonstiges
}

/// <summary>
/// Suchergebnis mit Relevanz-Score
/// </summary>
public class FoodSearchResult
{
    public FoodItem Food { get; set; } = null!;
    public double Score { get; set; }
    public string MatchedOn { get; set; } = "";

    /// <summary>Kennzeichnet ob das Ergebnis von der Open Food Facts API stammt.</summary>
    public bool IsFromApi { get; set; }
}

/// <summary>
/// Eintrag für Kalorienverfolgung
/// </summary>
public class FoodLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Date { get; set; } = DateTime.Today;
    public string FoodName { get; set; } = "";
    public double Grams { get; set; }
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public MealType Meal { get; set; }
}

public enum MealType
{
    Breakfast,  // Frühstück
    Lunch,      // Mittagessen
    Dinner,     // Abendessen
    Snack       // Snack
}

/// <summary>
/// Tagesübersicht der Kalorien
/// </summary>
public record DailyNutritionSummary(
    DateTime Date,
    double TotalCalories,
    double TotalProtein,
    double TotalCarbs,
    double TotalFat,
    int EntryCount
);
