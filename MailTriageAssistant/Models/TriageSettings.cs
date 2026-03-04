using System;

namespace MailTriageAssistant.Models;

public sealed class TriageSettings
{
    public string Language { get; set; } = "ko";

    public int AutoRefreshIntervalMinutes { get; set; } = 0;

    public bool EnableSystemTray { get; set; } = true;

    public int PrefetchCount { get; set; } = 10;

    public int DigestTopCount { get; set; } = 10;

    public int ClipboardAutoClearSeconds { get; set; } = 30;

    public int AutoRefreshFailurePauseThreshold { get; set; } = 3;

    public string[] VipSenders { get; set; } = Array.Empty<string>();

    public string[] ActionKeywords { get; set; } = Array.Empty<string>();

    public string[] ApprovalKeywords { get; set; } = Array.Empty<string>();

    public string[] MeetingKeywords { get; set; } = Array.Empty<string>();

    public string[] NewsletterKeywords { get; set; } = Array.Empty<string>();

    public string[] FyiKeywords { get; set; } = Array.Empty<string>();

    public int BaseScore { get; set; } = 50;
    public int VipBonus { get; set; } = 30;
    public int ActionBonus { get; set; } = 20;
    public int ApprovalBonus { get; set; } = 15;
    public int MeetingBonus { get; set; } = 10;
    public int NewsletterPenalty { get; set; } = 50;
    public int UnknownSenderPenalty { get; set; } = 10;
}
