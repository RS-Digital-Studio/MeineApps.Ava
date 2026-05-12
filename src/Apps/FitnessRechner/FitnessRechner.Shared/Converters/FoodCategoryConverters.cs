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
    // Bug-Fix: Vorherige Self-Assignments (FruitBrush = FruitBrush) erzeugten null-Brushes
    // (CS1717-Warnings). Brushes werden jetzt einmalig im static-Constructor angelegt — gespart
    // wird die SolidColorBrush-Allokation pro Convert-Aufruf bei haeufiger ListView-Nutzung.
    private static readonly IBrush FruitBrush = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush VegetableBrush = new SolidColorBrush(Color.Parse("#84CC16"));
    private static readonly IBrush MeatBrush = new SolidColorBrush(Color.Parse("#EF4444"));
    private static readonly IBrush FishBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush DairyBrush = new SolidColorBrush(Color.Parse("#EAB308"));
    private static readonly IBrush GrainBrush = new SolidColorBrush(Color.Parse("#D97706"));
    private static readonly IBrush BeverageBrush = new SolidColorBrush(Color.Parse("#06B6D4"));
    private static readonly IBrush SnackBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush FastFoodBrush = new SolidColorBrush(Color.Parse("#F97316"));
    private static readonly IBrush SweetBrush = new SolidColorBrush(Color.Parse("#EC4899"));
    private static readonly IBrush NutBrush = new SolidColorBrush(Color.Parse("#A16207"));
    private static readonly IBrush LegumeBrush = new SolidColorBrush(Color.Parse("#65A30D"));
    private static readonly IBrush FoodDefaultBrush = new SolidColorBrush(Color.Parse("#6B7280"));

    public static readonly FoodCategoryToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Models.FoodCategory category)
        {
            return category switch
            {
                Models.FoodCategory.Fruit => FruitBrush,
                Models.FoodCategory.Vegetable => VegetableBrush,
                Models.FoodCategory.Meat => MeatBrush,
                Models.FoodCategory.Fish => FishBrush,
                Models.FoodCategory.Dairy => DairyBrush,
                Models.FoodCategory.Grain => GrainBrush,
                Models.FoodCategory.Beverage => BeverageBrush,
                Models.FoodCategory.Snack => SnackBrush,
                Models.FoodCategory.FastFood => FastFoodBrush,
                Models.FoodCategory.Sweet => SweetBrush,
                Models.FoodCategory.Nut => NutBrush,
                Models.FoodCategory.Legume => LegumeBrush,
                _ => FoodDefaultBrush
            };
        }
        return FoodDefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
