using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class DigestService : IDigestService
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ClipboardSecurityHelper _clipboardHelper;
    private readonly RedactionService _redactionService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<DigestService> _logger;

    public DigestService(ClipboardSecurityHelper clipboardHelper, RedactionService redactionService, IDialogService dialogService)
        : this(clipboardHelper, redactionService, dialogService, NullLogger<DigestService>.Instance)
    {
    }

    public DigestService(
        ClipboardSecurityHelper clipboardHelper,
        RedactionService redactionService,
        IDialogService dialogService,
        ILogger<DigestService> logger)
    {
        _clipboardHelper = clipboardHelper ?? throw new ArgumentNullException(nameof(clipboardHelper));
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? NullLogger<DigestService>.Instance;
    }

    public string GenerateDigest(IReadOnlyList<AnalyzedItem> items)
    {
        var sw = Stopwatch.StartNew();

        var ordered = items
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.ReceivedTime)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("⚠️ SYSTEM PROMPT: You are my executive assistant. Analyze the following REDACTED email digest.");
        sb.AppendLine();
        sb.AppendLine("| Priority | Sender | Subject | Summary (Redacted) |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var item in ordered)
        {
            var priorityLabel = item.Score >= 80 ? "높음" : item.Score >= 50 ? "중간" : "낮음";

            var senderDisplay = string.IsNullOrWhiteSpace(item.SenderEmail)
                ? item.Sender
                : $"{item.Sender} <{item.SenderEmail}>";

            var sender = EscapeCell(_redactionService.Redact(senderDisplay));
            var subject = EscapeCell(_redactionService.Redact(item.Subject));
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

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("Tasks:");
        sb.AppendLine("1. Identify the top 3 critical items requiring immediate action.");
        sb.AppendLine("2. List any deadlines or meeting requests.");
        sb.AppendLine("3. Draft a polite 1-sentence reply for the top item.");
        sb.AppendLine();
        sb.AppendLine("Context: All PII has been redacted. Do NOT ask for unredacted information.");

        sw.Stop();
        _logger.LogInformation("Digest generated: {Count} items in {ElapsedMs}ms.", ordered.Count, sw.ElapsedMilliseconds);
        return sb.ToString();
    }

    public void OpenTeams(string digest, string? userEmail = null)
    {
        _clipboardHelper.SecureCopy(digest, alreadyRedacted: true);

        var email = (userEmail ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(email) && !EmailRegex.IsMatch(email))
        {
            email = string.Empty;
        }
        var https = string.IsNullOrWhiteSpace(email)
            ? "https://teams.microsoft.com"
            : $"https://teams.microsoft.com/l/chat/0/0?users={Uri.EscapeDataString(email)}";

        var msteams = string.IsNullOrWhiteSpace(email)
            ? "msteams:"
            : $"msteams:/l/chat/0/0?users={Uri.EscapeDataString(email)}";

        _logger.LogInformation("OpenTeams requested (hasUserEmail={HasUserEmail}).", !string.IsNullOrWhiteSpace(email));

        if (TryOpenUrl(https))
        {
            _logger.LogInformation("Teams opened via https.");
            return;
        }

        if (TryOpenUrl(msteams))
        {
            _logger.LogInformation("Teams opened via msteams.");
            return;
        }

        _logger.LogWarning("Failed to open Teams via https and msteams.");
        _dialogService.ShowInfo(
            "Teams를 열 수 없습니다.\n요약이 클립보드에 복사되었으니 Teams에 직접 붙여넣어 주세요.",
            "Teams 연결 실패");
    }

    private static bool TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeCell(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("!", "\\!", StringComparison.Ordinal)
            .Replace("<", "\\<", StringComparison.Ordinal)
            .Replace(">", "\\>", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}
