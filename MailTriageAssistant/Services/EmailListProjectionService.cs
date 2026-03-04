using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MailTriageAssistant.Helpers;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class EmailListProjectionService
{
    private readonly ITriageService _triageService;
    private readonly IRedactionService _redactionService;
    private readonly SessionStatsService _sessionStats;

    public EmailListProjectionService(
        ITriageService triageService,
        IRedactionService redactionService,
        SessionStatsService sessionStats)
    {
        _triageService = triageService ?? throw new ArgumentNullException(nameof(triageService));
        _redactionService = redactionService ?? throw new ArgumentNullException(nameof(redactionService));
        _sessionStats = sessionStats ?? throw new ArgumentNullException(nameof(sessionStats));
    }

    public IReadOnlyList<AnalyzedItem> Project(
        IReadOnlyList<RawEmailHeader> headers,
        IReadOnlyList<AnalyzedItem> existingItems,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(existingItems);

        var existingById = new Dictionary<string, AnalyzedItem>(StringComparer.Ordinal);
        foreach (var existing in existingItems)
        {
            if (!string.IsNullOrWhiteSpace(existing.EntryId) && !existingById.ContainsKey(existing.EntryId))
            {
                existingById.Add(existing.EntryId, existing);
            }
        }

        var projected = new List<AnalyzedItem>(headers.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(header.EntryId) || !seen.Add(header.EntryId))
            {
                continue;
            }

            var rawContent = new RawEmailContent(
                SenderName: header.SenderName ?? string.Empty,
                SenderEmail: header.SenderEmail ?? string.Empty,
                Subject: header.Subject ?? string.Empty,
                Body: string.Empty);

            var redacted = new RedactedEmailContent(
                Sender: _redactionService.Redact(header.SenderName ?? string.Empty),
                Subject: _redactionService.Redact(header.Subject ?? string.Empty),
                Summary: LocalizedStrings.Get("Str.Status.BodyLoadPendingSummary", "Select to load and view redacted summary."));

            if (existingById.TryGetValue(header.EntryId, out var existing))
            {
                existing.ReceivedTime = header.ReceivedTime;
                existing.UpdateRawContent(rawContent);
                existing.UpdateRedactedContent(existing.IsBodyLoaded ? existing.RedactedContent : redacted);

                if (!existing.IsBodyLoaded)
                {
                    var triage = _triageService.AnalyzeHeader(rawContent.SenderEmail, rawContent.Subject);
                    existing.Category = triage.Category;
                    existing.Score = triage.Score;
                    existing.ActionHint = triage.ActionHint;
                    existing.Tags = triage.Tags;
                }

                _sessionStats.RecordTriage(existing.Category);
                projected.Add(existing);
                continue;
            }

            var result = _triageService.AnalyzeHeader(rawContent.SenderEmail, rawContent.Subject);
            _sessionStats.RecordTriage(result.Category);
            var analyzed = new AnalyzedItem
            {
                EntryId = header.EntryId,
                ReceivedTime = header.ReceivedTime,
                HasAttachments = header.HasAttachments,
                Category = result.Category,
                Score = result.Score,
                ActionHint = result.ActionHint,
                Tags = result.Tags,
                IsBodyLoaded = false,
            };

            analyzed.UpdateRawContent(rawContent);
            analyzed.UpdateRedactedContent(redacted);
            projected.Add(analyzed);
        }

        return projected
            .OrderByDescending(static i => i.Score)
            .ThenByDescending(static i => i.ReceivedTime)
            .ToList();
    }

    public void ApplyDiff(RangeObservableCollection<AnalyzedItem> target, IReadOnlyList<AnalyzedItem> sorted)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sorted);

        if (target.Count == 0)
        {
            target.AddRange(sorted);
            return;
        }

        var currentIndexById = new Dictionary<string, int>(target.Count, StringComparer.Ordinal);

        void RefreshIndexRange(int start, int end)
        {
            for (var index = start; index <= end && index < target.Count; index++)
            {
                var entryId = target[index].EntryId;
                if (!string.IsNullOrWhiteSpace(entryId))
                {
                    currentIndexById[entryId] = index;
                }
            }
        }

        RefreshIndexRange(0, target.Count - 1);

        for (var i = 0; i < sorted.Count; i++)
        {
            var desired = sorted[i];
            var desiredEntryId = desired.EntryId;

            if (i >= target.Count)
            {
                target.Add(desired);
                if (!string.IsNullOrWhiteSpace(desiredEntryId))
                {
                    currentIndexById[desiredEntryId] = target.Count - 1;
                }

                continue;
            }

            if (string.Equals(target[i].EntryId, desiredEntryId, StringComparison.Ordinal))
            {
                continue;
            }

            var existingIndex = !string.IsNullOrWhiteSpace(desiredEntryId)
                                && currentIndexById.TryGetValue(desiredEntryId, out var resolvedIndex)
                                && resolvedIndex > i
                ? resolvedIndex
                : -1;

            if (existingIndex >= 0)
            {
                target.Move(existingIndex, i);
                RefreshIndexRange(i, existingIndex);
                continue;
            }

            target.Insert(i, desired);
            RefreshIndexRange(i, target.Count - 1);
        }

        while (target.Count > sorted.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    public static AnalyzedItem? RestoreSelection(IReadOnlyList<AnalyzedItem> items, string? selectedEntryId)
    {
        if (string.IsNullOrWhiteSpace(selectedEntryId))
        {
            return null;
        }

        return items.FirstOrDefault(item => string.Equals(item.EntryId, selectedEntryId, StringComparison.Ordinal));
    }
}
