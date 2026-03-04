using System;
using System.Globalization;
using System.Windows;
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

        if (score >= 80) return Resolve("Str.ScoreLabel.High", "High");
        if (score >= 50) return Resolve("Str.ScoreLabel.Medium", "Medium");
        if (score >= 30) return Resolve("Str.ScoreLabel.Normal", "Normal");
        return Resolve("Str.ScoreLabel.Low", "Low");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static string Resolve(string key, string fallback)
    {
        try
        {
            return (Application.Current?.TryFindResource(key) as string) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
