using System;
using System.Globalization;
using System.Windows.Data;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Helpers;

public sealed class CategoryToLocalizedLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryGetCategory(value, out var category))
        {
            return LocalizedStrings.Get("Str.Filter.Category.Other", "Other");
        }

        return category switch
        {
            EmailCategory.Action => LocalizedStrings.Get("Str.Filter.Category.Action", "Action"),
            EmailCategory.Approval => LocalizedStrings.Get("Str.Filter.Category.Approval", "Approval"),
            EmailCategory.VIP => LocalizedStrings.Get("Str.Filter.Category.Vip", "VIP"),
            EmailCategory.Meeting => LocalizedStrings.Get("Str.Filter.Category.Meeting", "Meeting"),
            EmailCategory.Newsletter => LocalizedStrings.Get("Str.Filter.Category.Newsletter", "Newsletter"),
            EmailCategory.FYI => LocalizedStrings.Get("Str.Filter.Category.Fyi", "FYI"),
            _ => LocalizedStrings.Get("Str.Filter.Category.Other", "Other"),
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
