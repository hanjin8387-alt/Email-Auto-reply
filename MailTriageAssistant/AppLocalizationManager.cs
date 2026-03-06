using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MailTriageAssistant.Models;
using System;
using System.Linq;
using System.Windows;

namespace MailTriageAssistant;

public static class AppLocalizationManager
{
    public static void TryApplyLanguageResources(IServiceProvider services)
    {
        try
        {
            var triageOptions = services.GetService<IOptionsMonitor<TriageSettings>>();
            var language = triageOptions?.CurrentValue.Language ?? "ko";

            var source = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                ? "Resources/Strings.en.xaml"
                : "Resources/Strings.ko.xaml";

            var merged = Application.Current?.Resources.MergedDictionaries;
            if (merged is null)
            {
                return;
            }

            var existing = merged.FirstOrDefault(d =>
            {
                var original = d.Source?.OriginalString ?? string.Empty;
                return original.EndsWith("Resources/Strings.ko.xaml", StringComparison.OrdinalIgnoreCase)
                       || original.EndsWith("Resources/Strings.en.xaml", StringComparison.OrdinalIgnoreCase);
            });

            var uri = new Uri(source, UriKind.Relative);
            if (existing is null)
            {
                merged.Insert(0, new ResourceDictionary { Source = uri });
            }
            else
            {
                existing.Source = uri;
            }
        }
        catch
        {
            // Ignore language selection issues; default resources should still work.
        }
    }

    public static string ResolveLocalizedContentPath(
        string? configuredPath,
        string? language,
        string filePrefix,
        string extensionWithoutDot)
    {
        var normalized = (configuredPath ?? string.Empty).Trim();
        var koPath = $"{filePrefix}.ko.{extensionWithoutDot}";
        var enPath = $"{filePrefix}.en.{extensionWithoutDot}";

        if (!string.IsNullOrWhiteSpace(normalized)
            && !string.Equals(normalized, koPath, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, enPath, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var code = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ko";
        return $"{filePrefix}.{code}.{extensionWithoutDot}";
    }

    public static string GetResourceString(string key)
    {
        try
        {
            return (Application.Current?.TryFindResource(key) as string) ?? key;
        }
        catch
        {
            return key;
        }
    }
}
