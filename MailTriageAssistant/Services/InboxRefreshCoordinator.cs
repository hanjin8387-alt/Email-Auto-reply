using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailTriageAssistant.Services;

public sealed class InboxRefreshCoordinator
{
    private readonly IOutlookMailGateway _mailGateway;
    private readonly EmailListProjectionService _projectionService;
    private readonly SelectedEmailBodyLoader _bodyLoader;
    private readonly SessionStatsService _sessionStats;
    private readonly IOptionsMonitor<TriageSettings> _settingsMonitor;
    private readonly ILogger<InboxRefreshCoordinator> _logger;

    public InboxRefreshCoordinator(
        IOutlookMailGateway mailGateway,
        EmailListProjectionService projectionService,
        SelectedEmailBodyLoader bodyLoader,
        SessionStatsService sessionStats,
        IOptionsMonitor<TriageSettings> settingsMonitor,
        ILogger<InboxRefreshCoordinator> logger)
    {
        _mailGateway = mailGateway ?? throw new ArgumentNullException(nameof(mailGateway));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _bodyLoader = bodyLoader ?? throw new ArgumentNullException(nameof(bodyLoader));
        _sessionStats = sessionStats ?? throw new ArgumentNullException(nameof(sessionStats));
        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _logger = logger ?? NullLogger<InboxRefreshCoordinator>.Instance;
    }

    public async Task<InboxRefreshResult> RefreshAsync(
        IReadOnlyList<AnalyzedItem> existingItems,
        CancellationToken ct = default)
    {
        try
        {
            var headers = await _mailGateway.FetchInboxHeadersAsync(ct).ConfigureAwait(false);
            _sessionStats.RecordHeadersLoaded(headers.Count);

            var sorted = _projectionService.Project(headers, existingItems, ct);
            var status = sorted.Count == 0
                ? LocalizedStrings.Get("Str.Status.NoEmails", "No emails to display.")
                : LocalizedStrings.GetFormat("Str.Status.LoadCompletedCount", "Loaded {0} emails.", sorted.Count);

            var prefetchTask = StartPrefetchAsync(sorted, ct);
            return new InboxRefreshResult(InboxRefreshOutcome.Success, sorted, status, prefetchTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Inbox refresh canceled.");
            return new InboxRefreshResult(
                InboxRefreshOutcome.Cancelled,
                SortedItems: existingItems.ToArray(),
                StatusMessage: LocalizedStrings.Get("Str.Status.LoadCanceled", "Email load canceled."),
                PrefetchTask: Task.CompletedTask);
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("Inbox refresh failed: {ExceptionType}.", ex.GetType().Name);
            return new InboxRefreshResult(
                InboxRefreshOutcome.Failure,
                SortedItems: existingItems.ToArray(),
                StatusMessage: LocalizedStrings.Get("Str.Status.LoadFailed", "Failed to load emails."),
                PrefetchTask: Task.CompletedTask);
        }
    }

    private Task StartPrefetchAsync(IReadOnlyList<AnalyzedItem> sorted, CancellationToken ct)
    {
        var prefetchCount = Math.Clamp(_settingsMonitor.CurrentValue.PrefetchCount, 0, 50);
        if (prefetchCount <= 0)
        {
            return Task.CompletedTask;
        }

        var targets = sorted
            .Take(prefetchCount)
            .Where(static item => !item.IsBodyLoaded && !string.IsNullOrWhiteSpace(item.EntryId))
            .ToList();

        if (targets.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _bodyLoader.EnsureBodiesLoadedAsync(targets, OutlookOperationPriority.Background, ct);
    }
}
