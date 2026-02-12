using FinanzRechner.Models;
using MeineApps.Core.Ava.Localization;
using SkiaSharp;

namespace FinanzRechner.Helpers;

/// <summary>
/// Zentraler Helper für lokalisierte Kategorie-Namen, Icons und Farben.
/// </summary>
public static class CategoryLocalizationHelper
{
    /// <summary>Emoji-Icon pro Kategorie (zentralisiert, statt Duplikate in VMs)</summary>
    public static string GetCategoryIcon(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Food => "\U0001F354",
        ExpenseCategory.Transport => "\U0001F697",
        ExpenseCategory.Housing => "\U0001F3E0",
        ExpenseCategory.Entertainment => "\U0001F3AC",
        ExpenseCategory.Shopping => "\U0001F6D2",
        ExpenseCategory.Health => "\U0001F48A",
        ExpenseCategory.Education => "\U0001F4DA",
        ExpenseCategory.Bills => "\U0001F4C4",
        ExpenseCategory.Salary => "\U0001F4B0",
        ExpenseCategory.Freelance => "\U0001F4BC",
        ExpenseCategory.Investment => "\U0001F4C8",
        ExpenseCategory.Gift => "\U0001F381",
        ExpenseCategory.OtherIncome => "\U0001F4B5",
        _ => "\U0001F4E6"
    };

    /// <summary>SKColor pro Kategorie für Charts (zentralisiert)</summary>
    public static SKColor GetCategoryColor(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Food => new SKColor(0xFF, 0x98, 0x00),           // Orange
        ExpenseCategory.Transport => new SKColor(0x21, 0x96, 0xF3),      // Blue
        ExpenseCategory.Housing => new SKColor(0x9C, 0x27, 0xB0),        // Purple
        ExpenseCategory.Entertainment => new SKColor(0xE9, 0x1E, 0x63),  // Pink
        ExpenseCategory.Shopping => new SKColor(0x00, 0xBC, 0xD4),       // Cyan
        ExpenseCategory.Health => new SKColor(0xF4, 0x43, 0x36),         // Red
        ExpenseCategory.Education => new SKColor(0x3F, 0x51, 0xB5),      // Indigo
        ExpenseCategory.Bills => new SKColor(0x60, 0x7D, 0x8B),          // Blue-grey
        ExpenseCategory.Other => new SKColor(0x79, 0x55, 0x48),          // Brown
        ExpenseCategory.Salary => new SKColor(0x4C, 0xAF, 0x50),         // Green
        ExpenseCategory.Freelance => new SKColor(0x00, 0x96, 0x88),      // Teal
        ExpenseCategory.Investment => new SKColor(0x8B, 0xC3, 0x4A),     // Light green
        ExpenseCategory.Gift => new SKColor(0xFF, 0xC1, 0x07),           // Amber
        ExpenseCategory.OtherIncome => new SKColor(0xCD, 0xDC, 0x39),    // Lime
        _ => new SKColor(0x9E, 0x9E, 0x9E)                               // Grey
    };

    public static string GetCategoryKey(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Food => "CategoryFood",
        ExpenseCategory.Transport => "CategoryTransport",
        ExpenseCategory.Housing => "CategoryHousing",
        ExpenseCategory.Entertainment => "CategoryEntertainment",
        ExpenseCategory.Shopping => "CategoryShopping",
        ExpenseCategory.Health => "CategoryHealth",
        ExpenseCategory.Education => "CategoryEducation",
        ExpenseCategory.Bills => "CategoryBills",
        ExpenseCategory.Other => "CategoryOther",
        ExpenseCategory.Salary => "CategorySalary",
        ExpenseCategory.Freelance => "CategoryFreelance",
        ExpenseCategory.Investment => "CategoryInvestment",
        ExpenseCategory.Gift => "CategoryGift",
        ExpenseCategory.OtherIncome => "CategoryOtherIncome",
        _ => "CategoryOther"
    };

    public static string GetLocalizedName(ExpenseCategory category, ILocalizationService? localizationService)
    {
        if (localizationService == null)
            return GetFallbackName(category);

        var key = GetCategoryKey(category);
        return localizationService.GetString(key) ?? GetFallbackName(category);
    }

    private static string GetFallbackName(ExpenseCategory category)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        return category switch
        {
            ExpenseCategory.Food => culture switch
            {
                "de" => "Lebensmittel", "es" => "Comida", "fr" => "Nourriture",
                "it" => "Cibo", "pt" => "Comida", _ => "Food"
            },
            ExpenseCategory.Transport => culture switch
            {
                "de" => "Transport", "es" => "Transporte", "fr" => "Transport",
                "it" => "Trasporto", "pt" => "Transporte", _ => "Transport"
            },
            ExpenseCategory.Housing => culture switch
            {
                "de" => "Wohnen", "es" => "Vivienda", "fr" => "Logement",
                "it" => "Casa", "pt" => "Moradia", _ => "Housing"
            },
            ExpenseCategory.Entertainment => culture switch
            {
                "de" => "Unterhaltung", "es" => "Entretenimiento", "fr" => "Divertissement",
                "it" => "Intrattenimento", "pt" => "Entretenimento", _ => "Entertainment"
            },
            ExpenseCategory.Shopping => culture switch
            {
                "de" => "Einkaufen", "es" => "Compras", "fr" => "Achats",
                "it" => "Acquisti", "pt" => "Compras", _ => "Shopping"
            },
            ExpenseCategory.Health => culture switch
            {
                "de" => "Gesundheit", "es" => "Salud", "fr" => "Sant\u00e9",
                "it" => "Salute", "pt" => "Sa\u00fade", _ => "Health"
            },
            ExpenseCategory.Education => culture switch
            {
                "de" => "Bildung", "es" => "Educaci\u00f3n", "fr" => "\u00c9ducation",
                "it" => "Educazione", "pt" => "Educa\u00e7\u00e3o", _ => "Education"
            },
            ExpenseCategory.Bills => culture switch
            {
                "de" => "Rechnungen", "es" => "Facturas", "fr" => "Factures",
                "it" => "Bollette", "pt" => "Contas", _ => "Bills"
            },
            ExpenseCategory.Other => culture switch
            {
                "de" => "Sonstiges", "es" => "Otros", "fr" => "Autres",
                "it" => "Altro", "pt" => "Outros", _ => "Other"
            },
            ExpenseCategory.Salary => culture switch
            {
                "de" => "Gehalt", "es" => "Salario", "fr" => "Salaire",
                "it" => "Stipendio", "pt" => "Sal\u00e1rio", _ => "Salary"
            },
            ExpenseCategory.Freelance => culture switch
            {
                "de" => "Freiberuflich", "es" => "Aut\u00f3nomo", "fr" => "Freelance",
                "it" => "Freelance", "pt" => "Freelancer", _ => "Freelance"
            },
            ExpenseCategory.Investment => culture switch
            {
                "de" => "Kapitalertr\u00e4ge", "es" => "Inversiones", "fr" => "Investissement",
                "it" => "Investimento", "pt" => "Investimento", _ => "Investment"
            },
            ExpenseCategory.Gift => culture switch
            {
                "de" => "Geschenk", "es" => "Regalo", "fr" => "Cadeau",
                "it" => "Regalo", "pt" => "Presente", _ => "Gift"
            },
            ExpenseCategory.OtherIncome => culture switch
            {
                "de" => "Sonstiges Einkommen", "es" => "Otros Ingresos", "fr" => "Autres Revenus",
                "it" => "Altre Entrate", "pt" => "Outras Receitas", _ => "Other Income"
            },
            _ => category.ToString()
        };
    }
}
