using System;

namespace MailTriageAssistant.Models;

public sealed class TriageSettings
{
    public string Language { get; set; } = "ko";

    public int AutoRefreshIntervalMinutes { get; set; } = 0;

    public int PrefetchCount { get; set; } = 10;

    public string[] VipSenders { get; set; } = new[]
    {
        "ceo@company.com",
        "cto@company.com",
        "manager@company.com",
    };

    public string[] ActionKeywords { get; set; } = new[]
    {
        "요청",
        "확인",
        "긴급",
        "ASAP",
        "기한",
        "Due",
    };

    public string[] ApprovalKeywords { get; set; } = new[]
    {
        "결재",
        "상신",
        "승인요청",
    };

    public string[] MeetingKeywords { get; set; } = new[]
    {
        "초대",
        "Invite",
        "회의",
        "미팅",
        "Zoom",
        "Teams",
    };

    public string[] NewsletterKeywords { get; set; } = new[]
    {
        "구독",
        "광고",
        "No-Reply",
        "News",
        "Unsubscribe",
    };

    public string[] FyiKeywords { get; set; } = new[]
    {
        "참고",
        "공유",
        "FYI",
        "공지",
    };

    public int BaseScore { get; set; } = 50;
    public int VipBonus { get; set; } = 30;
    public int ActionBonus { get; set; } = 20;
    public int ApprovalBonus { get; set; } = 15;
    public int MeetingBonus { get; set; } = 10;
    public int NewsletterPenalty { get; set; } = 50;
    public int UnknownSenderPenalty { get; set; } = 10;
}
