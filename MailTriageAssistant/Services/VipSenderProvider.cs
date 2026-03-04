using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class VipSenderProvider
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<VipSenderProvider> _logger;
    private readonly object _gate = new();
    private string[] _current = Array.Empty<string>();
    private Task _warmupTask = Task.CompletedTask;

    public VipSenderProvider(ISettingsService settingsService, ILogger<VipSenderProvider> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? NullLogger<VipSenderProvider>.Instance;
    }

    public IReadOnlyList<string> Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public Task WarmupAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_warmupTask.IsCompleted)
            {
                _warmupTask = LoadCoreAsync(ct);
            }

            return _warmupTask;
        }
    }

    private async Task LoadCoreAsync(CancellationToken ct)
    {
        try
        {
            var loaded = await _settingsService.LoadVipSendersAsync(ct).ConfigureAwait(false);
            var normalized = loaded
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Select(static x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            lock (_gate)
            {
                _current = normalized;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("VIP sender settings load skipped: {ExceptionType}.", ex.GetType().Name);
            lock (_gate)
            {
                _current = Array.Empty<string>();
            }
        }
    }
}
