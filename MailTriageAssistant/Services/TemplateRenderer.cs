using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MailTriageAssistant.Services;

public sealed class TemplateRenderer : ITemplateRenderer
{
    private const int MaxValueLength = 200;
    private static readonly Regex PlaceholderRegex = new(@"\{(?<key>[^{}\r\n]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<string> ExtractPlaceholders(string templateBody)
    {
        if (string.IsNullOrEmpty(templateBody))
        {
            return Array.Empty<string>();
        }

        return PlaceholderRegex.Matches(templateBody)
            .Select(static m => m.Groups["key"].Value.Trim())
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string FillTemplate(string templateBody, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(templateBody))
        {
            return string.Empty;
        }

        var valueMap = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return PlaceholderRegex.Replace(templateBody, match =>
        {
            var key = match.Groups["key"].Value.Trim();
            if (!valueMap.TryGetValue(key, out var value))
            {
                return string.Empty;
            }

            return SanitizeValue(value ?? string.Empty);
        });
    }

    private static string SanitizeValue(string value)
    {
        var sanitized = value
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (sanitized.Length > MaxValueLength)
        {
            sanitized = sanitized[..MaxValueLength];
        }

        if (string.IsNullOrWhiteSpace(sanitized) || string.Equals(sanitized, "___", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return sanitized;
    }
}
