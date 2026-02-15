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
    private const string MissingValue = "[미입력]";

    private static readonly Regex PlaceholderRegex = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<TemplateService> _logger;

    public TemplateService()
        : this(NullLogger<TemplateService>.Instance)
    {
    }

    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger ?? NullLogger<TemplateService>.Instance;
    }

    private readonly List<ReplyTemplate> _templates = new()
    {
        new ReplyTemplate
        {
            Id = "TMP_01",
            Title = "수신 확인 (Acknowledge)",
            BodyContent = "안녕하세요,\n\n메일 잘 받았습니다. 내용 확인 후 {TargetDate}까지 회신 드리겠습니다.\n\n감사합니다."
        },
        new ReplyTemplate
        {
            Id = "TMP_02",
            Title = "추가 정보 요청 (Request Info)",
            BodyContent = "안녕하세요,\n\n검토를 위해 아래 정보가 추가로 필요합니다.\n- {MissingInfo}\n\n공유 부탁드립니다.\n\n감사합니다."
        },
        new ReplyTemplate
        {
            Id = "TMP_03",
            Title = "일정 제안 (Propose Time)",
            BodyContent = "안녕하세요,\n\n요청하신 미팅 가능합니다. 다음 슬롯 중 편하신 시간 말씀해주세요.\n- 옵션1: {Date1}\n- 옵션2: {Date2}\n\n감사합니다."
        },
        new ReplyTemplate
        {
            Id = "TMP_04",
            Title = "지연 안내 (Delay Notice)",
            BodyContent = "안녕하세요,\n\n현재 {Blocker} 이슈로 인해 검토가 지연되고 있습니다. {NewDate}에 업데이트 드리겠습니다.\n\n양해 부탁드립니다."
        },
        new ReplyTemplate
        {
            Id = "TMP_05",
            Title = "완료 보고 (Task Done)",
            BodyContent = "안녕하세요,\n\n요청하신 {TaskName} 건 처리 완료했습니다. 결과 파일 첨부 드리오니 확인 부탁드립니다.\n\n감사합니다."
        },
        new ReplyTemplate
        {
            Id = "TMP_06",
            Title = "보류/대기 (On Hold)",
            BodyContent = "안녕하세요,\n\n유관부서({Dept}) 확인이 필요하여 잠시 보류 중입니다. 피드백 받는 대로 공유하겠습니다.\n\n감사합니다."
        },
        new ReplyTemplate
        {
            Id = "TMP_07",
            Title = "승인 (Approve)",
            BodyContent = "안녕하세요,\n\n상신하신 {ItemName} 건 승인합니다. 계획대로 진행해 주세요.\n\n감사합니다."
        },
        new ReplyTemplate
        {
            Id = "TMP_08",
            Title = "단순 감사 (Thank You)",
            BodyContent = "안녕하세요,\n\n공유 감사합니다. 업무에 참고하겠습니다.\n\n감사합니다."
        },
    };

    public List<ReplyTemplate> GetTemplates()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Templates listed: {Count}.", _templates.Count);
        }

        return _templates
            .Select(t => new ReplyTemplate { Id = t.Id, Title = t.Title, BodyContent = t.BodyContent })
            .ToList();
    }

    public string FillTemplate(string templateBody, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(templateBody))
        {
            return string.Empty;
        }

        var filled = PlaceholderRegex.Replace(templateBody, match =>
        {
            var key = match.Groups[1].Value;
            if (!values.TryGetValue(key, out var val) || val is null)
            {
                return MissingValue;
            }

            return SanitizeValue(val);
        });

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Template filled (values={ValueCount}).", values.Count);
        }

        return filled;
    }

    private static string SanitizeValue(string value)
    {
        // Prevent placeholder injection and keep the draft readable.
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
            return MissingValue;
        }

        return sanitized;
    }

}
