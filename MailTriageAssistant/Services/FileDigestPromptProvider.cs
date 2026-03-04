using System;
using System.IO;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class FileDigestPromptProvider : IDigestPromptProvider
{
    private const string DefaultPromptEn = """
SYSTEM PROMPT: You are my executive assistant. Analyze the following REDACTED email digest.

Tasks:
1. Identify the top 3 critical items requiring immediate action.
2. List deadlines or meeting requests.
3. Draft a polite one-sentence reply for the top item.

Context: All PII has been redacted. Do NOT ask for unredacted information.
""";
    private const string DefaultPromptKo = """
SYSTEM PROMPT: 당신은 나의 업무 보조 비서입니다. 아래 REDACTED 이메일 Digest를 분석하세요.

작업:
1. 즉시 조치가 필요한 핵심 항목 3개를 추려주세요.
2. 마감일 또는 미팅 요청을 정리해주세요.
3. 최우선 항목에 대한 공손한 한 줄 회신 문안을 작성해주세요.

Context: 모든 개인정보는 이미 마스킹되었습니다. 비식별 해제나 원문 제공을 요청하지 마세요.
""";

    private readonly IOptionsMonitor<DigestPromptOptions> _optionsMonitor;
    private readonly IOptionsMonitor<TriageSettings> _triageSettingsMonitor;
    private readonly ILogger<FileDigestPromptProvider> _logger;
    private readonly object _cacheGate = new();
    private string? _cachedPrompt;

    public FileDigestPromptProvider(
        IOptionsMonitor<DigestPromptOptions> optionsMonitor,
        IOptionsMonitor<TriageSettings> triageSettingsMonitor,
        ILogger<FileDigestPromptProvider> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _triageSettingsMonitor = triageSettingsMonitor ?? throw new ArgumentNullException(nameof(triageSettingsMonitor));
        _logger = logger ?? NullLogger<FileDigestPromptProvider>.Instance;
    }

    public string GetPrompt()
    {
        lock (_cacheGate)
        {
            if (_cachedPrompt is not null)
            {
                return _cachedPrompt;
            }

            var configuredPath = _optionsMonitor.CurrentValue.PromptPath ?? string.Empty;
            var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Digest prompt file missing; fallback prompt will be used.");
                _cachedPrompt = GetFallbackPrompt();
                return _cachedPrompt;
            }

            try
            {
                _cachedPrompt = File.ReadAllText(fullPath);
                return _cachedPrompt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Digest prompt load failed: {ExceptionType}.", ex.GetType().Name);
                _cachedPrompt = GetFallbackPrompt();
                return _cachedPrompt;
            }
        }
    }

    private string GetFallbackPrompt()
    {
        var language = _triageSettingsMonitor.CurrentValue.Language;
        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? DefaultPromptEn
            : DefaultPromptKo;
    }
}
