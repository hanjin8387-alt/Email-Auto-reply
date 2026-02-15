using System;
using System.Collections.Generic;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class TriageService
{
    public readonly record struct TriageResult(EmailCategory Category, int Score, string ActionHint, string[] Tags);

    private static readonly HashSet<string> VipSenders = new(StringComparer.OrdinalIgnoreCase)
    {
        "ceo@company.com",
        "cto@company.com",
        "manager@company.com",
    };

    private static readonly string[] ActionKeywords = new[]
    {
        "요청",
        "확인",
        "긴급",
        "ASAP",
        "기한",
        "Due",
    };

    private static readonly string[] ApprovalKeywords = new[]
    {
        "결재",
        "상신",
        "승인요청",
    };

    private static readonly string[] MeetingKeywords = new[]
    {
        "초대",
        "Invite",
        "회의",
        "미팅",
        "Zoom",
        "Teams",
    };

    private static readonly string[] NewsletterKeywords = new[]
    {
        "구독",
        "광고",
        "No-Reply",
        "News",
        "Unsubscribe",
    };

    private static readonly string[] FyiKeywords = new[]
    {
        "참고",
        "공유",
        "FYI",
        "공지",
    };

    public TriageResult AnalyzeHeader(string sender, string subject)
        => AnalyzeInternal(sender, subject, body: null);

    public TriageResult AnalyzeWithBody(string sender, string subject, string body)
        => AnalyzeInternal(sender, subject, body);

    private static TriageResult AnalyzeInternal(string sender, string subject, string? body)
    {
        var combined = (subject ?? string.Empty) + " " + (body ?? string.Empty);

        var isVip = IsVipSender(sender);
        var hasAction = ContainsAny(combined, ActionKeywords);
        var hasApproval = ContainsAny(combined, ApprovalKeywords);
        var hasMeeting = ContainsAny(combined, MeetingKeywords);
        var isNewsletter = ContainsAny(combined, NewsletterKeywords);
        var hasFyi = ContainsAny(combined, FyiKeywords);

        var score = 50;
        if (isVip) score += 30;
        if (hasAction) score += 20;
        if (hasApproval) score += 15;
        if (hasMeeting) score += 10;
        if (isNewsletter) score -= 50;

        if (!isVip && !isNewsletter && !LooksLikeKnownSender(sender))
        {
            score -= 10;
        }

        score = Math.Clamp(score, 0, 100);

        var category =
            hasAction ? EmailCategory.Action :
            hasApproval ? EmailCategory.Approval :
            isVip ? EmailCategory.VIP :
            hasMeeting ? EmailCategory.Meeting :
            isNewsletter ? EmailCategory.Newsletter :
            hasFyi ? EmailCategory.FYI :
            EmailCategory.Other;

        var hint = category switch
        {
            EmailCategory.Action => "즉시 처리 필요",
            EmailCategory.Approval => "결재/승인 확인 필요",
            EmailCategory.VIP => "VIP 응답 필요",
            EmailCategory.Meeting => "일정 확인 필요",
            EmailCategory.Newsletter => "구독 해제 고려",
            EmailCategory.FYI => "읽기 전용",
            _ => "검토",
        };

        var tags = new List<string>(capacity: 6);
        if (isVip) tags.Add("VIP");
        if (hasAction) tags.Add("Action");
        if (hasApproval) tags.Add("Approval");
        if (hasMeeting) tags.Add("Meeting");
        if (isNewsletter) tags.Add("Newsletter");
        if (hasFyi) tags.Add("FYI");

        return new TriageResult(category, score, hint, tags.ToArray());
    }

    private static bool IsVipSender(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        foreach (var vip in VipSenders)
        {
            if (sender.Contains(vip, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeKnownSender(string sender)
        => !string.IsNullOrWhiteSpace(sender) && sender.Contains('@', StringComparison.Ordinal);

    private static bool ContainsAny(string text, string[] keywords)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var keyword in keywords)
        {
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

