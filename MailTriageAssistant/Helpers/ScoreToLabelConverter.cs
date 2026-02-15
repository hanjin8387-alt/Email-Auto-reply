using System;
using System.Globalization;
using System.Windows.Data;

namespace MailTriageAssistant.Helpers;

public sealed class ScoreToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var score = value switch
        {
            int i => i,
            string s when int.TryParse(s, out var i) => i,
            _ => 0,
        };

        if (score >= 80) return "긴급";
        if (score >= 50) return "중요";
        if (score >= 30) return "보통";
        return "참고";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

