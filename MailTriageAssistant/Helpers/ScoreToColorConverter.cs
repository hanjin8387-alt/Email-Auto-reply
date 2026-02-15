using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MailTriageAssistant.Helpers;

public sealed class ScoreToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var score = value switch
        {
            int i => i,
            string s when int.TryParse(s, out var i) => i,
            _ => 0,
        };

        if (score >= 80) return Brushes.IndianRed;
        if (score >= 50) return Brushes.DarkOrange;
        if (score >= 30) return Brushes.SeaGreen;
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

