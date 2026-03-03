using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class TemplateService : ITemplateService
{
    private const int MaxValueLength = 200;

    private static readonly Regex PlaceholderRegex = new(@"\{(?<key>[^{}\r\n]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<TemplateService> _logger;

    private readonly List<ReplyTemplate> _templates = new()
    {
        new ReplyTemplate
        {
            Id = "TMP_01",
            Title = "?섏떊 ?뺤씤 (Acknowledge)",
            BodyContent = "?덈뀞?섏꽭??\n\n硫붿씪 ??諛쏆븯?듬땲?? ?댁슜 ?뺤씤 ??{TargetDate}源뚯? ?뚯떊 ?쒕━寃좎뒿?덈떎.\n\n媛먯궗?⑸땲??",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "TargetDate", Label = "Target date", Placeholder = "e.g. 2026-03-05", IsRequired = true },
            },
        },
        new ReplyTemplate
        {
            Id = "TMP_02",
            Title = "異붽? ?뺣낫 ?붿껌 (Request Info)",
            BodyContent = "?덈뀞?섏꽭??\n\n寃?좊? ?꾪빐 ?꾨옒 ?뺣낫媛 異붽?濡??꾩슂?⑸땲??\n- {MissingInfo}\n\n怨듭쑀 遺?곷뱶由쎈땲??\n\n媛먯궗?⑸땲??",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "MissingInfo", Label = "Requested info", Placeholder = "e.g. contract file, owner contact", IsRequired = true },
            },
        },
        new ReplyTemplate
        {
            Id = "TMP_03",
            Title = "?쇱젙 ?쒖븞 (Propose Time)",
            BodyContent = "?덈뀞?섏꽭??\n\n?붿껌?섏떊 誘명똿 媛?ν빀?덈떎. ?꾨옒 ?꾨낫 以??명븯???쒓컙??留먯??댁＜?몄슂.\n- ?듭뀡1: {Date1}\n- ?듭뀡2: {Date2}\n\n媛먯궗?⑸땲??",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "Date1", Label = "?듭뀡 1", Placeholder = "?? 2026-03-04 10:00", IsRequired = true },
                new ReplyTemplateField { Key = "Date2", Label = "?듭뀡 2", Placeholder = "?? 2026-03-04 15:00", IsRequired = true },
            },
        },
        new ReplyTemplate
        {
            Id = "TMP_04",
            Title = "吏???덈궡 (Delay Notice)",
            BodyContent = "?덈뀞?섏꽭??\n\n?꾩옱 {Blocker} ?댁뒋濡??명빐 寃?좉? 吏?곕릺怨??덉뒿?덈떎. {NewDate}???낅뜲?댄듃 ?쒕━寃좎뒿?덈떎.\n\n?묓빐 遺?곷뱶由쎈땲??",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "Blocker", Label = "Delay reason", Placeholder = "e.g. waiting on another team", IsRequired = true },
                new ReplyTemplateField { Key = "NewDate", Label = "Updated date", Placeholder = "e.g. 2026-03-06", IsRequired = true },
            },
        },
        new ReplyTemplate
        {
            Id = "TMP_05",
            Title = "?꾨즺 蹂닿퀬 (Task Done)",
            BodyContent = "?덈뀞?섏꽭??\n\n?붿껌?섏떊 {TaskName} 嫄?泥섎━ ?꾨즺?섏뿀?듬땲?? 寃곌낵 ?뚯씪 泥⑤? ?쒕━???뺤씤 遺?곷뱶由쎈땲??\n\n媛먯궗?⑸땲??",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "TaskName", Label = "Task name", Placeholder = "e.g. February report", IsRequired = true },
            },
        },
        new ReplyTemplate
        {
            Id = "TMP_06",
            Title = "蹂대쪟/?湲?(On Hold)",
            BodyContent = "?덈뀞?섏꽭??\n\n愿??遺??{Dept}) ?뺤씤???꾩슂?섏뿬 ?좎떆 蹂대쪟 以묒엯?덈떎. ?뚯떊 諛쏅뒗 ?濡?怨듭쑀?섍쿋?듬땲??\n\n媛먯궗?⑸땲??",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "Dept", Label = "Department", Placeholder = "e.g. Legal", IsRequired = true },
            },
        },
        new ReplyTemplate
        {
            Id = "TMP_07",
            Title = "?뱀씤 (Approve)",
            BodyContent = "?덈뀞?섏꽭??\n\n?뚯떊?섏떊 {ItemName} 嫄??뱀씤?⑸땲?? 怨꾪쉷?濡?吏꾪뻾??二쇱꽭??\n\n媛먯궗?⑸땲??",
            Fields = new[]
            {
                new ReplyTemplateField { Key = "ItemName", Label = "?뱀씤 ??ぉ", Placeholder = "?? ?쒖븞??v2", IsRequired = true },
            },
        },
        new ReplyTemplate
        {
            Id = "TMP_08",
            Title = "?⑥닚 媛먯궗 (Thank You)",
            BodyContent = "?덈뀞?섏꽭??\n\n怨듭쑀 媛먯궗?⑸땲?? ?낅Т??李멸퀬?섍쿋?듬땲??\n\n媛먯궗?⑸땲??",
            Fields = Array.Empty<ReplyTemplateField>(),
        },
    };

    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger ?? NullLogger<TemplateService>.Instance;
    }

    public List<ReplyTemplate> GetTemplates()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Templates listed: {Count}.", _templates.Count);
        }

        return _templates.ToList();
    }

    public IReadOnlyList<string> ExtractPlaceholders(string templateBody)
    {
        if (string.IsNullOrEmpty(templateBody))
        {
            return Array.Empty<string>();
        }

        return PlaceholderRegex.Matches(templateBody)
            .Select(static m => m.Groups["key"].Value.Trim())
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string FillTemplate(string templateBody, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(templateBody))
        {
            return string.Empty;
        }

        var valueMap = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var filled = PlaceholderRegex.Replace(templateBody, match =>
        {
            var key = match.Groups["key"].Value.Trim();
            if (!valueMap.TryGetValue(key, out var val) || val is null)
            {
                return string.Empty;
            }

            return SanitizeValue(val);
        });

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Template filled (values={ValueCount}).", valueMap.Count);
        }

        return filled;
    }

    private static string SanitizeValue(string value)
    {
        var sanitized = (value ?? string.Empty)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (sanitized.Length > MaxValueLength)
        {
            sanitized = sanitized[..MaxValueLength];
        }

        if (string.IsNullOrWhiteSpace(sanitized) || string.Equals(sanitized, "___", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return sanitized;
    }
}
