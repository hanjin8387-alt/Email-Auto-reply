using System;
using System.Globalization;
using System.Windows.Data;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Helpers;

public sealed class CategoryToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryGetCategory(value, out var category))
        {
            return "•";
        }

        return category switch
        {
            EmailCategory.Action => "!",
            EmailCategory.Approval => "✓",
            EmailCategory.VIP => "★",
            EmailCategory.Meeting => "⌚",
            EmailCategory.Newsletter => "✉",
            EmailCategory.FYI => "i",
            _ => "•",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static bool TryGetCategory(object? value, out EmailCategory category)
    {
        if (value is EmailCategory c)
        {
            category = c;
            return true;
        }

        if (value is string s && Enum.TryParse(s, ignoreCase: true, out EmailCategory parsed))
        {
            category = parsed;
            return true;
        }

        category = default;
        return false;
    }
}

