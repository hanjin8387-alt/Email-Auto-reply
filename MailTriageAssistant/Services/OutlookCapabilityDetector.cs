using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class OutlookCapabilityDetector : IOutlookCapabilityDetector
{
    private readonly ILogger<OutlookCapabilityDetector> _logger;
    private readonly IOptionsMonitor<OutlookOptions> _optionsMonitor;
    private readonly object _gate = new();
    private OutlookCapabilitySnapshot? _cachedSnapshot;
    private DateTimeOffset _cacheExpiresAtUtc;

    public OutlookCapabilityDetector(
        IOptionsMonitor<OutlookOptions> optionsMonitor,
        ILogger<OutlookCapabilityDetector> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? NullLogger<OutlookCapabilityDetector>.Instance;
    }

    public OutlookCapabilitySnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (_cachedSnapshot is not null && now < _cacheExpiresAtUtc)
            {
                return _cachedSnapshot;
            }

            var hasClassicOutlook = Process.GetProcessesByName("outlook").Length > 0;
            var hasNewOutlook = Process.GetProcessesByName("olk").Length > 0;
            var isNewOutlookOnly = hasNewOutlook && !hasClassicOutlook;
            var diagnosticCode = isNewOutlookOnly
                ? "new_outlook_only"
                : hasClassicOutlook
                    ? "classic_available"
                    : hasNewOutlook
                        ? "new_outlook_detected"
                        : "no_outlook_process";

            var snapshot = new OutlookCapabilitySnapshot(
                HasClassicOutlook: hasClassicOutlook,
                HasNewOutlook: hasNewOutlook,
                IsNewOutlookOnly: isNewOutlookOnly,
                DiagnosticCode: diagnosticCode);

            _cachedSnapshot = snapshot;
            var ttlMinutes = Math.Max(1, _optionsMonitor.CurrentValue.CapabilityCheckTtlMinutes);
            _cacheExpiresAtUtc = now.AddMinutes(ttlMinutes);

            if (snapshot.IsNewOutlookOnly)
            {
                _logger.LogWarning(
                    "New Outlook only environment detected (diagnostic={DiagnosticCode}).",
                    snapshot.DiagnosticCode);
            }

            return snapshot;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cachedSnapshot = null;
            _cacheExpiresAtUtc = default;
        }
    }
}
