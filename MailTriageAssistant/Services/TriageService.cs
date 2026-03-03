using System;
using System.Collections.Generic;
using System.Linq;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class TriageService : ITriageService, IDisposable
{
    public readonly record struct TriageResult(EmailCategory Category, int Score, string ActionHint, string[] Tags);

    private TriageSettings _settings;
    private string[] _userVipSenders = Array.Empty<string>();
    private readonly ISettingsService? _settingsService;
    private readonly ILogger<TriageService> _logger;
    private readonly IDisposable? _settingsSubscription;

    public TriageService(IOptionsMonitor<TriageSettings> optionsMonitor, ILogger<TriageService> logger)
        : this(optionsMonitor, settingsService: null, logger, loadUserVipSenders: false)
    {
    }

    public TriageService(IOptionsMonitor<TriageSettings> optionsMonitor, ISettingsService settingsService, ILogger<TriageService> logger)
        : this(optionsMonitor, settingsService, logger, loadUserVipSenders: true)
    {
    }

    private TriageService(
        IOptionsMonitor<TriageSettings> optionsMonitor,
        ISettingsService? settingsService,
        ILogger<TriageService> logger,
        bool loadUserVipSenders)
    {
        _logger = logger ?? NullLogger<TriageService>.Instance;
        _settingsService = loadUserVipSenders ? settingsService : null;
        _userVipSenders = loadUserVipSenders ? LoadUserVipSenders() : Array.Empty<string>();
        _settings = MergeVipSenders(CloneSettings(optionsMonitor?.CurrentValue ?? new TriageSettings()), _userVipSenders);
        _settingsSubscription = optionsMonitor?.OnChange(updated =>
        {
            _settings = MergeVipSenders(CloneSettings(updated), _userVipSenders);
        });
    }

    private TriageService(TriageSettings settings, ILogger<TriageService> logger)
    {
        _settings = CloneSettings(settings ?? throw new ArgumentNullException(nameof(settings)));
        _settingsService = null;
        _logger = logger ?? NullLogger<TriageService>.Instance;
        _settingsSubscription = null;
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

        // Category priority: Action > Approval > VIP > Meeting > Newsletter > FYI > Other.
        // If a VIP sender sends an action-required email, it is classified as Action
        // (highest urgency). VIP is still added to Tags for filtering.
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
            EmailCategory.Action => "利됱떆 泥섎━ ?꾩슂",
            EmailCategory.Approval => "寃곗옱/?뱀씤 ?뺤씤 ?꾩슂",
            EmailCategory.VIP => "VIP ?묐떟 ?꾩슂",
            EmailCategory.Meeting => "?쇱젙 ?뺤씤 ?꾩슂",
            EmailCategory.Newsletter => "援щ룆 ?댁젣 怨좊젮",
            EmailCategory.FYI => "李멸퀬 ?꾩슜",
            _ => "Review",
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

    private string[] LoadUserVipSenders()
    {
        if (_settingsService is null)
        {
            return Array.Empty<string>();
        }

        try
        {
            var loaded = _settingsService
                .LoadVipSendersAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            return loaded
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Select(static x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("VIP sender settings load skipped: {ExceptionType}.", ex.GetType().Name);
            return Array.Empty<string>();
        }
    }

    private static TriageSettings MergeVipSenders(TriageSettings settings, IReadOnlyList<string> userVipSenders)
    {
        settings.VipSenders = (settings.VipSenders ?? Array.Empty<string>())
            .Concat(userVipSenders ?? Array.Empty<string>())
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return settings;
    }

    private static TriageSettings CloneSettings(TriageSettings source)
    {
        return new TriageSettings
        {
            Language = source.Language,
            AutoRefreshIntervalMinutes = source.AutoRefreshIntervalMinutes,
            EnableSystemTray = source.EnableSystemTray,
            PrefetchCount = source.PrefetchCount,
            VipSenders = (source.VipSenders ?? Array.Empty<string>()).ToArray(),
            ActionKeywords = (source.ActionKeywords ?? Array.Empty<string>()).ToArray(),
            ApprovalKeywords = (source.ApprovalKeywords ?? Array.Empty<string>()).ToArray(),
            MeetingKeywords = (source.MeetingKeywords ?? Array.Empty<string>()).ToArray(),
            NewsletterKeywords = (source.NewsletterKeywords ?? Array.Empty<string>()).ToArray(),
            FyiKeywords = (source.FyiKeywords ?? Array.Empty<string>()).ToArray(),
            BaseScore = source.BaseScore,
            VipBonus = source.VipBonus,
            ActionBonus = source.ActionBonus,
            ApprovalBonus = source.ApprovalBonus,
            MeetingBonus = source.MeetingBonus,
            NewsletterPenalty = source.NewsletterPenalty,
            UnknownSenderPenalty = source.UnknownSenderPenalty,
        };
    }

    private bool IsVipSender(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        var normalizedSender = NormalizeSender(sender);
        var senderDomain = ExtractDomain(normalizedSender);

        foreach (var vip in _settings.VipSenders ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(vip))
            {
                continue;
            }

            var normalizedVip = NormalizeSender(vip);
            if (normalizedVip.StartsWith("@", StringComparison.Ordinal))
            {
                var vipDomain = normalizedVip[1..];
                if (!string.IsNullOrWhiteSpace(senderDomain)
                    && string.Equals(senderDomain, vipDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (normalizedVip.Contains('@', StringComparison.Ordinal))
            {
                if (string.Equals(normalizedSender, normalizedVip, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (string.Equals(sender.Trim(), normalizedVip, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeKnownSender(string sender)
    {
        var normalized = NormalizeSender(sender);
        if (ExtractDomain(normalized).Length > 0)
        {
            return true;
        }

        // Exchange/X.500 sender identifiers do not contain '@' but should not be treated as unknown senders.
        return normalized.StartsWith("/O=", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/CN=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var keyword in keywords ?? Array.Empty<string>())
        {
            if (ContainsKeyword(text, keyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsKeyword(string text, string keyword)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        var normalizedKeyword = keyword.Trim();
        if (!NeedsWordBoundary(normalizedKeyword))
        {
            return text.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(normalizedKeyword, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var beforeValid = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var afterIndex = index + normalizedKeyword.Length;
            var afterValid = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
            if (beforeValid && afterValid)
            {
                return true;
            }

            start = index + normalizedKeyword.Length;
        }

        return false;
    }

    private static bool NeedsWordBoundary(string keyword)
    {
        foreach (var ch in keyword)
        {
            if (ch > 127)
            {
                return false;
            }

            if (!(char.IsLetterOrDigit(ch) || ch is '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeSender(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return string.Empty;
        }

        var trimmed = sender.Trim();
        var lt = trimmed.IndexOf('<');
        if (lt < 0)
        {
            return trimmed;
        }

        var gt = trimmed.IndexOf('>', lt + 1);
        if (gt <= lt + 1)
        {
            return trimmed;
        }

        return trimmed[(lt + 1)..gt].Trim();
    }

    private static string ExtractDomain(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return string.Empty;
        }

        var at = sender.LastIndexOf('@');
        if (at < 0 || at == sender.Length - 1)
        {
            return string.Empty;
        }

        return sender[(at + 1)..].Trim();
    }
}
