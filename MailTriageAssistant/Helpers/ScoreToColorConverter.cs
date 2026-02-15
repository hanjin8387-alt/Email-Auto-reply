using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MailTriageAssistant.Helpers;

public sealed class ScoreToColorConverter : IValueConverter
{
    private const int HighThreshold = 80;
    private const int MediumThreshold = 50;
    private const int LowThreshold = 30;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var score = value switch
        {
            int i => i,
            string s when int.TryParse(s, out var i) => i,
            _ => 0,
        };

        if (score >= HighThreshold) return Brushes.DarkRed;
        if (score >= MediumThreshold) return Brushes.DarkGoldenrod;
        if (score >= LowThreshold) return Brushes.DarkGreen;
        return Brushes.DimGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
