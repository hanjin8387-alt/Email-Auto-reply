using System;
using System.Globalization;
using System.Windows;

namespace MailTriageAssistant.Helpers;

public static class LocalizedStrings
{
    public static string Get(string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        try
        {
            if (Application.Current?.TryFindResource(key) is string localized && !string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }
        }
        catch
        {
            // Ignore resource lookup failures and return fallback.
        }

        return fallback;
    }

    public static string GetFormat(string key, string fallbackFormat, params object[] args)
    {
        var format = Get(key, fallbackFormat);
        return string.Format(CultureInfo.CurrentCulture, format, args);
    }
}
