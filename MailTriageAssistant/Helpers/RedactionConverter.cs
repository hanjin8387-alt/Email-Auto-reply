using System;
using System.Globalization;
using System.Windows.Data;
using MailTriageAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Helpers;

public sealed class RedactionConverter : IValueConverter
{
    private readonly RedactionService _redactionService = new(NullLogger<RedactionService>.Instance);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        return _redactionService.Redact(text);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
