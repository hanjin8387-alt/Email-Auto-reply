using System;
using System.Collections.Generic;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class TriageService : ITriageService
{
    public readonly record struct TriageResult(EmailCategory Category, int Score, string ActionHint, string[] Tags);

    private TriageSettings _settings;
    private readonly ILogger<TriageService> _logger;

    public TriageService()
        : this(new TriageSettings(), NullLogger<TriageService>.Instance)
    {
    }

    public TriageService(IOptionsMonitor<TriageSettings> optionsMonitor)
        : this(optionsMonitor, NullLogger<TriageService>.Instance)
    {
    }

    public TriageService(IOptionsMonitor<TriageSettings> optionsMonitor, ILogger<TriageService> logger)
    {
        _logger = logger ?? NullLogger<TriageService>.Instance;
        _settings = optionsMonitor?.CurrentValue ?? new TriageSettings();
        _ = optionsMonitor?.OnChange(updated => _settings = updated);
    }

    private TriageService(TriageSettings settings, ILogger<TriageService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? NullLogger<TriageService>.Instance;
    }

    public TriageResult AnalyzeHeader(string sender, string subject)
        => AnalyzeInternal(sender, subject, body: null);

    public TriageResult AnalyzeWithBody(string sender, string subject, string body)
        => AnalyzeInternal(sender, subject, body);

    private TriageResult AnalyzeInternal(string sender, string subject, string? body)
    {
        var combined = (subject ?? string.Empty) + " " + (body ?? string.Empty);

        var isVip = IsVipSender(sender);
        var hasAction = ContainsAny(combined, _settings.ActionKeywords);
        var hasApproval = ContainsAny(combined, _settings.ApprovalKeywords);
        var hasMeeting = ContainsAny(combined, _settings.MeetingKeywords);
        var isNewsletter = ContainsAny(combined, _settings.NewsletterKeywords);
        var hasFyi = ContainsAny(combined, _settings.FyiKeywords);

        var score = _settings.BaseScore;
        if (isVip) score += _settings.VipBonus;
        if (hasAction) score += _settings.ActionBonus;
        if (hasApproval) score += _settings.ApprovalBonus;
        if (hasMeeting) score += _settings.MeetingBonus;
        if (isNewsletter) score -= _settings.NewsletterPenalty;

        if (!isVip && !isNewsletter && !LooksLikeKnownSender(sender))
        {
            score -= _settings.UnknownSenderPenalty;
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

        var result = new TriageResult(category, score, hint, tags.ToArray());

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Triage analyzed (Category={Category}, Score={Score}).", result.Category, result.Score);
        }

        return result;
    }

    private bool IsVipSender(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        foreach (var vip in _settings.VipSenders ?? Array.Empty<string>())
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

        foreach (var keyword in keywords ?? Array.Empty<string>())
        {
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
