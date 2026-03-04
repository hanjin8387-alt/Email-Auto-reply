using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using MailTriageAssistant.Models;

namespace MailTriageAssistant.Services;

public sealed class SessionStatsService
{
    private int _headersLoaded;
    private int _digestsGenerated;
    private int _digestsCopied;
    private int _teamsOpenAttempts;
    private int _errors;
    private int _bodyBatchesRequested;
    private int _bodyBatchesLoaded;
    private int _bodyBatchesFailed;
    private int _bodyBatchesCanceled;

    private readonly Dictionary<EmailCategory, int> _categoryCounts = new();

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;

    public void RecordHeadersLoaded(int count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _headersLoaded, count);
    }

    public void RecordTriage(EmailCategory category)
    {
        lock (_categoryCounts)
        {
            _categoryCounts.TryGetValue(category, out var current);
            _categoryCounts[category] = current + 1;
        }
    }

    public void RecordDigestGenerated()
        => Interlocked.Increment(ref _digestsGenerated);

    public void RecordDigestCopied()
        => Interlocked.Increment(ref _digestsCopied);

    public void RecordTeamsOpenAttempt()
        => Interlocked.Increment(ref _teamsOpenAttempts);

    public void RecordError()
        => Interlocked.Increment(ref _errors);

    public void RecordBodyBatch(int requestedCount, int loadedCount, int failedCount, int canceledCount)
    {
        if (requestedCount > 0)
        {
            Interlocked.Add(ref _bodyBatchesRequested, requestedCount);
        }

        if (loadedCount > 0)
        {
            Interlocked.Add(ref _bodyBatchesLoaded, loadedCount);
        }

        if (failedCount > 0)
        {
            Interlocked.Add(ref _bodyBatchesFailed, failedCount);
        }

        if (canceledCount > 0)
        {
            Interlocked.Add(ref _bodyBatchesCanceled, canceledCount);
        }
    }

    public SessionStatsSnapshot Snapshot()
    {
        lock (_categoryCounts)
        {
            return new SessionStatsSnapshot(
                StartedAt,
                Volatile.Read(ref _headersLoaded),
                Volatile.Read(ref _digestsGenerated),
                Volatile.Read(ref _digestsCopied),
                Volatile.Read(ref _teamsOpenAttempts),
                Volatile.Read(ref _errors),
                Volatile.Read(ref _bodyBatchesRequested),
                Volatile.Read(ref _bodyBatchesLoaded),
                Volatile.Read(ref _bodyBatchesFailed),
                Volatile.Read(ref _bodyBatchesCanceled),
                new ReadOnlyDictionary<EmailCategory, int>(new Dictionary<EmailCategory, int>(_categoryCounts)));
        }
    }

    public sealed record SessionStatsSnapshot(
        DateTimeOffset StartedAt,
        int HeadersLoaded,
        int DigestsGenerated,
        int DigestsCopied,
        int TeamsOpenAttempts,
        int Errors,
        int BodyBatchesRequested,
        int BodyBatchesLoaded,
        int BodyBatchesFailed,
        int BodyBatchesCanceled,
        IReadOnlyDictionary<EmailCategory, int> CategoryCounts);
}

