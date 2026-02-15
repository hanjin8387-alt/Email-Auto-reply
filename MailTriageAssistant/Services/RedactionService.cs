using System.Text.RegularExpressions;

namespace MailTriageAssistant.Services;

public sealed class RedactionService
{
    private static readonly (Regex Pattern, string Replacement)[] Rules = new[]
    {
        // Order matters: more specific patterns first.
        (new Regex(@"\d{4}-\d{4}-\d{4}-\d{4}", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[CARD]"),
        (new Regex(@"\d{6}-\d{7}", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[SSN]"),
        (new Regex(@"010-\d{4}-\d{4}", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[PHONE]"),
        (new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[EMAIL]"),
    };

    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = input;
        foreach (var (pattern, replacement) in Rules)
        {
            result = pattern.Replace(result, replacement);
        }

        return result;
    }
}

