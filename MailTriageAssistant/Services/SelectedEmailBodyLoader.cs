using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailTriageAssistant.Services;

public sealed class SelectedEmailBodyLoader : IDisposable
{
    private readonly IOutlookMailGateway _mailGateway;
    private readonly ITriageService _triageService;
    private readonly IRedactionService _redactionService;
    private readonly ILogger<SelectedEmailBodyLoader> _logger;
    private readonly object _selectedLoadGate = new();
    private CancellationTokenSource? _selectedLoadCts;

    public SelectedEmailBodyLoader(
        IOutlookMailGateway mailGateway,
        ITriageService triageService,
        IRedactionService redactionService,
        ILogger<SelectedEmailBodyLoader> logger)
    {
        _mailGateway = mailGateway ?? throw new ArgumentNullException(nameof(mailGateway));
        _triageService = triageService ?? throw new ArgumentNullException(nameof(triageService));
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _logger = logger ?? NullLogger<SelectedEmailBodyLoader>.Instance;
    }

    public async Task<bool> LoadSelectedBodyAsync(AnalyzedItem? item, CancellationToken ct = default)
    {
        if (item is null || item.IsBodyLoaded)
        {
            return false;
        }

        CancellationToken linkedToken;
        lock (_selectedLoadGate)
        {
            _selectedLoadCts?.Cancel();
            _selectedLoadCts?.Dispose();
            _selectedLoadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedToken = _selectedLoadCts.Token;
        }

        return await EnsureBodyAsync(item, OutlookOperationPriority.Interactive, linkedToken).ConfigureAwait(false);
    }

    public async Task<int> EnsureBodiesLoadedAsync(
        IReadOnlyList<AnalyzedItem> items,
        OutlookOperationPriority priority,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var targets = items
            .Where(static item => item is not null && !item.IsBodyLoaded && !string.IsNullOrWhiteSpace(item.EntryId))
            .ToList();

        if (targets.Count == 0)
        {
            return 0;
        }

        var entryIds = targets
            .Select(static x => x.EntryId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var loadedByEntryId = await _mailGateway
            .GetRawEmailContentsAsync(entryIds, priority, ct)
            .ConfigureAwait(false);

        var loadedCount = 0;
        foreach (var item in targets)
        {
            ct.ThrowIfCancellationRequested();
            if (!loadedByEntryId.TryGetValue(item.EntryId, out var rawContent))
            {
                continue;
            }

            ApplyBodyAnalysis(item, rawContent);
            loadedCount++;
        }

        return loadedCount;
    }

    private async Task<bool> EnsureBodyAsync(AnalyzedItem item, OutlookOperationPriority priority, CancellationToken ct)
    {
        try
        {
            var raw = await _mailGateway
                .GetRawEmailContentAsync(item.EntryId, priority, ct)
                .ConfigureAwait(false);

            ApplyBodyAnalysis(item, raw);
            return true;
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("LoadSelectedBody skipped due to {ExceptionType}.", ex.GetType().Name);
            return false;
        }
    }

    private void ApplyBodyAnalysis(AnalyzedItem item, RawEmailContent loadedRaw)
    {
        var senderName = string.IsNullOrWhiteSpace(loadedRaw.SenderName)
            ? item.RawContent.SenderName
            : loadedRaw.SenderName;
        var senderEmail = string.IsNullOrWhiteSpace(loadedRaw.SenderEmail)
            ? item.RawContent.SenderEmail
            : loadedRaw.SenderEmail;
        var subject = string.IsNullOrWhiteSpace(loadedRaw.Subject)
            ? item.RawContent.Subject
            : loadedRaw.Subject;

        var mergedRaw = new RawEmailContent(
            SenderName: senderName ?? string.Empty,
            SenderEmail: senderEmail ?? string.Empty,
            Subject: subject ?? string.Empty,
            Body: loadedRaw.Body ?? string.Empty);

        var triage = _triageService.AnalyzeWithBody(mergedRaw.SenderEmail, mergedRaw.Subject, mergedRaw.Body);
        var redactedSummary = string.IsNullOrWhiteSpace(mergedRaw.Body)
            ? LocalizedStrings.Get("Str.Status.EmptyBodySummary", "(Body is empty.)")
            : _redactionService.Redact(mergedRaw.Body);

        var redacted = new RedactedEmailContent(
            Sender: _redactionService.Redact(mergedRaw.SenderName),
            Subject: _redactionService.Redact(mergedRaw.Subject),
            Summary: redactedSummary);

        item.UpdateRawContent(mergedRaw);
        item.UpdateRedactedContent(redacted);
        item.Category = triage.Category;
        item.Score = triage.Score;
        item.ActionHint = triage.ActionHint;
        item.Tags = triage.Tags;
        item.IsBodyLoaded = true;
    }

    public void Dispose()
    {
        lock (_selectedLoadGate)
        {
            _selectedLoadCts?.Cancel();
            _selectedLoadCts?.Dispose();
            _selectedLoadCts = null;
        }
    }
}
