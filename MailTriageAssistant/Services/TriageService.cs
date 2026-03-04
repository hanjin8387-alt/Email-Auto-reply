using System;
using System.Collections.Generic;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class TriageService : ITriageService, IDisposable
{
    public readonly record struct TriageResult(
        EmailCategory Category,
        int Score,
        string ActionHint,
        string[] Tags,
        string[] MatchedRules,
        string[] Reasons);

    private readonly ILogger<TriageService> _logger;
    private readonly VipSenderProvider _vipSenderProvider;
    private CompiledTriageRules _compiledRules;
    private readonly IDisposable? _settingsSubscription;

    public TriageService(
        IOptionsMonitor<TriageSettings> optionsMonitor,
        VipSenderProvider vipSenderProvider,
        ILogger<TriageService> logger)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        _vipSenderProvider = vipSenderProvider ?? throw new ArgumentNullException(nameof(vipSenderProvider));
        _logger = logger ?? NullLogger<TriageService>.Instance;

        _compiledRules = CreateCompiledRules(optionsMonitor.CurrentValue ?? new TriageSettings());
        _settingsSubscription = optionsMonitor.OnChange(updated =>
        {
            _compiledRules = CreateCompiledRules(updated ?? new TriageSettings());
        });

        _ = _vipSenderProvider.WarmupAsync();
    }

    public void Dispose()
    {
        _settingsSubscription?.Dispose();
    }

    public TriageResult AnalyzeHeader(string sender, string subject)
        => AnalyzeInternal(sender, subject, body: null);

    public TriageResult AnalyzeWithBody(string sender, string subject, string body)
        => AnalyzeInternal(sender, subject, body);

    private TriageResult AnalyzeInternal(string sender, string subject, string? body)
    {
        var rules = _compiledRules;
        if (_vipSenderProvider.Current.Count > 0)
        {
            rules = new CompiledTriageRules(rules.Settings, MergeVipEntries(rules.Settings, _vipSenderProvider.Current));
        }

        var combined = (subject ?? string.Empty) + " " + (body ?? string.Empty);
        var matchedRules = new List<MatchedRule>(capacity: 8);

        var isVip = rules.IsVipSender(sender);
        if (isVip)
        {
            matchedRules.Add(new MatchedRule("vip_sender", rules.Settings.VipBonus, "VIP sender matched"));
        }

        var hasAction = rules.HasAction(combined);
        if (hasAction)
        {
            matchedRules.Add(new MatchedRule("action_keyword", rules.Settings.ActionBonus, "Action keyword matched"));
        }

        var hasApproval = rules.HasApproval(combined);
        if (hasApproval)
        {
            matchedRules.Add(new MatchedRule("approval_keyword", rules.Settings.ApprovalBonus, "Approval keyword matched"));
        }

        var hasMeeting = rules.HasMeeting(combined);
        if (hasMeeting)
        {
            matchedRules.Add(new MatchedRule("meeting_keyword", rules.Settings.MeetingBonus, "Meeting keyword matched"));
        }

        var isNewsletter = rules.IsNewsletter(combined);
        if (isNewsletter)
        {
            matchedRules.Add(new MatchedRule("newsletter_keyword", -rules.Settings.NewsletterPenalty, "Newsletter keyword matched"));
        }

        var hasFyi = rules.HasFyi(combined);
        if (hasFyi)
        {
            matchedRules.Add(new MatchedRule("fyi_keyword", 0, "FYI keyword matched"));
        }

        var score = rules.Settings.BaseScore;
        foreach (var matchedRule in matchedRules)
        {
            score += matchedRule.ScoreDelta;
        }

        if (!isVip && !isNewsletter && !CompiledTriageRules.LooksLikeKnownSender(sender))
        {
            score -= rules.Settings.UnknownSenderPenalty;
            matchedRules.Add(new MatchedRule("unknown_sender", -rules.Settings.UnknownSenderPenalty, "Unknown sender penalty"));
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
            EmailCategory.Action => LocalizedStrings.Get("Str.ActionHint.Action", "Immediate action required"),
            EmailCategory.Approval => LocalizedStrings.Get("Str.ActionHint.Approval", "Approval review needed"),
            EmailCategory.VIP => LocalizedStrings.Get("Str.ActionHint.Vip", "VIP response needed"),
            EmailCategory.Meeting => LocalizedStrings.Get("Str.ActionHint.Meeting", "Meeting scheduling needed"),
            EmailCategory.Newsletter => LocalizedStrings.Get("Str.ActionHint.Newsletter", "Consider unsubscribe"),
            EmailCategory.FYI => LocalizedStrings.Get("Str.ActionHint.Fyi", "For reference"),
            _ => LocalizedStrings.Get("Str.ActionHint.Other", "Review"),
        };

        var tags = new List<string>(capacity: 6);
        if (isVip) tags.Add("VIP");
        if (hasAction) tags.Add("Action");
        if (hasApproval) tags.Add("Approval");
        if (hasMeeting) tags.Add("Meeting");
        if (isNewsletter) tags.Add("Newsletter");
        if (hasFyi) tags.Add("FYI");

        var result = new TriageResult(
            Category: category,
            Score: score,
            ActionHint: hint,
            Tags: tags.ToArray(),
            MatchedRules: matchedRules.ConvertAll(static x => x.RuleCode).ToArray(),
            Reasons: matchedRules.ConvertAll(static x => x.Reason).ToArray());

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Triage analyzed (Category={Category}, Score={Score}, Rules={RuleCount}).",
                result.Category,
                result.Score,
                result.MatchedRules.Length);
        }

        return result;
    }

    private static IReadOnlyCollection<string> MergeVipEntries(TriageSettings settings, IReadOnlyList<string> loadedVipEntries)
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in settings.VipSenders ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                merged.Add(item.Trim());
            }
        }

        foreach (var item in loadedVipEntries ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                merged.Add(item.Trim());
            }
        }

        return merged;
    }

    private CompiledTriageRules CreateCompiledRules(TriageSettings settings)
        => new(settings, MergeVipEntries(settings, _vipSenderProvider.Current));
}
