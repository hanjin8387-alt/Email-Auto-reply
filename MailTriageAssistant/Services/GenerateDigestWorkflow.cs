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

public sealed class GenerateDigestWorkflow
{
    private readonly SelectedEmailBodyLoader _bodyLoader;
    private readonly IDigestService _digestService;
    private readonly IDigestDeliveryService _digestDeliveryService;
    private readonly SessionStatsService _sessionStats;
    private readonly IOptionsMonitor<TriageSettings> _settingsMonitor;
    private readonly ILogger<GenerateDigestWorkflow> _logger;

    public GenerateDigestWorkflow(
        SelectedEmailBodyLoader bodyLoader,
        IDigestService digestService,
        IDigestDeliveryService digestDeliveryService,
        SessionStatsService sessionStats,
        IOptionsMonitor<TriageSettings> settingsMonitor,
        ILogger<GenerateDigestWorkflow> logger)
    {
        _bodyLoader = bodyLoader ?? throw new ArgumentNullException(nameof(bodyLoader));
        _digestService = digestService ?? throw new ArgumentNullException(nameof(digestService));
        _digestDeliveryService = digestDeliveryService ?? throw new ArgumentNullException(nameof(digestDeliveryService));
        _sessionStats = sessionStats ?? throw new ArgumentNullException(nameof(sessionStats));
        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _logger = logger ?? NullLogger<GenerateDigestWorkflow>.Instance;
    }

    public async Task<DigestWorkflowResult> RunAsync(
        IReadOnlyList<AnalyzedItem> emails,
        Task prefetchTask,
        string? teamsUserEmail,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var topCount = Math.Clamp(_settingsMonitor.CurrentValue.DigestTopCount, 1, 50);
        var topItems = emails
            .OrderByDescending(static x => x.Score)
            .ThenByDescending(static x => x.ReceivedTime)
            .Take(topCount)
            .ToList();

        try
        {
            try
            {
                await prefetchTask.ConfigureAwait(false);
            }
            catch
            {
                // Prefetch is best effort.
            }

            await _bodyLoader
                .EnsureBodiesLoadedAsync(topItems, OutlookOperationPriority.UserInitiated, ct)
                .ConfigureAwait(false);

            var digestItems = topItems
                .Select(static item => new DigestEmailItem(
                    Score: item.Score,
                    ReceivedTime: item.ReceivedTime,
                    RedactedSender: item.RedactedContent.Sender,
                    RedactedSubject: item.RedactedContent.Subject,
                    RedactedSummary: item.RedactedContent.Summary))
                .ToList();

            var digest = _digestService.GenerateDigest(digestItems);
            _sessionStats.RecordDigestGenerated();

            _digestDeliveryService.CopyDigestToClipboard(digest);
            _sessionStats.RecordDigestCopied();

            var teamsOpened = _digestDeliveryService.TryOpenTeams(teamsUserEmail);
            _sessionStats.RecordTeamsOpenAttempt();

            var status = teamsOpened
                ? LocalizedStrings.Get("Str.Status.DigestTeamsOpened", "Digest copied. Opening Teams.")
                : LocalizedStrings.Get("Str.Status.DigestTeamsFailed", "Digest copied. Failed to open Teams.");
            return new DigestWorkflowResult(status, teamsOpened);
        }
        catch (OperationCanceledException)
        {
            return new DigestWorkflowResult(
                LocalizedStrings.Get("Str.Status.DigestCanceled", "Digest generation canceled."),
                TeamsOpened: false);
        }
        catch (Exception ex)
        {
            _sessionStats.RecordError();
            _logger.LogError("Digest workflow failed: {ExceptionType}.", ex.GetType().Name);
            throw;
        }
    }
}

public sealed record DigestWorkflowResult(string StatusMessage, bool TeamsOpened);
