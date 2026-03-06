using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class DigestService : IDigestService
{
    private static readonly Regex EscapeCellCharsRegex = new(@"[|\[\]()\!<>]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IDigestPromptProvider _promptProvider;
    private readonly ILogger<DigestService> _logger;

    public DigestService(
        IDigestPromptProvider promptProvider,
        ILogger<DigestService> logger)
    {
        _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider));
        _logger = logger ?? NullLogger<DigestService>.Instance;
    }

    public string GenerateDigest(IReadOnlyList<DigestEmailItem> items)
    {
        var sw = Stopwatch.StartNew();

        var sb = new StringBuilder();
        sb.AppendLine(_promptProvider.GetPrompt().TrimEnd());
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(BuildPayload(items), JsonOptions));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"| {LocalizedStrings.Get("Str.Digest.Header.Priority", "Priority")} | {LocalizedStrings.Get("Str.Digest.Header.Sender", "Sender")} | {LocalizedStrings.Get("Str.Digest.Header.Subject", "Subject")} | {LocalizedStrings.Get("Str.Digest.Header.Summary", "Summary (Redacted)")} |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var item in items)
        {
            var priorityLabel = item.Score >= 80
                ? LocalizedStrings.Get("Str.ScoreLabel.High", "High")
                : item.Score >= 50
                    ? LocalizedStrings.Get("Str.ScoreLabel.Medium", "Medium")
                    : item.Score >= 30
                        ? LocalizedStrings.Get("Str.ScoreLabel.Normal", "Normal")
                        : LocalizedStrings.Get("Str.ScoreLabel.Low", "Low");

            var sender = EscapeCell(item.RedactedSender);
            var subject = EscapeCell(item.RedactedSubject);
            var summary = EscapeCell(item.RedactedSummary);

            sb.Append("| ")
              .Append(item.Score)
              .Append(' ')
              .Append(priorityLabel)
              .Append(" | ")
              .Append(sender)
              .Append(" | ")
              .Append(subject)
              .Append(" | ")
              .Append(summary)
              .AppendLine(" |");
        }

        sw.Stop();
        _logger.LogInformation("Digest generated: {Count} items in {ElapsedMs}ms.", items.Count, sw.ElapsedMilliseconds);
        return sb.ToString();
    }

    private static object BuildPayload(IReadOnlyList<DigestEmailItem> items)
    {
        return new
        {
            schema = "mail-triage-assistant/digest-v1",
            item_count = items.Count,
            items = items.Select(item => new
            {
                score = item.Score,
                received_time = item.ReceivedTime,
                priority_label = item.Score >= 80
                    ? LocalizedStrings.Get("Str.ScoreLabel.High", "High")
                    : item.Score >= 50
                        ? LocalizedStrings.Get("Str.ScoreLabel.Medium", "Medium")
                        : item.Score >= 30
                            ? LocalizedStrings.Get("Str.ScoreLabel.Normal", "Normal")
                            : LocalizedStrings.Get("Str.ScoreLabel.Low", "Low"),
                sender = item.RedactedSender ?? string.Empty,
                subject = item.RedactedSubject ?? string.Empty,
                summary = item.RedactedSummary ?? string.Empty,
            }),
        };
    }

    private static string EscapeCell(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var escaped = EscapeCellCharsRegex.Replace(text, m => "\\" + m.Value);
        return escaped
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}
