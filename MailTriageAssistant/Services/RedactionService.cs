using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed partial class RedactionService : IRedactionService
{
    private readonly ILogger<RedactionService> _logger;

    public RedactionService()
        : this(NullLogger<RedactionService>.Instance)
    {
    }

    public RedactionService(ILogger<RedactionService> logger)
    {
        _logger = logger ?? NullLogger<RedactionService>.Instance;
    }

    private static readonly (Regex Pattern, string Replacement)[] Rules = new[]
    {
        // Order matters: more specific patterns first.
        (UrlTokenRegex(), "${name}=[URL_TOKEN]"),
        (AccountRegex(), "${label}${sep}[ACCOUNT]"),
        (PassportRegex(), "${label}${sep}[PASSPORT]"),
        (CardHyphenRegex(), "[CARD]"),
        (CardSpaceRegex(), "[CARD]"),
        (SsnHyphenRegex(), "[SSN]"),
        (SsnCompactRegex(), "[SSN]"),
        (KoreanPhoneRegex(), "[PHONE]"),
        (IpV4Regex(), "[IP]"),
        (EmailRegex(), "[EMAIL]"),
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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var changed = !string.Equals(result, input, StringComparison.Ordinal);
            _logger.LogDebug("Redact completed (changed={Changed}, length={Length}).", changed, result.Length);
        }

        return result;
    }

    private static string NormalizeToAsciiDigits(string text)
        => text.Normalize(NormalizationForm.FormKC);

    [GeneratedRegex(@"\b(?<name>token|access_token|id_token|refresh_token|api[-_]?key|apikey|sig|signature|secret|password|pwd|code|key)=(?<value>[^&\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlTokenRegex();

    [GeneratedRegex(@"\b(?<label>account|acct|iban|계좌(?:번호)?)\b(?<sep>\s*[:：]?\s*)(?<number>(?<!\d)\d{2,6}(?:[- ]\d{2,6}){1,3}(?!\d))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AccountRegex();

    [GeneratedRegex(@"\b(?<label>passport|여권(?:번호)?)\b(?<sep>\s*[:：]?\s*)(?<number>[A-Z]\d{8}|\d{9})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PassportRegex();

    [GeneratedRegex(@"(?<!\d)\d{4}-\d{4}-\d{4}-\d{4}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CardHyphenRegex();

    [GeneratedRegex(@"(?<!\d)\d{4}(?:\s+\d{4}){3}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CardSpaceRegex();

    [GeneratedRegex(@"(?<!\d)\d{6}-\d{7}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex SsnHyphenRegex();

    [GeneratedRegex(@"(?<!\d)\d{6}[1-8]\d{6}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex SsnCompactRegex();

    [GeneratedRegex(@"(?<!\d)010-\d{4}-\d{4}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex KoreanPhoneRegex();

    [GeneratedRegex(@"(?<!\d)(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?:\.(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex IpV4Regex();

    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
