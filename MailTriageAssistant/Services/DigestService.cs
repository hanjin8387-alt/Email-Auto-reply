using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class DigestService
{
    private readonly ClipboardSecurityHelper _clipboardHelper;
    private readonly RedactionService _redactionService;

    public DigestService(ClipboardSecurityHelper clipboardHelper, RedactionService redactionService)
    {
        _clipboardHelper = clipboardHelper ?? throw new ArgumentNullException(nameof(clipboardHelper));
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
    }

    public string GenerateDigest(IReadOnlyList<AnalyzedItem> items)
    {
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

        return sb.ToString();
    }

    public void OpenTeams(string digest, string? userEmail = null)
    {
        _clipboardHelper.SecureCopy(digest);

        var email = (userEmail ?? string.Empty).Trim();
        var https = string.IsNullOrWhiteSpace(email)
            ? "https://teams.microsoft.com"
            : $"https://teams.microsoft.com/l/chat/0/0?users={Uri.EscapeDataString(email)}";

        var msteams = string.IsNullOrWhiteSpace(email)
            ? "msteams:"
            : $"msteams:/l/chat/0/0?users={Uri.EscapeDataString(email)}";

        if (TryOpenUrl(https))
        {
            return;
        }

        if (TryOpenUrl(msteams))
        {
            return;
        }

        MessageBox.Show(
            "Teams를 열 수 없습니다.\n요약이 클립보드에 복사되었으니 직접 붙여넣어 주세요.",
            "Teams 연결 실패",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}

