using System;
using System.Collections.Generic;
using System.Linq;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class CompiledTriageRules
{
    private readonly KeywordMatcher[] _actionMatchers;
    private readonly KeywordMatcher[] _approvalMatchers;
    private readonly KeywordMatcher[] _meetingMatchers;
    private readonly KeywordMatcher[] _newsletterMatchers;
    private readonly KeywordMatcher[] _fyiMatchers;

    public CompiledTriageRules(
        TriageSettings settings,
        IReadOnlyCollection<string> vipEntries)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Settings = CloneSettings(settings);
        VipEntries = (vipEntries ?? Array.Empty<string>())
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => NormalizeSender(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _actionMatchers = Compile(Settings.ActionKeywords);
        _approvalMatchers = Compile(Settings.ApprovalKeywords);
        _meetingMatchers = Compile(Settings.MeetingKeywords);
        _newsletterMatchers = Compile(Settings.NewsletterKeywords);
        _fyiMatchers = Compile(Settings.FyiKeywords);
    }

    public TriageSettings Settings { get; }

    public IReadOnlyList<string> VipEntries { get; }

    public bool HasAction(string text) => ContainsAny(_actionMatchers, text);

    public bool HasApproval(string text) => ContainsAny(_approvalMatchers, text);

    public bool HasMeeting(string text) => ContainsAny(_meetingMatchers, text);

    public bool IsNewsletter(string text) => ContainsAny(_newsletterMatchers, text);

    public bool HasFyi(string text) => ContainsAny(_fyiMatchers, text);

    public bool IsVipSender(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        var normalizedSender = NormalizeSender(sender);
        var senderDomain = ExtractDomain(normalizedSender);

        foreach (var vip in VipEntries)
        {
            if (vip.StartsWith("@", StringComparison.Ordinal))
            {
                var vipDomain = vip[1..];
                if (!string.IsNullOrWhiteSpace(senderDomain)
                    && string.Equals(senderDomain, vipDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (vip.Contains('@', StringComparison.Ordinal))
            {
                if (string.Equals(normalizedSender, vip, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (string.Equals(sender.Trim(), vip, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool LooksLikeKnownSender(string sender)
    {
        var normalized = NormalizeSender(sender);
        if (ExtractDomain(normalized).Length > 0)
        {
            return true;
        }

        return normalized.StartsWith("/O=", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/CN=", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeSender(string sender)
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

    public static string ExtractDomain(string sender)
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

    private static bool ContainsAny(IReadOnlyList<KeywordMatcher> matchers, string text)
    {
        if (matchers.Count == 0 || string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var i = 0; i < matchers.Count; i++)
        {
            if (matchers[i].IsMatch(text))
            {
                return true;
            }
        }

        return false;
    }

    private static KeywordMatcher[] Compile(string[] keywords)
    {
        return (keywords ?? Array.Empty<string>())
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => KeywordMatcher.Create(x.Trim()))
            .ToArray();
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

    private readonly record struct KeywordMatcher(string Keyword, bool NeedsBoundary)
    {
        public static KeywordMatcher Create(string keyword)
            => new(keyword, NeedsWordBoundary(keyword));

        public bool IsMatch(string text)
        {
            if (!NeedsBoundary)
            {
                return text.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            var start = 0;
            while (start < text.Length)
            {
                var index = text.IndexOf(Keyword, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                var beforeValid = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                var afterIndex = index + Keyword.Length;
                var afterValid = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
                if (beforeValid && afterValid)
                {
                    return true;
                }

                start = index + Keyword.Length;
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
    }
}
