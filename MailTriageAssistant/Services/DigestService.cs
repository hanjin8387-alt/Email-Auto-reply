using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class DigestService : IDigestService
{
    private static readonly Regex EscapeCellCharsRegex = new(@"[|\[\]()\!<>]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IRedactionService _redactionService;
    private readonly ILogger<DigestService> _logger;

    public DigestService(
        IRedactionService redactionService,
        ILogger<DigestService> logger)
    {
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _logger = logger ?? NullLogger<DigestService>.Instance;
    }

    public string GenerateDigest(IReadOnlyList<AnalyzedItem> items)
    {
        var sw = Stopwatch.StartNew();

        var sb = new StringBuilder();
        sb.AppendLine("🧠 SYSTEM PROMPT: You are my executive assistant. Analyze the following REDACTED email digest.");
        sb.AppendLine();
        sb.AppendLine("| Priority | Sender | Subject | Summary (Redacted) |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var item in items)
        {
            var priorityLabel = item.Score >= 80 ? "높음" : item.Score >= 50 ? "중간" : "낮음";

            var senderDisplay = string.IsNullOrWhiteSpace(item.SenderEmail)
                ? item.Sender
                : $"{item.Sender} <{item.SenderEmail}>";

            var sender = EscapeCell(_redactionService.Redact(senderDisplay));
            var subject = EscapeCell(_redactionService.Redact(item.Subject));
            var summary = EscapeCell(_redactionService.Redact(item.RedactedSummary));

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

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("Tasks:");
        sb.AppendLine("1. Identify the top 3 critical items requiring immediate action.");
        sb.AppendLine("2. List any deadlines or meeting requests.");
        sb.AppendLine("3. Draft a polite 1-sentence reply for the top item.");
        sb.AppendLine();
        sb.AppendLine("Context: All PII has been redacted. Do NOT ask for unredacted information.");

        sw.Stop();
        _logger.LogInformation("Digest generated: {Count} items in {ElapsedMs}ms.", items.Count, sw.ElapsedMilliseconds);
        return sb.ToString();
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
