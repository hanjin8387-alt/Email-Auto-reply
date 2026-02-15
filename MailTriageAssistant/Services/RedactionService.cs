using System.Text;
using System.Text.RegularExpressions;

namespace MailTriageAssistant.Services;

public sealed class RedactionService : IRedactionService
{
    private static readonly (Regex Pattern, string Replacement)[] Rules = new[]
    {
        // Order matters: more specific patterns first.
        (new Regex(
            @"(?i)\b(?<name>token|access_token|id_token|refresh_token|api[-_]?key|apikey|sig|signature|secret|password|pwd|code|key)=(?<value>[^&\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "${name}=[URL_TOKEN]"),
        (new Regex(
            @"(?i)\b(?<label>account|acct|iban|계좌(?:번호)?)\b(?<sep>\s*[:：]?\s*)(?<number>(?<!\d)\d{2,6}(?:[- ]\d{2,6}){1,3}(?!\d))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "${label}${sep}[ACCOUNT]"),
        (new Regex(
            @"(?i)\b(?<label>passport|여권(?:번호)?)\b(?<sep>\s*[:：]?\s*)(?<number>[A-Z]\d{8}|\d{9})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "${label}${sep}[PASSPORT]"),
        (new Regex(@"(?<!\d)\d{4}-\d{4}-\d{4}-\d{4}(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[CARD]"),
        (new Regex(@"(?<!\d)\d{4}(?:\s+\d{4}){3}(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[CARD]"),
        (new Regex(@"(?<!\d)\d{6}-\d{7}(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[SSN]"),
        (new Regex(@"(?<!\d)\d{6}[1-8]\d{6}(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[SSN]"),
        (new Regex(@"(?<!\d)010-\d{4}-\d{4}(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[PHONE]"),
        (new Regex(
            @"(?<!\d)(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}(?!\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "[IP]"),
        (new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant), "[EMAIL]"),
    };

    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = NormalizeToAsciiDigits(input);
        foreach (var (pattern, replacement) in Rules)
        {
            result = pattern.Replace(result, replacement);
        }

        return result;
    }

    private static string NormalizeToAsciiDigits(string text)
        => text.Normalize(NormalizationForm.FormKC);
}
