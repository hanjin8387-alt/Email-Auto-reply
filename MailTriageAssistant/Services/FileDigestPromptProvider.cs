using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class FileDigestPromptProvider : IDigestPromptProvider
{
    private const string DefaultPrompt = """
SYSTEM PROMPT: You are my executive assistant. Analyze the following REDACTED email digest.

Tasks:
1. Identify the top 3 critical items requiring immediate action.
2. List deadlines or meeting requests.
3. Draft a polite one-sentence reply for the top item.

Context: All PII has been redacted. Do NOT ask for unredacted information.
""";

    private readonly IOptionsMonitor<DigestPromptOptions> _optionsMonitor;
    private readonly ILogger<FileDigestPromptProvider> _logger;
    private readonly object _cacheGate = new();
    private string? _cachedPrompt;

    public FileDigestPromptProvider(
        IOptionsMonitor<DigestPromptOptions> optionsMonitor,
        ILogger<FileDigestPromptProvider> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
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
                _cachedPrompt = DefaultPrompt;
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
                _cachedPrompt = DefaultPrompt;
                return _cachedPrompt;
            }
        }
    }
}
