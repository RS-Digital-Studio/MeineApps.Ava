using System.Globalization;
using Avalonia.Data.Converters;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Converts WorkshopType enum to MDI icon glyph string.
/// Use with FontFamily="MDI" on the TextBlock.
/// </summary>
public class WorkshopTypeToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not WorkshopType workshopType)
            return Icons.Plumber;

        return workshopType switch
        {
            WorkshopType.Carpenter => Icons.Carpenter,
            WorkshopType.Plumber => Icons.Plumber,
            WorkshopType.Electrician => Icons.Electrician,
            WorkshopType.Painter => Icons.Painter,
            WorkshopType.Roofer => Icons.Roofer,
            WorkshopType.Contractor => Icons.Contractor,
            WorkshopType.Architect => Icons.RulerSquare,
            WorkshopType.GeneralContractor => Icons.Crown,
            WorkshopType.MasterSmith => Icons.Anvil,
            WorkshopType.InnovationLab => Icons.FlaskOutline,
            _ => Icons.Plumber
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
