using System.Text.RegularExpressions;

namespace MailTriageAssistant.Helpers;

internal static partial class EmailValidator
{
    [GeneratedRegex(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    public static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email) && EmailPattern().IsMatch(email);
}
