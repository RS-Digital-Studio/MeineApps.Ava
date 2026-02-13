using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Material.Icons;

namespace FitnessRechner.Converters;

/// <summary>
/// Konvertiert FoodCategory → MaterialIconKind für farbiges Kategorie-Icon.
/// </summary>
public class FoodCategoryToIconConverter : IValueConverter
{
    public static readonly FoodCategoryToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Models.FoodCategory category)
        {
            return category switch
            {
                Models.FoodCategory.Fruit => MaterialIconKind.FoodApple,
                Models.FoodCategory.Vegetable => MaterialIconKind.Carrot,
                Models.FoodCategory.Meat => MaterialIconKind.FoodSteak,
                Models.FoodCategory.Fish => MaterialIconKind.Fish,
                Models.FoodCategory.Dairy => MaterialIconKind.Cheese,
                Models.FoodCategory.Grain => MaterialIconKind.Grain,
                Models.FoodCategory.Beverage => MaterialIconKind.CupWater,
                Models.FoodCategory.Snack => MaterialIconKind.Cookie,
                Models.FoodCategory.FastFood => MaterialIconKind.Hamburger,
                Models.FoodCategory.Sweet => MaterialIconKind.Candy,
                Models.FoodCategory.Nut => MaterialIconKind.Peanut,
                Models.FoodCategory.Legume => MaterialIconKind.Seed,
                _ => MaterialIconKind.FoodApple
            };
        }
        return MaterialIconKind.FoodApple;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Konvertiert FoodCategory → SolidColorBrush für Kategorie-Hintergrund.
/// </summary>
public class FoodCategoryToColorConverter : IValueConverter
{
    public static readonly FoodCategoryToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Models.FoodCategory category)
        {
            var color = category switch
            {
                Models.FoodCategory.Fruit => Color.Parse("#22C55E"),      // Grün
                Models.FoodCategory.Vegetable => Color.Parse("#84CC16"),  // Hellgrün
                Models.FoodCategory.Meat => Color.Parse("#EF4444"),       // Rot
                Models.FoodCategory.Fish => Color.Parse("#3B82F6"),       // Blau
                Models.FoodCategory.Dairy => Color.Parse("#EAB308"),      // Gelb
                Models.FoodCategory.Grain => Color.Parse("#D97706"),      // Amber
                Models.FoodCategory.Beverage => Color.Parse("#06B6D4"),   // Cyan
                Models.FoodCategory.Snack => Color.Parse("#F59E0B"),      // Orange
                Models.FoodCategory.FastFood => Color.Parse("#F97316"),   // Dunkelorange
                Models.FoodCategory.Sweet => Color.Parse("#EC4899"),      // Pink
                Models.FoodCategory.Nut => Color.Parse("#A16207"),        // Braun
                Models.FoodCategory.Legume => Color.Parse("#65A30D"),     // Limette
                _ => Color.Parse("#6B7280")                               // Grau
            };
            return new SolidColorBrush(color);
        }
        return new SolidColorBrush(Color.Parse("#6B7280"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
